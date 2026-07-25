using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenFlix.Subscriptions;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<SubscriptionDb>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("subscriptions")));
builder.Services.AddHttpClient<StripeClient>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseAuthentication();
app.UseAuthorization();

var subscriptions = app.MapGroup("/api/subscriptions");
subscriptions.MapGet("/plans", () => Results.Ok(new[]
{
    new { Id = "free", Name = "Open", Price = 0m, Features = new[] { "Curated public-domain catalog", "One profile", "Standard playback" } },
    new { Id = "supporter", Name = "Supporter", Price = 7.99m, Features = new[] { "Support catalog curation", "Watch progress sync", "Early features", "Supporter badge" } }
}));
subscriptions.MapGet("/me", async (ClaimsPrincipal principal, SubscriptionDb db, CancellationToken ct) =>
{
    var userId = GetUserId(principal);
    return userId is null ? Results.Unauthorized() :
        Results.Ok(await db.Subscriptions.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct));
}).RequireAuthorization();
subscriptions.MapPost("/checkout", async (CheckoutRequest request, ClaimsPrincipal principal,
    StripeClient stripe, CancellationToken ct) =>
{
    var userId = GetUserId(principal);
    if (userId is null) return Results.Unauthorized();
    if (!Uri.TryCreate(request.SuccessUrl, UriKind.Absolute, out var success) ||
        !Uri.TryCreate(request.CancelUrl, UriKind.Absolute, out var cancel) ||
        success.Host != cancel.Host) return Results.BadRequest(new { error = "Invalid redirect URL." });
    var url = await stripe.CreateCheckoutAsync(userId.Value, success.ToString(), cancel.ToString(), ct);
    return Results.Ok(new { url });
}).RequireAuthorization();
subscriptions.MapPost("/webhooks/stripe", async (HttpRequest request, StripeClient stripe,
    SubscriptionDb db, CancellationToken ct) =>
{
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync(ct);
    if (!request.Headers.TryGetValue("Stripe-Signature", out var header) ||
        !stripe.VerifyWebhook(payload, header.ToString())) return Results.Unauthorized();
    using var document = JsonDocument.Parse(payload);
    var root = document.RootElement;
    var eventId = root.GetProperty("id").GetString()!;
    if (await db.ProcessedEvents.AnyAsync(x => x.Id == eventId, ct)) return Results.Ok();
    var type = root.GetProperty("type").GetString();
    var data = root.GetProperty("data").GetProperty("object");
    if (type is "customer.subscription.created" or "customer.subscription.updated" or "customer.subscription.deleted")
    {
        var subscriptionId = data.GetProperty("id").GetString()!;
        var userIdText = data.GetProperty("metadata").TryGetProperty("user_id", out var userIdNode)
            ? userIdNode.GetString() : null;
        if (Guid.TryParse(userIdText, out var userId))
        {
            var entity = await db.Subscriptions.SingleOrDefaultAsync(x => x.UserId == userId, ct);
            if (entity is null) { entity = new Subscription { UserId = userId }; db.Subscriptions.Add(entity); }
            entity.StripeSubscriptionId = subscriptionId;
            entity.StripeCustomerId = data.GetProperty("customer").GetString();
            entity.Status = MapStatus(data.GetProperty("status").GetString());
            entity.CurrentPeriodEnd = data.TryGetProperty("current_period_end", out var period)
                ? DateTimeOffset.FromUnixTimeSeconds(period.GetInt64()).UtcDateTime : null;
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
    db.ProcessedEvents.Add(new ProcessedEvent { Id = eventId });
    await db.SaveChangesAsync(ct);
    return Results.Ok();
});

await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<SubscriptionDb>().Database.EnsureCreatedAsync();
app.MapDefaultEndpoints();
app.Run();

static Guid? GetUserId(ClaimsPrincipal principal) =>
    Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out var id) ? id : null;
static SubscriptionStatus MapStatus(string? status) => status switch
{
    "active" => SubscriptionStatus.Active, "trialing" => SubscriptionStatus.Trialing,
    "past_due" or "unpaid" => SubscriptionStatus.PastDue, "canceled" => SubscriptionStatus.Canceled,
    _ => SubscriptionStatus.None
};

public partial class Program;
