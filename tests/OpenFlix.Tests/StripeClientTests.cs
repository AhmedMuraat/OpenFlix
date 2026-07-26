using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using OpenFlix.Subscriptions;
using Xunit;

namespace OpenFlix.Tests;

public sealed class StripeClientTests
{
    [Fact]
    public void PaymentsRemainDisabledWithoutStripeCredentials()
    {
        var client = CreateClient(new Dictionary<string, string?>());

        Assert.False(client.CheckoutEnabled);
        Assert.False(client.WebhooksEnabled);
    }

    [Fact]
    public void CheckoutAndWebhooksRequireCompleteConfiguration()
    {
        var client = CreateClient(new Dictionary<string, string?>
        {
            ["Stripe:SecretKey"] = "sk_test_local",
            ["Stripe:PriceId"] = "price_local",
            ["Stripe:WebhookSecret"] = "whsec_local"
        });

        Assert.True(client.CheckoutEnabled);
        Assert.True(client.WebhooksEnabled);
    }

    [Fact]
    public void ValidStripeSignatureIsAccepted()
    {
        const string payload = """{"id":"evt_local","type":"test"}""";
        const string secret = "whsec_local";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}")));
        var client = CreateClient(new Dictionary<string, string?>
        {
            ["Stripe:SecretKey"] = "sk_test_local",
            ["Stripe:PriceId"] = "price_local",
            ["Stripe:WebhookSecret"] = secret
        });

        Assert.True(client.VerifyWebhook(payload, $"t={timestamp},v1={signature}"));
    }

    private static StripeClient CreateClient(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new StripeClient(new HttpClient(), configuration);
    }
}
