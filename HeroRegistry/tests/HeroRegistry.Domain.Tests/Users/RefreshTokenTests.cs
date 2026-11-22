using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.Users;

namespace HeroRegistry.Domain.Tests.Users;

public class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 15, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private static readonly Guid Family = Guid.NewGuid();
    private static readonly string Hash = new('a', 64);
    private static readonly string NextHash = new('b', 64);

    private static RefreshToken Issue() => RefreshToken.Issue(userId: 7, Hash, Family, Now, Lifetime);

    [Fact]
    public void Issue_creates_an_active_token_that_expires_after_its_lifetime()
    {
        var token = Issue();

        token.UserId.ShouldBe(7);
        token.TokenHash.ShouldBe(Hash);
        token.FamilyId.ShouldBe(Family);
        token.CreatedAt.ShouldBe(Now);
        token.ExpiresAt.ShouldBe(Now + Lifetime);
        token.RevokedAt.ShouldBeNull();
        token.ReplacedByHash.ShouldBeNull();
        token.IsActive(Now).ShouldBeTrue();
    }

    [Fact]
    public void A_token_is_no_longer_active_from_its_expiry()
    {
        var token = Issue();

        token.IsActive(Now + Lifetime - TimeSpan.FromSeconds(1)).ShouldBeTrue();
        token.IsActive(Now + Lifetime).ShouldBeFalse();
    }

    [Fact]
    public void Revoke_records_when_and_by_which_token_it_was_replaced()
    {
        var token = Issue();
        var later = Now.AddMinutes(20);

        token.Revoke(later, NextHash);

        token.RevokedAt.ShouldBe(later);
        token.ReplacedByHash.ShouldBe(NextHash);
        token.IsActive(later).ShouldBeFalse();
    }

    [Fact]
    public void Revoke_without_a_replacement_is_a_plain_revocation()
    {
        var token = Issue();

        token.Revoke(Now.AddMinutes(1));

        token.ReplacedByHash.ShouldBeNull();
        token.WasRotatedWithin(Now.AddMinutes(1), Grace).ShouldBeFalse();
    }

    [Fact]
    public void A_revoked_token_cannot_be_revoked_again()
    {
        var token = Issue();
        token.Revoke(Now.AddMinutes(1), NextHash);

        Should.Throw<DomainException>(() => token.Revoke(Now.AddMinutes(2), NextHash));
    }

    [Theory]
    [InlineData(29, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void WasRotatedWithin_covers_the_grace_window_boundaries(int secondsAfterRotation, bool expected)
    {
        var token = Issue();
        var rotatedAt = Now.AddMinutes(10);
        token.Revoke(rotatedAt, NextHash);

        token.WasRotatedWithin(rotatedAt.AddSeconds(secondsAfterRotation), Grace).ShouldBe(expected);
    }

    [Fact]
    public void An_active_token_was_not_rotated()
    {
        Issue().WasRotatedWithin(Now, Grace).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Issue_requires_a_valid_user(int userId)
    {
        Should.Throw<DomainException>(() => RefreshToken.Issue(userId, Hash, Family, Now, Lifetime)).Field.ShouldBe("UserId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // upper case
    public void Issue_requires_a_lowercase_hex_SHA256_hash(string hash)
    {
        Should.Throw<DomainException>(() => RefreshToken.Issue(7, hash, Family, Now, Lifetime)).Field.ShouldBe("TokenHash");
    }

    [Fact]
    public void Issue_requires_a_family_a_UTC_time_and_a_positive_lifetime()
    {
        Should.Throw<DomainException>(() => RefreshToken.Issue(7, Hash, Guid.Empty, Now, Lifetime)).Field.ShouldBe("FamilyId");
        Should.Throw<DomainException>(() => RefreshToken.Issue(7, Hash, Family, DateTime.SpecifyKind(Now, DateTimeKind.Local), Lifetime));
        Should.Throw<DomainException>(() => RefreshToken.Issue(7, Hash, Family, Now, TimeSpan.Zero)).Field.ShouldBe("ExpiresAt");
    }
}
