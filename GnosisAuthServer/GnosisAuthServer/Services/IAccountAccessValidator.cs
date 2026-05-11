namespace GnosisAuthServer.Services;

public interface IAccountAccessValidator
{
    Task<AccountAccessValidationResult> ValidateAsync(string steamId, CancellationToken cancellationToken);
}
