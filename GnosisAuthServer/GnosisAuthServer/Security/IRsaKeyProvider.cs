using Microsoft.IdentityModel.Tokens;

namespace GnosisAuthServer.Security;

public interface IRsaKeyProvider
{
    SecurityKey GetSigningKey();
    SecurityKey GetValidationKey();
}
