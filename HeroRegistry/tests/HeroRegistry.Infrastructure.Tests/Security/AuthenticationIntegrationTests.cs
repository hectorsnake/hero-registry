using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Common;
using HeroRegistry.Application.Users;
using HeroRegistry.Infrastructure.Security;
using HeroRegistry.Infrastructure.Tests.Fixtures;
using HeroRegistry.Infrastructure.Tests.Persistence.Repositories;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;

namespace HeroRegistry.Infrastructure.Tests.Security;

/// <summary>
/// Registration and login end to end: AuthService with the real repository (SQL Server, seeded),
/// BCrypt hasher and JWT service.
/// </summary>
public class AuthenticationIntegrationTests(SqlServerContainerFixture sqlServer) : RepositoryTestBase(sqlServer)
{
    private AuthService CreateService() => new(
        Users,
        new BCryptPasswordHasher(),
        new JwtTokenService(
            Options.Create(new JwtOptions { Key = "integration-test-key-with-32-bytes-or-more" }),
            TimeProvider.System),
        RefreshTokens,
        Substitute.For<ICurrentUser>(),
        Options.Create(new RefreshTokenOptions()),
        TimeProvider.System,
        Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance);

    [Fact]
    public async Task The_seeded_administrator_can_log_in_with_Admin_Admin()
    {
        var result = await CreateService().LoginAsync(new LoginRequest { UserName = "Admin", Password = "Admin" }, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Response.User.IsAdmin.ShouldBeTrue();
        new JsonWebTokenHandler().ReadJsonWebToken(result.Value.Response.AccessToken).GetClaim("role").Value.ShouldBe("Admin");
    }

    [Fact]
    public async Task A_new_user_is_stored_with_a_BCrypt_hash_and_can_log_in()
    {
        var service = CreateService();

        var registered = await service.RegisterAsync(
            new RegisterUserRequest { UserName = "peter.parker", Email = "peter@dailybugle.com", Password = "Sp1der-Sense!" }, Ct);
        var login = await service.LoginAsync(new LoginRequest { UserName = "PETER.PARKER", Password = "Sp1der-Sense!" }, Ct);

        registered.IsSuccess.ShouldBeTrue();
        login.IsSuccess.ShouldBeTrue();
        login.Value.Response.User.IsAdmin.ShouldBeFalse();

        var stored = await Database.ScalarAsync<string>(
            "SELECT Password FROM dbo.Users WHERE UserName = @Name", ("@Name", "peter.parker"));
        stored.ShouldStartWith("$2a$11$");
        stored.ShouldNotContain("Sp1der-Sense!");
    }

    [Fact]
    public async Task Registering_an_existing_user_name_is_a_Conflict()
    {
        var result = await CreateService().RegisterAsync(
            new RegisterUserRequest { UserName = "admin", Email = "other@heroregistry.com", Password = "Sp1der-Sense!" }, Ct);

        result.Error!.Type.ShouldBe(ErrorType.Conflict);
        result.Error.FieldErrors.Keys.ShouldBe(["UserName"]);
    }

    [Theory]
    [InlineData("Admin", "wrong")]
    [InlineData("nobody", "Admin")]
    public async Task Bad_credentials_get_the_same_error(string userName, string password)
    {
        var result = await CreateService().LoginAsync(new LoginRequest { UserName = userName, Password = password }, Ct);

        result.Error.ShouldBe(Errors.InvalidCredentials());
    }

    [Fact]
    public async Task A_deleted_user_cannot_log_in()
    {
        var service = CreateService();
        await service.RegisterAsync(
            new RegisterUserRequest { UserName = "leaving", Email = "leaving@heroregistry.com", Password = "Sp1der-Sense!" }, Ct);
        var user = (await Users.GetByUserNameAsync("leaving", Ct))!;
        user.SoftDelete(AdminId, DateTime.UtcNow.AddMinutes(1));
        await Users.SoftDeleteAsync(user, Ct);

        var result = await service.LoginAsync(new LoginRequest { UserName = "leaving", Password = "Sp1der-Sense!" }, Ct);

        result.Error.ShouldBe(Errors.InvalidCredentials());
    }
}
