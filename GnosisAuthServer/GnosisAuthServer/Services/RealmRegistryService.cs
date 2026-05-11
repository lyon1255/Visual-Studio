using Gnosis.AuthServer.Domain.Entities;
using GnosisAuthServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gnosis.AuthServer.Application.Services
{
    public sealed class RealmRegistryService
    {
        private readonly AuthDbContext _dbContext;
        private readonly ILogger<RealmRegistryService> _logger;

        public RealmRegistryService(AuthDbContext dbContext, ILogger<RealmRegistryService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<RealmRegistrationResult> RegisterOrUpdateAsync(
            RealmRegistrationRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateRegistrationRequest(request);

            string normalizedPublicUrl = NormalizeBaseUrl(request.PublicBaseUrl);
            string? normalizedInternalUrl = string.IsNullOrWhiteSpace(request.InternalBaseUrl)
                ? null
                : NormalizeBaseUrl(request.InternalBaseUrl);

            RealmNode? existingRealm = await _dbContext.RealmNodes
                .SingleOrDefaultAsync(x => x.RealmId == request.RealmId, cancellationToken);

            if (existingRealm is null)
            {
                var newRealm = new RealmNode
                {
                    RealmId = request.RealmId.Trim(),
                    Name = request.Name.Trim(),
                    Region = request.Region.Trim(),
                    PublicBaseUrl = normalizedPublicUrl,
                    InternalBaseUrl = normalizedInternalUrl,
                    ServiceSecretHash = HashSecret(request.ServiceSecret),
                    Enabled = true,
                    Status = "offline",
                    CurrentPlayers = 0,
                    MaxPlayers = request.MaxPlayers,
                    BuildVersion = request.BuildVersion?.Trim(),
                    ProtocolVersion = request.ProtocolVersion,
                    LastHeartbeatUtc = null,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                _dbContext.RealmNodes.Add(newRealm);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Registered new realm. RealmId={RealmId}, Region={Region}, PublicBaseUrl={PublicBaseUrl}",
                    newRealm.RealmId,
                    newRealm.Region,
                    newRealm.PublicBaseUrl);

                return new RealmRegistrationResult(
                    newRealm.RealmId,
                    Created: true,
                    Enabled: newRealm.Enabled,
                    Status: newRealm.Status);
            }

            existingRealm.Name = request.Name.Trim();
            existingRealm.Region = request.Region.Trim();
            existingRealm.PublicBaseUrl = normalizedPublicUrl;
            existingRealm.InternalBaseUrl = normalizedInternalUrl;
            existingRealm.MaxPlayers = request.MaxPlayers;
            existingRealm.BuildVersion = request.BuildVersion?.Trim();
            existingRealm.ProtocolVersion = request.ProtocolVersion;
            existingRealm.UpdatedAtUtc = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.ServiceSecret))
            {
                existingRealm.ServiceSecretHash = HashSecret(request.ServiceSecret);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Updated realm registration. RealmId={RealmId}, Enabled={Enabled}, Status={Status}",
                existingRealm.RealmId,
                existingRealm.Enabled,
                existingRealm.Status);

            return new RealmRegistrationResult(
                existingRealm.RealmId,
                Created: false,
                Enabled: existingRealm.Enabled,
                Status: existingRealm.Status);
        }

        public async Task<bool> VerifyServiceSecretAsync(
            string realmId,
            string providedSecret,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(realmId) || string.IsNullOrWhiteSpace(providedSecret))
            {
                return false;
            }

            RealmNode? realm = await _dbContext.RealmNodes
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.RealmId == realmId && x.Enabled, cancellationToken);

            if (realm is null)
            {
                return false;
            }

            string providedHash = HashSecret(providedSecret);

            return FixedTimeEquals(realm.ServiceSecretHash, providedHash);
        }

        public async Task<RealmHeartbeatResult> UpdateHeartbeatAsync(
            RealmHeartbeatRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.RealmId))
            {
                throw new ArgumentException("RealmId is required.", nameof(request.RealmId));
            }

            RealmNode? realm = await _dbContext.RealmNodes
                .SingleOrDefaultAsync(x => x.RealmId == request.RealmId, cancellationToken);

            if (realm is null)
            {
                throw new InvalidOperationException($"Realm '{request.RealmId}' was not found.");
            }

            if (!realm.Enabled)
            {
                throw new InvalidOperationException($"Realm '{request.RealmId}' is disabled.");
            }

            realm.Status = string.IsNullOrWhiteSpace(request.Status)
                ? "online"
                : request.Status.Trim().ToLowerInvariant();

            realm.CurrentPlayers = Math.Max(0, request.CurrentPlayers);
            realm.MaxPlayers = Math.Max(0, request.MaxPlayers);
            realm.BuildVersion = request.BuildVersion?.Trim();
            realm.ProtocolVersion = request.ProtocolVersion;
            realm.LastHeartbeatUtc = DateTime.UtcNow;
            realm.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new RealmHeartbeatResult(
                realm.RealmId,
                realm.Status,
                realm.CurrentPlayers,
                realm.MaxPlayers,
                realm.LastHeartbeatUtc);
        }

        public async Task<RealmSummary[]> GetEnabledRealmsAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.RealmNodes
                .AsNoTracking()
                .Where(x => x.Enabled)
                .OrderBy(x => x.Region)
                .ThenBy(x => x.Name)
                .Select(x => new RealmSummary(
                    x.RealmId,
                    x.Name,
                    x.Region,
                    x.PublicBaseUrl,
                    x.Status,
                    x.CurrentPlayers,
                    x.MaxPlayers,
                    x.LastHeartbeatUtc))
                .ToArrayAsync(cancellationToken);
        }

        private static void ValidateRegistrationRequest(RealmRegistrationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RealmId))
            {
                throw new ArgumentException("RealmId is required.", nameof(request.RealmId));
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Name is required.", nameof(request.Name));
            }

            if (string.IsNullOrWhiteSpace(request.Region))
            {
                throw new ArgumentException("Region is required.", nameof(request.Region));
            }

            if (string.IsNullOrWhiteSpace(request.PublicBaseUrl))
            {
                throw new ArgumentException("PublicBaseUrl is required.", nameof(request.PublicBaseUrl));
            }

            if (string.IsNullOrWhiteSpace(request.ServiceSecret))
            {
                throw new ArgumentException("ServiceSecret is required.", nameof(request.ServiceSecret));
            }

            if (request.ProtocolVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.ProtocolVersion), "ProtocolVersion must be greater than 0.");
            }

            if (request.MaxPlayers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.MaxPlayers), "MaxPlayers cannot be negative.");
            }
        }

        private static string NormalizeBaseUrl(string url)
        {
            string trimmed = url.Trim();

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? parsed))
            {
                throw new ArgumentException($"Invalid base url: {url}");
            }

            if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException($"Unsupported base url scheme: {parsed.Scheme}");
            }

            return parsed.ToString().TrimEnd('/');
        }

        private static string HashSecret(string secret)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(secret.Trim());
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        private static bool FixedTimeEquals(string leftHex, string rightHex)
        {
            byte[] left = Convert.FromHexString(leftHex);
            byte[] right = Convert.FromHexString(rightHex);
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
    }

    public sealed record RealmRegistrationRequest(
        string RealmId,
        string Name,
        string Region,
        string PublicBaseUrl,
        string? InternalBaseUrl,
        string ServiceSecret,
        int MaxPlayers,
        string? BuildVersion,
        int ProtocolVersion);

    public sealed record RealmRegistrationResult(
        string RealmId,
        bool Created,
        bool Enabled,
        string Status);

    public sealed record RealmHeartbeatRequest(
        string RealmId,
        string Status,
        int CurrentPlayers,
        int MaxPlayers,
        string? BuildVersion,
        int ProtocolVersion);

    public sealed record RealmHeartbeatResult(
        string RealmId,
        string Status,
        int CurrentPlayers,
        int MaxPlayers,
        DateTime? LastHeartbeatUtc);

    public sealed record RealmSummary(
        string RealmId,
        string Name,
        string Region,
        string PublicBaseUrl,
        string Status,
        int CurrentPlayers,
        int MaxPlayers,
        DateTime? LastHeartbeatUtc);
}