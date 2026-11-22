using System.Security.Claims;
using HeroRegistry.Api.Security;
using HeroRegistry.Application.Abstractions;
using HeroRegistry.Application.Abstractions.Security;
using HeroRegistry.Application.Heroes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace HeroRegistry.Api.Tests.Security;

/// <summary>
/// The API's <see cref="ICurrentUser"/>: who is calling, read from the request's claims (the JWT, T09).
/// </summary>
public class HttpCurrentUserTests
{
    [Theory]
    [InlineData("sub")]                   // JWT standard claim
    [InlineData(ClaimTypes.NameIdentifier)] // ASP.NET Core's mapped name for "sub"
    public void Reads_the_user_id_from_the_subject_claim(string claimType)
    {
        var user = CurrentUser(authenticated: true, new Claim(claimType, "42"));

        user.UserId.ShouldBe(42);
    }

    [Fact]
    public void An_admin_role_claim_makes_the_user_an_administrator()
    {
        var user = CurrentUser(authenticated: true, new Claim("sub", "1"), new Claim(ClaimTypes.Role, Roles.Admin));

        user.IsAdmin.ShouldBeTrue();
    }

    [Fact]
    public void A_signed_in_user_without_the_admin_role_is_not_an_administrator()
    {
        var user = CurrentUser(authenticated: true, new Claim("sub", "2"), new Claim(ClaimTypes.Role, Roles.User));

        user.UserId.ShouldBe(2);
        user.IsAdmin.ShouldBeFalse();
    }

    [Fact]
    public void An_anonymous_request_has_no_user_even_if_it_carries_claims()
    {
        // Claims on an unauthenticated identity were never validated: ignore them.
        var user = CurrentUser(authenticated: false, new Claim("sub", "1"), new Claim(ClaimTypes.Role, Roles.Admin));

        user.UserId.ShouldBeNull();
        user.IsAdmin.ShouldBeFalse();
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-3")]
    public void An_invalid_subject_claim_means_no_user(string subject)
    {
        var user = CurrentUser(authenticated: true, new Claim("sub", subject), new Claim(ClaimTypes.Role, Roles.Admin));

        user.UserId.ShouldBeNull();
        user.IsAdmin.ShouldBeFalse();
    }

    [Fact]
    public void Outside_a_request_there_is_no_user()
    {
        var user = new HttpCurrentUser(new HttpContextAccessor());

        user.UserId.ShouldBeNull();
        user.IsAdmin.ShouldBeFalse();
    }

    [Fact]
    public void The_API_registers_it_so_the_hero_use_cases_can_be_resolved()
    {
        using var factory = new HeroRegistryApiFactory();
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ICurrentUser>().ShouldBeOfType<HttpCurrentUser>();
        scope.ServiceProvider.GetRequiredService<IHeroService>().ShouldBeOfType<HeroService>();
    }

    private static HttpCurrentUser CurrentUser(bool authenticated, params Claim[] claims)
    {
        var identity = authenticated ? new ClaimsIdentity(claims, authenticationType: "Bearer") : new ClaimsIdentity(claims);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new HttpCurrentUser(new HttpContextAccessor { HttpContext = context });
    }
}
