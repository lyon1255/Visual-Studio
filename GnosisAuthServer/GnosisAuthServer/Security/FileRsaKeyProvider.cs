using GnosisAuthServer.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace GnosisAuthServer.Security;

public sealed class FileRsaKeyProvider : IRsaKeyProvider, IDisposable
{
    private readonly RSA _privateRsa;
    private readonly RSA _publicRsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly RsaSecurityKey _validationKey;

    public FileRsaKeyProvider(IOptions<JwtOptions> options)
    {
        var jwtOptions = options.Value;
        if (!File.Exists(jwtOptions.PrivateKeyPemPath))
        {
            throw new InvalidOperationException($"JWT private key file was not found: {jwtOptions.PrivateKeyPemPath}");
        }

        if (!File.Exists(jwtOptions.PublicKeyPemPath))
        {
            throw new InvalidOperationException($"JWT public key file was not found: {jwtOptions.PublicKeyPemPath}");
        }

        _privateRsa = RSA.Create();
        _privateRsa.ImportFromPem(File.ReadAllText(jwtOptions.PrivateKeyPemPath));

        _publicRsa = RSA.Create();
        _publicRsa.ImportFromPem(File.ReadAllText(jwtOptions.PublicKeyPemPath));

        _signingKey = new RsaSecurityKey(_privateRsa)
        {
            KeyId = jwtOptions.KeyId
        };

        _validationKey = new RsaSecurityKey(_publicRsa)
        {
            KeyId = jwtOptions.KeyId
        };
    }

    public SecurityKey GetSigningKey() => _signingKey;
    public SecurityKey GetValidationKey() => _validationKey;

    public void Dispose()
    {
        _privateRsa.Dispose();
        _publicRsa.Dispose();
    }
}
