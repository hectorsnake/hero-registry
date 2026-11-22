using System.Globalization;
using System.Security.Cryptography;
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
/// Also issues the opaque refresh tokens (T09.1): 256 random bits, stored only as a SHA-256 hash.
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

    /// <summary>
    /// 32 bytes from the OS's cryptographic random generator, Base64Url-encoded (43 characters, cookie-safe).
    /// Not a JWT: it carries no data, so it is only meaningful to this API's database.
    /// </summary>
    public NewRefreshToken CreateRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return new NewRefreshToken(token, HashRefreshToken(token));
    }

    /// <summary>
    /// SHA-256, lower-case hex. A fast hash is safe here (unlike for passwords) because the input is 256 bits
    /// of randomness, and it lets the API find the token through an indexed, unique column.
    /// </summary>
    public string HashRefreshToken(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
