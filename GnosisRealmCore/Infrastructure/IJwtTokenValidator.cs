using System.Security.Claims;

namespace GnosisRealmCore.Infrastructure;

public interface IJwtTokenValidator
{
    bool TryValidate(HttpRequest request, out ClaimsPrincipal? principal, out string error);
}
