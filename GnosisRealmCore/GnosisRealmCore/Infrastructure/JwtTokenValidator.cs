using GnosisRealmCore.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace GnosisRealmCore.Infrastructure;

public sealed class JwtTokenValidator : IJwtTokenValidator
{
    private readonly JwtValidationOptions _options;
    private readonly RsaSecurityKey _securityKey;

    public JwtTokenValidator(IOptions<JwtValidationOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.PublicKeyPemPath) || !File.Exists(_options.PublicKeyPemPath))
        {
            throw new InvalidOperationException($"JWT public key file was not found: {_options.PublicKeyPemPath}");
        }

        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(_options.PublicKeyPemPath));
        _securityKey = new RsaSecurityKey(rsa);
    }

    public bool TryValidate(HttpRequest request, out ClaimsPrincipal? principal, out string error)
    {
        principal = null;
        error = string.Empty;

        var authorization = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            error = "Missing bearer token.";
            return false;
        }

        var token = authorization["Bearer ".Length..].Trim();

        var handler = new JwtSecurityTokenHandler();
        try
        {
            principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _securityKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(_options.ClockSkewSeconds)
            }, out _);

            return true;
        }
        catch (Exception ex)
        {
            error = $"Token validation failed: {ex.Message}";
            return false;
        }
    }
}
