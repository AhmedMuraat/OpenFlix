using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenFlix.Catalog;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<CatalogDb>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("catalog")));
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

var catalog = app.MapGroup("/api/catalog");
catalog.MapGet("/home", async (CatalogDb db, CancellationToken ct) =>
{
    var featured = await db.Media.AsNoTracking().Where(x => x.Featured).OrderByDescending(x => x.Views).FirstAsync(ct);
    var movies = await db.Media.AsNoTracking().Where(x => x.Kind == MediaKind.Movie).OrderByDescending(x => x.Views).Take(12).ToListAsync(ct);
    var series = await db.Media.AsNoTracking().Where(x => x.Kind == MediaKind.Series).OrderByDescending(x => x.Views).Take(12).ToListAsync(ct);
    var genres = await db.Media.AsNoTracking().GroupBy(x => x.Genre)
        .Select(g => new { Genre = g.Key, Items = g.OrderByDescending(x => x.Views).Take(10).ToList() }).ToListAsync(ct);
    return Results.Ok(new { featured, movies, series, genres });
});
catalog.MapGet("/media/{id:guid}", async (Guid id, CatalogDb db, CancellationToken ct) =>
    await db.Media.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is { } item
        ? Results.Ok(item) : Results.NotFound());
catalog.MapGet("/search", async (string q, MediaKind? kind, CatalogDb db, CancellationToken ct) =>
{
    var query = db.Media.AsNoTracking().Where(x => EF.Functions.ILike(x.Title, $"%{q}%") ||
        EF.Functions.ILike(x.Genre, $"%{q}%"));
    if (kind is not null) query = query.Where(x => x.Kind == kind);
    return Results.Ok(await query.OrderByDescending(x => x.Views).Take(30).ToListAsync(ct));
});
catalog.MapPost("/media/{id:guid}/view", async (Guid id, CatalogDb db, CancellationToken ct) =>
{
    var updated = await db.Media.Where(x => x.Id == id).ExecuteUpdateAsync(
        setters => setters.SetProperty(x => x.Views, x => x.Views + 1), ct);
    return updated == 1 ? Results.NoContent() : Results.NotFound();
});
catalog.MapPut("/media/{id:guid}/progress", async (Guid id, ProgressRequest request, ClaimsPrincipal principal,
    CatalogDb db, CancellationToken ct) =>
{
    if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out var userId))
        return Results.Unauthorized();
    var progress = await db.WatchProgress.SingleOrDefaultAsync(x => x.UserId == userId && x.MediaId == id, ct);
    if (progress is null)
        db.WatchProgress.Add(new WatchProgress { UserId = userId, MediaId = id,
            PositionSeconds = request.PositionSeconds, DurationSeconds = request.DurationSeconds });
    else
    {
        progress.PositionSeconds = request.PositionSeconds;
        progress.DurationSeconds = request.DurationSeconds;
        progress.UpdatedAt = DateTime.UtcNow;
    }
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
}).RequireAuthorization();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDb>();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Media.AnyAsync()) { db.Media.AddRange(Seed.Items); await db.SaveChangesAsync(); }
}
app.MapDefaultEndpoints();
app.Run();

public partial class Program;

