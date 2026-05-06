using GnosisAuthServer.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace GnosisAuthServer.Services;

public sealed class SteamTicketValidator : ISteamTicketValidator
{
    private readonly HttpClient _httpClient;
    private readonly IHostEnvironment _environment;
    private readonly SteamOptions _options;
    private readonly ILogger<SteamTicketValidator> _logger;

    public SteamTicketValidator(
        HttpClient httpClient,
        IHostEnvironment environment,
        IOptions<SteamOptions> options,
        ILogger<SteamTicketValidator> logger)
    {
        _httpClient = httpClient;
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SteamTicketValidationResult> ValidateAsync(string steamId, string ticket, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(steamId) || string.IsNullOrWhiteSpace(ticket))
        {
            return new SteamTicketValidationResult(false, "SteamId or ticket is missing.");
        }

        if (!_options.Enabled)
        {
            return new SteamTicketValidationResult(false, "Steam validation is disabled.");
        }

        if (_environment.IsDevelopment() && _options.AllowMockTicketsInDevelopment && ticket == "DEV_BYPASS")
        {
            _logger.LogWarning("Development bypass ticket used for SteamId {SteamId}.", steamId);
            return new SteamTicketValidationResult(true);
        }

        if (_options.AppId == 0 || string.IsNullOrWhiteSpace(_options.PublisherKey))
        {
            _logger.LogError("Steam validation is enabled, but AppId or PublisherKey is missing.");
            return new SteamTicketValidationResult(false, "Steam validation is not configured.");
        }

        var url = $"https://partner.steam-api.com/ISteamUserAuth/AuthenticateUserTicket/v1/?key={Uri.EscapeDataString(_options.PublisherKey)}&appid={_options.AppId}&ticket={Uri.EscapeDataString(ticket)}";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Steam API returned status {StatusCode} for SteamId {SteamId}.", response.StatusCode, steamId);
            return new SteamTicketValidationResult(false, "Steam validation failed.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("response", out var responseNode) ||
            !responseNode.TryGetProperty("params", out var paramsNode))
        {
            return new SteamTicketValidationResult(false, "Steam response is malformed.");
        }

        var ownerSteamId = paramsNode.TryGetProperty("steamid", out var steamIdNode)
            ? steamIdNode.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(ownerSteamId) || !string.Equals(ownerSteamId, steamId, StringComparison.Ordinal))
        {
            return new SteamTicketValidationResult(false, "Steam ticket owner mismatch.");
        }

        return new SteamTicketValidationResult(true);
    }
}
