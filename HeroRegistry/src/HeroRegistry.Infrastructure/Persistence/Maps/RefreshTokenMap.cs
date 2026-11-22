using HeroRegistry.Domain.Users;
using HeroRegistry.Infrastructure.Persistence.Mapping;

namespace HeroRegistry.Infrastructure.Persistence.Maps;

/// <summary>Maps <see cref="RefreshToken"/> to <c>dbo.RefreshTokens</c> (0004_CreateRefreshTokens.sql).</summary>
internal sealed class RefreshTokenMap : EntityMap<RefreshToken>
{
    public RefreshTokenMap()
    {
        ToTable("RefreshTokens");
        HasKey(t => t.Id);
        Property(t => t.TokenHash).IsAnsi().IsFixedLength().HasMaxLength(64);
        Property(t => t.ReplacedByHash).IsAnsi().IsFixedLength().HasMaxLength(64);
        Property(t => t.UserId).IsInsertOnly();
        Property(t => t.FamilyId).IsInsertOnly();
        Property(t => t.CreatedAt).IsInsertOnly();
    }
}
