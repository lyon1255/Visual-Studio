namespace GnosisAuthServer.Services;

public interface ISteamTicketValidator
{
    Task<SteamTicketValidationResult> ValidateAsync(string steamId, string ticket, CancellationToken cancellationToken);
}

public sealed record SteamTicketValidationResult(bool IsValid, string? Error = null);
