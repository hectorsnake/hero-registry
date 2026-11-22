using System.Globalization;
using System.Text;
using HeroRegistry.Application.Abstractions.Security;
using HeroRegistry.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HeroRegistry.Infrastructure.Security;

/// <summary>
/// Issues HMAC-SHA256 signed JWTs. Claims: <c>sub</c> (user id), <c>unique_name</c> (user name),
/// <c>role</c> (<see cref="Roles.Admin"/> or <see cref="Roles.User"/>) and <c>jti</c> (token id).
/// No personal data (email) or secret (password hash) goes into the token, whose payload is only encoded.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private static readonly JsonWebTokenHandler Handler = new();

    public AccessToken CreateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(settings.ExpirationMinutes);

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(CultureInfo.InvariantCulture),
                [JwtRegisteredClaimNames.UniqueName] = user.UserName,
                ["role"] = user.IsAdmin ? Roles.Admin : Roles.User,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expiresAt);
    }
}
