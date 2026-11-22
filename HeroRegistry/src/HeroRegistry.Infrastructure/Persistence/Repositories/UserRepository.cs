using HeroRegistry.Application.Abstractions.Persistence;
using HeroRegistry.Domain.Users;
using HeroRegistry.Infrastructure.Persistence.Diagnostics;
using HeroRegistry.Infrastructure.Persistence.Maps;
using HeroRegistry.Infrastructure.Persistence.Sql;

namespace HeroRegistry.Infrastructure.Persistence.Repositories;

/// <summary>SQL Server implementation of <see cref="IUserRepository"/>, on the Reflection mapper (T05).</summary>
public sealed class UserRepository(IDbConnectionFactory connections, DataAccessInstrumentation instrumentation)
    : SqlRepository<User>(connections, instrumentation, new UserMap()), IUserRepository
{
    public Task<int> AddAsync(User user, CancellationToken cancellationToken = default) =>
        InsertAsync(user, cancellationToken);

    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userName);

        var name = userName.Trim();
        if (name.Length == 0)
        {
            return null;
        }

        // UserName has a case-insensitive collation, so "admin" finds "Admin".
        var users = await QueryAsync(
            $"{SelectFromTable} WHERE [IsDeleted] = 0 AND [UserName] = @UserName",
            [Parameter(nameof(User.UserName), name)],
            cancellationToken);

        return users.Count == 0 ? null : users[0];
    }

    public Task<bool> ExistsByUserNameAsync(string userName, int? excludeId, CancellationToken cancellationToken = default) =>
        ExistsActiveAsync(nameof(User.UserName), userName.Trim(), excludeId, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, int? excludeId, CancellationToken cancellationToken = default) =>
        ExistsActiveAsync(nameof(User.Email), email.Trim(), excludeId, cancellationToken);
}
