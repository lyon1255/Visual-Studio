namespace GnosisAuthServer.Services;

public sealed class AccountAccessValidationResult
{
    private AccountAccessValidationResult(bool isAllowed, string? denialReason)
    {
        IsAllowed = isAllowed;
        DenialReason = denialReason;
    }

    public bool IsAllowed { get; }

    public string? DenialReason { get; }

    public static AccountAccessValidationResult Allowed()
        => new(true, null);

    public static AccountAccessValidationResult Denied(string reason)
        => new(false, reason);
}
