using GnosisAuthServer.Data;

namespace GnosisAuthServer.Services;

public interface IJwtTokenService
{
    string CreateAccessToken(Account account);
    int GetAccessTokenLifetimeSeconds();
}
