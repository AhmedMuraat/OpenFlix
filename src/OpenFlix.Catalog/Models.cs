using Microsoft.EntityFrameworkCore;

namespace OpenFlix.Catalog;

public enum MediaKind { Movie, Series }

public sealed class Media
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; set; }
    public required string Synopsis { get; set; }
    public required string Genre { get; set; }
    public required string PosterUrl { get; set; }
    public required string BackdropUrl { get; set; }
    public required string StreamUrl { get; set; }
    public required string SourceUrl { get; set; }
    public required string License { get; set; }
    public required string Attribution { get; set; }
    public MediaKind Kind { get; set; }
    public int Year { get; set; }
    public int MaturityRating { get; set; }
    public long Views { get; set; }
    public bool Featured { get; set; }
}

public sealed class WatchProgress
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public Guid MediaId { get; init; }
    public int PositionSeconds { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class CatalogDb(DbContextOptions<CatalogDb> options) : DbContext(options)
{
    public DbSet<Media> Media => Set<Media>();
    public DbSet<WatchProgress> WatchProgress => Set<WatchProgress>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Media>().HasIndex(x => new { x.Kind, x.Views });
        builder.Entity<WatchProgress>().HasIndex(x => new { x.UserId, x.MediaId }).IsUnique();
    }
}

public sealed record ProgressRequest(int PositionSeconds, int DurationSeconds);

