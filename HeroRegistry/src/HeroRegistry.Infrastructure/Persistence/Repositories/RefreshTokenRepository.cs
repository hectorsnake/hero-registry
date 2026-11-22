using System.Data;
using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Domain.Users;
using HeroRegistry.Infrastructure.Persistence.Diagnostics;
using HeroRegistry.Infrastructure.Persistence.Maps;
using HeroRegistry.Infrastructure.Persistence.Sql;

namespace HeroRegistry.Infrastructure.Persistence.Repositories;

/// <summary>SQL Server implementation of <see cref="IRefreshTokenRepository"/>, on the Reflection mapper (T05).</summary>
public sealed class RefreshTokenRepository(IDbConnectionFactory connections, DataAccessInstrumentation instrumentation)
    : SqlRepository<RefreshToken>(connections, instrumentation, new RefreshTokenMap()), IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        var tokens = await QueryAsync(
            $"{SelectFromTable} WHERE [TokenHash] = @TokenHash",
            [Parameter(nameof(RefreshToken.TokenHash), tokenHash)],
            cancellationToken);

        return tokens.Count == 0 ? null : tokens[0];
    }

    public Task<int> AddAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        InsertAsync(token, cancellationToken);

    /// <summary>
    /// Writes the revocation only while the stored row is still active, so of two requests rotating the
    /// same token at the same instant exactly one wins.
    /// </summary>
    public async Task<bool> RevokeAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (token.RevokedAt is null)
        {
            throw new ArgumentException("Call token.Revoke(...) before persisting the revocation.", nameof(token));
        }

        var rows = await ExecuteAsync(
            $"UPDATE {Metadata.QualifiedTableName} SET [RevokedAt] = @RevokedAt, [ReplacedByHash] = @ReplacedByHash " +
            "WHERE [Id] = @Id AND [RevokedAt] IS NULL",
            [
                Parameter(nameof(RefreshToken.RevokedAt), token.RevokedAt),
                Parameter(nameof(RefreshToken.ReplacedByHash), token.ReplacedByHash),
                Parameter(nameof(RefreshToken.Id), token.Id),
            ],
            DataOperation.Update,
            cancellationToken);

        return rows == 1;
    }

    public Task<int> RevokeFamilyAsync(Guid familyId, DateTime utcNow, CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            $"UPDATE {Metadata.QualifiedTableName} SET [RevokedAt] = @Now WHERE [FamilyId] = @FamilyId AND [RevokedAt] IS NULL",
            [
                Parameter(nameof(RefreshToken.RevokedAt), utcNow, "@Now"),
                Parameter(nameof(RefreshToken.FamilyId), familyId),
            ],
            DataOperation.Update,
            cancellationToken);

    public Task<int> RevokeAllForUserAsync(int userId, DateTime utcNow, CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            $"UPDATE {Metadata.QualifiedTableName} SET [RevokedAt] = @Now WHERE [UserId] = @UserId AND [RevokedAt] IS NULL",
            [
                Parameter(nameof(RefreshToken.RevokedAt), utcNow, "@Now"),
                Parameter(nameof(RefreshToken.UserId), userId),
            ],
            DataOperation.Update,
            cancellationToken);

    public Task<int> DeleteExpiredAsync(int userId, DateTime utcNow, CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            $"DELETE FROM {Metadata.QualifiedTableName} WHERE [UserId] = @UserId AND [ExpiresAt] <= @Now",
            [
                Parameter(nameof(RefreshToken.UserId), userId),
                new SqlParameterValue("@Now", utcNow, DbType.DateTime2, 0),
            ],
            DataOperation.Delete,
            cancellationToken);
}
