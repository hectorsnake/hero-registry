using HeroRegistry.Application.Users;
using HeroRegistry.Domain.Users;

namespace HeroRegistry.Application.Tests.Users;

internal static class UserTestData
{
    public const int AdminId = 1;
    public const int AgentId = 2;
    public const string StrongPassword = "Sp1der-Sense!";
    public const string StoredHash = "$2a$11$stored.hash.for.tests.only.xxxxxxxxxxxxxxxxxxxxxxxxxxx";

    public static readonly DateTimeOffset Now = new(2026, 11, 22, 15, 0, 0, TimeSpan.Zero);

    public static RegisterUserRequest ValidRegistration() => new()
    {
        UserName = "peter.parker",
        Email = "peter@dailybugle.com",
        Password = StrongPassword,
    };

    /// <summary>A stored user as the repository would return it (Id assigned).</summary>
    public static User StoredUser(int id = AgentId, string userName = "peter.parker", bool isAdmin = false)
    {
        var user = User.Register(userName, $"{userName}@heroregistry.com", StoredHash, Now.UtcDateTime.AddDays(-10));
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        if (isAdmin)
        {
            typeof(User).GetProperty(nameof(User.IsAdmin))!.SetValue(user, true);
        }

        return user;
    }
}
