using HeroRegistry.Application.Common;
using HeroRegistry.Domain.Users;
using HeroRegistry.Infrastructure.Tests.Fixtures;
using Microsoft.Data.SqlClient;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Repositories;

/// <summary>RefreshTokenRepository against a real SQL Server (Testcontainers), on the 0004 migration.</summary>
public class RefreshTokenRepositoryTests(SqlServerContainerFixture sqlServer) : RepositoryTestBase(sqlServer)
{
    private static readonly DateTime IssuedAt = new(2026, 11, 22, 15, 0, 0, DateTimeKind.Utc);

    private RefreshToken NewToken(Guid? family = null, int? userId = null, DateTime? issuedAt = null, TimeSpan? lifetime = null) =>
        RefreshToken.Issue(userId ?? AdminId, RandomHash(), family ?? Guid.NewGuid(), issuedAt ?? IssuedAt, lifetime ?? TimeSpan.FromDays(7));

    private static string RandomHash() => Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()) + Convert.ToHexStringLower(Guid.NewGuid().ToByteArray());

    [Fact]
    public async Task A_token_round_trips_by_its_hash()
    {
        var token = NewToken();

        var id = await RefreshTokens.AddAsync(token, Ct);
        var loaded = await RefreshTokens.GetByHashAsync(token.TokenHash, Ct);

        id.ShouldBeGreaterThan(0);
        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(id);
        loaded.UserId.ShouldBe(AdminId);
        loaded.FamilyId.ShouldBe(token.FamilyId);
        loaded.CreatedAt.ShouldBe(IssuedAt);
        loaded.ExpiresAt.ShouldBe(IssuedAt.AddDays(7));
        loaded.ExpiresAt.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.RevokedAt.ShouldBeNull();
        loaded.IsActive(IssuedAt).ShouldBeTrue();
    }

    [Fact]
    public async Task An_unknown_hash_returns_null()
    {
        (await RefreshTokens.GetByHashAsync(RandomHash(), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Two_tokens_can_never_share_a_hash()
    {
        var token = NewToken();
        await RefreshTokens.AddAsync(token, Ct);
        var clone = RefreshToken.Issue(AdminId, token.TokenHash, Guid.NewGuid(), IssuedAt, TimeSpan.FromDays(7));

        (await Should.ThrowAsync<DuplicateEntityException>(() => RefreshTokens.AddAsync(clone, Ct))).Field.ShouldBe("TokenHash");
    }

    [Fact]
    public async Task A_token_must_belong_to_an_existing_user()
    {
        var orphan = NewToken(userId: 999_999);

        (await Should.ThrowAsync<SqlException>(() => RefreshTokens.AddAsync(orphan, Ct))).Number.ShouldBe(547);
    }

    [Fact]
    public async Task Revoke_persists_the_rotation_only_once()
    {
        var token = NewToken();
        await RefreshTokens.AddAsync(token, Ct);
        var replacement = RandomHash();
        token.Revoke(IssuedAt.AddMinutes(20), replacement);

        (await RefreshTokens.RevokeAsync(token, Ct)).ShouldBeTrue();
        (await RefreshTokens.RevokeAsync(token, Ct)).ShouldBeFalse(); // a parallel rotation loses

        var loaded = (await RefreshTokens.GetByHashAsync(token.TokenHash, Ct))!;
        loaded.RevokedAt.ShouldBe(IssuedAt.AddMinutes(20));
        loaded.ReplacedByHash.ShouldBe(replacement);
    }

    [Fact]
    public async Task Revoking_a_family_revokes_only_its_active_tokens()
    {
        var family = Guid.NewGuid();
        var rotated = NewToken(family);
        var current = NewToken(family);
        var otherFamily = NewToken();
        foreach (var token in new[] { rotated, current, otherFamily })
        {
            await RefreshTokens.AddAsync(token, Ct);
        }

        rotated.Revoke(IssuedAt.AddMinutes(1), current.TokenHash);
        await RefreshTokens.RevokeAsync(rotated, Ct);

        var revoked = await RefreshTokens.RevokeFamilyAsync(family, IssuedAt.AddMinutes(5), Ct);

        revoked.ShouldBe(1);
        (await RefreshTokens.GetByHashAsync(current.TokenHash, Ct))!.RevokedAt.ShouldBe(IssuedAt.AddMinutes(5));
        (await RefreshTokens.GetByHashAsync(rotated.TokenHash, Ct))!.RevokedAt.ShouldBe(IssuedAt.AddMinutes(1)); // unchanged
        (await RefreshTokens.GetByHashAsync(otherFamily.TokenHash, Ct))!.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Revoking_all_of_a_users_tokens_leaves_other_users_alone()
    {
        var userId = await Users.AddAsync(User.Register($"rt_{Guid.NewGuid():N}"[..15], $"{Guid.NewGuid():N}@heroregistry.com", "$2a$11$hash", IssuedAt), Ct);
        var mine = NewToken(userId: userId);
        var mineToo = NewToken(userId: userId);
        var admins = NewToken();
        foreach (var token in new[] { mine, mineToo, admins })
        {
            await RefreshTokens.AddAsync(token, Ct);
        }

        (await RefreshTokens.RevokeAllForUserAsync(userId, IssuedAt.AddHours(1), Ct)).ShouldBe(2);
        (await RefreshTokens.GetByHashAsync(admins.TokenHash, Ct))!.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Housekeeping_deletes_only_the_users_expired_tokens()
    {
        var expired = NewToken(issuedAt: IssuedAt.AddDays(-10), lifetime: TimeSpan.FromDays(7));
        var valid = NewToken();
        await RefreshTokens.AddAsync(expired, Ct);
        await RefreshTokens.AddAsync(valid, Ct);

        var deleted = await RefreshTokens.DeleteExpiredAsync(AdminId, IssuedAt, Ct);

        deleted.ShouldBeGreaterThanOrEqualTo(1);
        (await RefreshTokens.GetByHashAsync(expired.TokenHash, Ct)).ShouldBeNull();
        (await RefreshTokens.GetByHashAsync(valid.TokenHash, Ct)).ShouldNotBeNull();
    }

    [Fact]
    public async Task The_migration_creates_the_foreign_key_and_indexes()
    {
        (await Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM sys.foreign_keys WHERE name = 'FK_RefreshTokens_UserId'"))
            .ShouldBe(1);
        (await Database.ColumnAsync(
                "SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.RefreshTokens') AND name IS NOT NULL ORDER BY name"))
            .ShouldBe(["IX_RefreshTokens_FamilyId", "IX_RefreshTokens_UserId_RevokedAt", "PK_RefreshTokens", "UX_RefreshTokens_TokenHash"]);
    }
}
