using System.Text;
using HeroRegistry.Domain.Users;
using HeroRegistry.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HeroRegistry.Infrastructure.Tests.Security;

public class JwtTokenServiceTests
{
    private const string Key = "test-signing-key-with-at-least-32-bytes!!";

    private static readonly DateTimeOffset Now = new(2026, 11, 22, 15, 0, 0, TimeSpan.Zero);

    private static readonly JwtOptions Options = new()
    {
        Issuer = "hero-registry-api",
        Audience = "hero-registry-ui",
        Key = Key,
        ExpirationMinutes = 45,
    };

    private readonly JwtTokenService _service = new(Microsoft.Extensions.Options.Options.Create(Options), new FakeTimeProvider(Now));

    [Fact]
    public void The_token_identifies_the_user_by_id_name_and_role()
    {
        var token = Read(_service.CreateToken(UserWithId(7, "peter.parker")).Token);

        token.GetClaim("sub").Value.ShouldBe("7");
        token.GetClaim("unique_name").Value.ShouldBe("peter.parker");
        token.GetClaim("role").Value.ShouldBe("User");
        token.TryGetClaim("jti", out var jti).ShouldBeTrue();
        jti.Value.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void An_administrator_gets_the_Admin_role()
    {
        Read(_service.CreateToken(UserWithId(1, "Admin", isAdmin: true)).Token).GetClaim("role").Value.ShouldBe("Admin");
    }

    [Fact]
    public void The_token_carries_issuer_audience_and_its_lifetime()
    {
        var access = _service.CreateToken(UserWithId(7, "peter.parker"));
        var token = Read(access.Token);

        token.Issuer.ShouldBe("hero-registry-api");
        token.Audiences.ShouldBe(["hero-registry-ui"]);
        token.IssuedAt.ShouldBe(Now.UtcDateTime);
        token.ValidTo.ShouldBe(Now.UtcDateTime.AddMinutes(45));
        access.ExpiresAt.ShouldBe(Now.UtcDateTime.AddMinutes(45));
        access.ExpiresAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void The_token_never_contains_the_password_hash_or_email()
    {
        var user = UserWithId(7, "peter.parker");

        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(_service.CreateToken(user).Token.Split('.')[1]));

        payload.ShouldNotContain(user.Password);
        payload.ShouldNotContain(user.Email);
    }

    [Fact]
    public async Task The_token_is_signed_with_HMAC_SHA256_and_validates_with_the_same_key()
    {
        var token = _service.CreateToken(UserWithId(7, "peter.parker")).Token;

        Read(token).Alg.ShouldBe(SecurityAlgorithms.HmacSha256);
        (await ValidateAsync(token, Key)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_rejected()
    {
        var token = _service.CreateToken(UserWithId(7, "peter.parker")).Token;

        (await ValidateAsync(token, "a-completely-different-key-of-32-bytes!!")).IsValid.ShouldBeFalse();
    }

    private static JsonWebToken Read(string token) => new JsonWebTokenHandler().ReadJsonWebToken(token);

    private static Task<TokenValidationResult> ValidateAsync(string token, string key) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "hero-registry-api",
            ValidAudience = "hero-registry-ui",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = false, // the fake clock is in the past
        });

    private static User UserWithId(int id, string userName, bool isAdmin = false)
    {
        var user = User.Register(userName, $"{userName}@heroregistry.com", "$2a$11$secret-hash", Now.UtcDateTime);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        if (isAdmin)
        {
            typeof(User).GetProperty(nameof(User.IsAdmin))!.SetValue(user, true);
        }

        return user;
    }
}
