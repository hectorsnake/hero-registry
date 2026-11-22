using HeroRegistry.Application.Common;
using HeroRegistry.Domain.Users;
using HeroRegistry.Infrastructure.Tests.Fixtures;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Repositories;

/// <summary>UserRepository against a real SQL Server (Testcontainers), starting from the seeded Admin.</summary>
public class UserRepositoryTests(SqlServerContainerFixture sqlServer) : RepositoryTestBase(sqlServer)
{
    private const string Hash = "$2a$11$rnYVgixh.98DnFhT80CktedjRbl0EMfnBjZppegPZdRKmMXDk2uTq";

    private User NewUser(string userName, string? email = null) =>
        User.Register(userName, email ?? $"{userName}@heroregistry.com", Hash, Now);

    [Fact]
    public async Task Add_get_list_and_soft_delete_round_trip()
    {
        var user = NewUser("peter.parker");

        var id = await Users.AddAsync(user, Ct);

        (await Users.GetByIdAsync(id, Ct))!.UserName.ShouldBe("peter.parker");
        (await Users.ListAsync(Ct)).Select(u => u.UserName).ShouldBe(["Admin", "peter.parker"]);

        user.SoftDelete(AdminId, Now.AddHours(1));
        (await Users.SoftDeleteAsync(user, Ct)).ShouldBeTrue();
        (await Users.GetByIdAsync(id, Ct)).ShouldBeNull();
        (await Users.ListAsync(Ct)).Select(u => u.UserName).ShouldBe(["Admin"]);
    }

    // Login lookup

    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData(" ADMIN ")]
    public async Task GetByUserName_finds_the_user_ignoring_case_and_surrounding_spaces(string userName)
    {
        var user = await Users.GetByUserNameAsync(userName, Ct);

        user.ShouldNotBeNull();
        user.Id.ShouldBe(AdminId);
        user.IsAdmin.ShouldBeTrue();
        user.Password.ShouldStartWith("$2a$11$");
    }

    [Theory]
    [InlineData("nobody")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByUserName_returns_null_for_unknown_or_blank_names(string userName)
    {
        (await Users.GetByUserNameAsync(userName, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task A_deleted_user_cannot_be_found_by_user_name()
    {
        var user = NewUser("leaving");
        await Users.AddAsync(user, Ct);
        user.SoftDelete(AdminId, Now.AddHours(1));
        await Users.SoftDeleteAsync(user, Ct);

        (await Users.GetByUserNameAsync("leaving", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task GetByUserName_treats_the_name_as_data()
    {
        (await Users.GetByUserNameAsync("' OR '1'='1", Ct)).ShouldBeNull();
    }

    // Uniqueness checks

    [Fact]
    public async Task ExistsByUserName_ignores_case_the_excluded_user_and_deleted_users()
    {
        (await Users.ExistsByUserNameAsync("ADMIN", excludeId: null, Ct)).ShouldBeTrue();
        (await Users.ExistsByUserNameAsync("admin", excludeId: AdminId, Ct)).ShouldBeFalse();
        (await Users.ExistsByUserNameAsync("nobody", excludeId: null, Ct)).ShouldBeFalse();

        var user = NewUser("gone");
        await Users.AddAsync(user, Ct);
        user.SoftDelete(AdminId, Now.AddHours(1));
        await Users.SoftDeleteAsync(user, Ct);
        (await Users.ExistsByUserNameAsync("gone", excludeId: null, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByEmail_ignores_case_and_the_excluded_user()
    {
        (await Users.ExistsByEmailAsync("Admin@HeroRegistry.com", excludeId: null, Ct)).ShouldBeTrue();
        (await Users.ExistsByEmailAsync("admin@heroregistry.com", excludeId: AdminId, Ct)).ShouldBeFalse();
    }

    // Unique violations

    [Fact]
    public async Task Adding_a_duplicate_user_name_throws_DuplicateEntityException_naming_the_field()
    {
        var exception = await Should.ThrowAsync<DuplicateEntityException>(
            () => Users.AddAsync(NewUser("ADMIN", "another@heroregistry.com"), Ct));

        exception.EntityName.ShouldBe("User");
        exception.Field.ShouldBe("UserName");
        exception.Message.ShouldNotContain("ADMIN");
    }

    [Fact]
    public async Task Adding_a_duplicate_email_names_the_Email_field()
    {
        var exception = await Should.ThrowAsync<DuplicateEntityException>(
            () => Users.AddAsync(NewUser("someone", "admin@heroregistry.com"), Ct));

        exception.Field.ShouldBe("Email");
        exception.Message.ShouldNotContain("admin@heroregistry.com");
    }
}
