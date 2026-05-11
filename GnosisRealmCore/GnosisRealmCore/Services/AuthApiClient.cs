using GnosisRealmCore.Infrastructure;
using GnosisRealmCore.Models;
using GnosisRealmCore.Options;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GnosisRealmCore.Services;

public sealed class AuthApiClient : IAuthApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthApiOptions _options;

    public AuthApiClient(IHttpClientFactory httpClientFactory, IOptions<AuthApiOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public Task<GlobalGameDataVersionResponse?> GetGlobalGameDataVersionAsync(CancellationToken cancellationToken)
        => SendAuthorizedAsync<GlobalGameDataVersionResponse>(HttpMethod.Get, "/api/internal/gamedata/version", null, cancellationToken);

    public Task<GlobalGameDataSnapshotResponse?> GetGlobalGameDataSnapshotAsync(CancellationToken cancellationToken)
        => SendAuthorizedAsync<GlobalGameDataSnapshotResponse>(HttpMethod.Get, "/api/internal/gamedata/snapshot", null, cancellationToken);

    public Task<SchemaManifestResponse?> GetSchemaManifestAsync(CancellationToken cancellationToken)
        => SendAuthorizedAsync<SchemaManifestResponse>(HttpMethod.Get, "/api/internal/schema/manifest", null, cancellationToken);

    public Task<SchemaMigrationContentResponse?> GetSchemaMigrationAsync(string migrationId, CancellationToken cancellationToken)
        => SendAuthorizedAsync<SchemaMigrationContentResponse>(
            HttpMethod.Get,
            $"/api/internal/schema/migrations/{Uri.EscapeDataString(migrationId)}",
            null,
            cancellationToken);

    public async Task SendRealmHeartbeatAsync(RealmHeartbeatRequest request, CancellationToken cancellationToken)
    {
        _ = await SendAuthorizedAsync<object>(
            HttpMethod.Post,
            "/api/internal/realms/heartbeat",
            request,
            cancellationToken);
    }

    private async Task<T?> SendAuthorizedAsync<T>(HttpMethod method, string relativePath, object? body, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(AuthApiClient));
        client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/'));

        var bodyJson = body is null ? string.Empty : JsonSerializer.Serialize(body, JsonOptions);
        var bodyHashHex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bodyJson))).ToLowerInvariant();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");

        using var requestMessage = new HttpRequestMessage(method, relativePath);
        var requestPath = requestMessage.RequestUri?.AbsolutePath ?? relativePath;
        var requestQuery = requestMessage.RequestUri?.Query ?? string.Empty;

        var canonical = string.Join("\n",
            method.Method.ToUpperInvariant(),
            requestPath,
            requestQuery,
            timestamp,
            nonce,
            bodyHashHex);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ServiceSecret));
        var signatureHex = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        requestMessage.Headers.Add(ServiceAuthHeaderNames.ServiceId, _options.ServiceId);
        requestMessage.Headers.Add(ServiceAuthHeaderNames.Timestamp, timestamp);
        requestMessage.Headers.Add(ServiceAuthHeaderNames.Nonce, nonce);
        requestMessage.Headers.Add(ServiceAuthHeaderNames.Signature, signatureHex);
        requestMessage.Headers.Add(ServiceAuthHeaderNames.BodySha256, bodyHashHex);

        if (!string.IsNullOrEmpty(bodyJson))
        {
            requestMessage.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(requestMessage, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"AuthApi request failed ({(int)response.StatusCode}): {responseText}");
        }

        if (typeof(T) == typeof(object) || string.IsNullOrWhiteSpace(responseText))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(responseText, JsonOptions);
    }
}
