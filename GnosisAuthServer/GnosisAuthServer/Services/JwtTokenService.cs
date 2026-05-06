using GnosisAuthServer.Data;
using GnosisAuthServer.Options;
using GnosisAuthServer.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GnosisAuthServer.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly IRsaKeyProvider _rsaKeyProvider;

    public JwtTokenService(IOptions<JwtOptions> options, IRsaKeyProvider rsaKeyProvider)
    {
        _options = options.Value;
        _rsaKeyProvider = rsaKeyProvider;
    }

    public string CreateAccessToken(Account account)
    {
        var handler = new JwtSecurityTokenHandler();
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, account.SteamId),
                new Claim(ClaimTypes.NameIdentifier, account.SteamId),
                new Claim("account_id", account.Id.ToString()),
                new Claim("account_type", account.AccountType),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            }),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(_rsaKeyProvider.GetSigningKey(), SecurityAlgorithms.RsaSha256)
        };

        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    public int GetAccessTokenLifetimeSeconds() => _options.AccessTokenMinutes * 60;
}
