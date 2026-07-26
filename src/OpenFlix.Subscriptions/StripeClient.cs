using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenFlix.Subscriptions;

public sealed class StripeClient(HttpClient http, IConfiguration configuration)
{
    public bool CheckoutEnabled =>
        configuration["Stripe:SecretKey"]?.StartsWith("sk_", StringComparison.Ordinal) == true &&
        configuration["Stripe:PriceId"]?.StartsWith("price_", StringComparison.Ordinal) == true;

    public bool WebhooksEnabled =>
        CheckoutEnabled &&
        configuration["Stripe:WebhookSecret"]?.StartsWith("whsec_", StringComparison.Ordinal) == true;

    public async Task<string> CreateCheckoutAsync(Guid userId, string successUrl, string cancelUrl, CancellationToken ct)
    {
        if (!CheckoutEnabled)
            throw new InvalidOperationException("Stripe checkout is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration["Stripe:SecretKey"]);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["mode"] = "subscription",
            ["line_items[0][price]"] = configuration["Stripe:PriceId"]!,
            ["line_items[0][quantity]"] = "1",
            ["success_url"] = successUrl,
            ["cancel_url"] = cancelUrl,
            ["client_reference_id"] = userId.ToString(),
            ["subscription_data[metadata][user_id]"] = userId.ToString()
        });
        using var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(json).RootElement.GetProperty("url").GetString()!;
    }

    public bool VerifyWebhook(string payload, string signatureHeader)
    {
        if (!WebhooksEnabled) return false;
        var values = signatureHeader.Split(',').Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2).GroupBy(x => x[0]).ToDictionary(x => x.Key, x => x.Select(v => v[1]).ToArray());
        if (!values.TryGetValue("t", out var timestamps) || !long.TryParse(timestamps[0], out var timestamp) ||
            Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp) > 300 ||
            !values.TryGetValue("v1", out var signatures)) return false;
        var secret = configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret)) return false;
        var digest = Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}")));
        return signatures.Any(signature => CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(digest), Encoding.ASCII.GetBytes(signature)));
    }
}

