using System.Net.Http.Json;

namespace OpenFlix.Subscriptions;

public sealed class EntitlementClient(HttpClient http, IConfiguration configuration, ILogger<EntitlementClient> logger)
{
    public async Task SyncAsync(Guid userId, bool active, CancellationToken ct)
    {
        var baseUrl = configuration["InternalApi:IdentityBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("Identity service URL is not configured; subscriber entitlement was not synchronized.");
            return;
        }
        var internalKey = configuration["InternalApi:Key"];
        if (string.IsNullOrWhiteSpace(internalKey) || internalKey.Length < 32)
            throw new InvalidOperationException("InternalApi:Key must be configured with at least 32 characters.");
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"{baseUrl}/internal/users/{userId}/subscriber?active={active}");
        request.Headers.Add("X-Internal-Key", internalKey);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
