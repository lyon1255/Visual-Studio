using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace GnosisAuthServer.Tests;

public sealed class HmacServiceRequestAuthenticatorTests
{
    [Fact]
    public async Task AuthenticateAsync_ReturnsSuccess_ForValidSignedRequest()
    {
        var options = Options.Create(new ServiceAuthOptions
        {
            Enabled = true,
            AllowedClockSkewSeconds = 30,
            NonceTtlSeconds = 90,
            Clients =
            [
                new ServiceClientOptions
                {
                    ServiceId = "realm-core",
                    Secret = "super-secret",
                    AllowedRealmIds = ["realm-1"]
                }
            ]
        });

        var nonceStore = new TestNonceStore();
        var sut = new HmacServiceRequestAuthenticator(options, nonceStore, NullLogger<HmacServiceRequestAuthenticator>.Instance);
        var request = BuildSignedRequest("realm-core", "super-secret", "heartbeat-body");

        var result = await sut.AuthenticateAsync(request, CancellationToken.None);

        Assert.True(result.IsAuthenticated);
        Assert.NotNull(result.Context);
        Assert.Equal("realm-core", result.Context!.ServiceId);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsFailure_WhenReplayDetected()
    {
        var options = Options.Create(new ServiceAuthOptions
        {
            Enabled = true,
            AllowedClockSkewSeconds = 30,
            NonceTtlSeconds = 90,
            Clients =
            [
                new ServiceClientOptions
                {
                    ServiceId = "realm-core",
                    Secret = "super-secret",
                    AllowedRealmIds = ["realm-1"]
                }
            ]
        });

        var nonceStore = new TestNonceStore(alwaysReject: true);
        var sut = new HmacServiceRequestAuthenticator(options, nonceStore, NullLogger<HmacServiceRequestAuthenticator>.Instance);
        var request = BuildSignedRequest("realm-core", "super-secret", "heartbeat-body");

        var result = await sut.AuthenticateAsync(request, CancellationToken.None);

        Assert.False(result.IsAuthenticated);
        Assert.Equal("Replay detected.", result.Error);
    }

    private static HttpRequest BuildSignedRequest(string serviceId, string secret, string body)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = HttpMethods.Post;
        request.Path = "/api/internal/realms/heartbeat";
        request.QueryString = QueryString.Empty;

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        request.Body = new MemoryStream(bodyBytes);
        request.ContentLength = bodyBytes.Length;

        var bodyHash = Convert.ToHexString(SHA256.HashData(bodyBytes)).ToLowerInvariant();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = "nonce-123";
        var canonical = string.Join("\n",
            request.Method.ToUpperInvariant(),
            request.Path.Value ?? "/",
            string.Empty,
            timestamp,
            nonce,
            bodyHash);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        request.Headers[ServiceAuthHeaderNames.ServiceId] = serviceId;
        request.Headers[ServiceAuthHeaderNames.Timestamp] = timestamp;
        request.Headers[ServiceAuthHeaderNames.Nonce] = nonce;
        request.Headers[ServiceAuthHeaderNames.Signature] = signature;
        request.Headers[ServiceAuthHeaderNames.BodySha256] = bodyHash;
        request.Body.Position = 0;
        return request;
    }

    private sealed class TestNonceStore : INonceStore
    {
        private readonly bool _alwaysReject;

        public TestNonceStore(bool alwaysReject = false)
        {
            _alwaysReject = alwaysReject;
        }

        public bool TryUseNonce(string scope, string nonce, TimeSpan ttl) => !_alwaysReject;
    }
}
