using HeroRegistry.Domain.Users;
using HeroRegistry.Infrastructure.Persistence.Mapping;

namespace HeroRegistry.Infrastructure.Persistence.Maps;

/// <summary>Maps <see cref="User"/> to <c>dbo.Users</c> (0001_CreateUsers.sql).</summary>
internal sealed class UserMap : AuditableEntityMap<User>
{
    public UserMap()
    {
        ToTable("Users");
        Property(u => u.UserName).HasMaxLength(UserRules.UserNameMaxLength);
        Property(u => u.Email).HasMaxLength(Domain.ValueObjects.EmailAddress.MaxLength);
        Property(u => u.Password).HasMaxLength(UserRules.PasswordHashMaxLength);
    }
}
