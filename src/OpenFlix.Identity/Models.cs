using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace OpenFlix.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public ICollection<RefreshToken> RefreshTokens { get; init; } = [];
}

public sealed class RefreshToken
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string TokenHash { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime? RevokedAt { get; set; }
    public Guid UserId { get; init; }
    public AppUser User { get; init; } = null!;
}

public sealed class IdentityDb(DbContextOptions<IdentityDb> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
    }
}

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAt, UserView User);
public sealed record UserView(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles);
public sealed record IssuedTokens(AuthResponse Response, string RefreshToken);

