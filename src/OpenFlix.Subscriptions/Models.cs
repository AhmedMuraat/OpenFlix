using Microsoft.EntityFrameworkCore;

namespace OpenFlix.Subscriptions;

public enum SubscriptionStatus { None, Trialing, Active, PastDue, Canceled }

public sealed class Subscription
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public SubscriptionStatus Status { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ProcessedEvent
{
    public required string Id { get; init; }
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
}

public sealed class SubscriptionDb(DbContextOptions<SubscriptionDb> options) : DbContext(options)
{
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Subscription>().HasIndex(x => x.UserId).IsUnique();
        builder.Entity<Subscription>().HasIndex(x => x.StripeSubscriptionId).IsUnique();
        builder.Entity<ProcessedEvent>().HasKey(x => x.Id);
    }
}

public sealed record CheckoutRequest(string SuccessUrl, string CancelUrl);

