using System.Text;
using System.Security.Cryptography;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenFlix.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.ValidateProductionSecret("Jwt:Key")
    .ValidateProductionSecret("InternalApi:Key");
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<IdentityDb>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("identity"),
        npgsql => npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(3), null)));
builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<IdentityDb>()
    .AddSignInManager();
builder.Services.AddScoped<TokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseAuthentication();
app.UseAuthorization();

var auth = app.MapGroup("/api/auth");
auth.MapPost("/register", async (RegisterRequest request, HttpRequest httpRequest, HttpResponse httpResponse,
    UserManager<AppUser> users, TokenService tokens, IConfiguration configuration, CancellationToken ct) =>
{
    var email = request.Email?.Trim().ToLowerInvariant() ?? "";
    var displayName = request.DisplayName?.Trim() ?? "";
    var validation = new Dictionary<string, string[]>();
    if (!new EmailAddressAttribute().IsValid(email))
        validation["email"] = ["Enter a valid email address."];
    if (displayName.Length is < 2 or > 80)
        validation["displayName"] = ["Display name must be between 2 and 80 characters."];
    if (string.IsNullOrEmpty(request.Password))
        validation["password"] = ["Password is required."];
    if (validation.Count > 0) return Results.ValidationProblem(validation);
    var user = new AppUser
    {
        Id = Guid.NewGuid(), UserName = email,
        Email = email, DisplayName = displayName
    };
    var result = await users.CreateAsync(user, request.Password);
    if (!result.Succeeded)
        return Results.ValidationProblem(result.Errors
            .GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
    await users.AddToRoleAsync(user, "Member");
    var issued = await tokens.IssueAsync(user, ct);
    SetRefreshCookie(httpRequest, httpResponse, configuration, issued.RefreshToken);
    return Results.Ok(issued.Response);
});
auth.MapPost("/login", async (LoginRequest request, UserManager<AppUser> users,
    SignInManager<AppUser> signIn, TokenService tokens, HttpRequest httpRequest, HttpResponse httpResponse,
    IConfiguration configuration, CancellationToken ct) =>
{
    var user = await users.FindByEmailAsync(request.Email.Trim());
    if (user is null) return Results.Unauthorized();
    var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
    if (!result.Succeeded) return Results.Unauthorized();
    var issued = await tokens.IssueAsync(user, ct);
    SetRefreshCookie(httpRequest, httpResponse, configuration, issued.RefreshToken);
    return Results.Ok(issued.Response);
});
auth.MapPost("/refresh", async (HttpRequest request, HttpResponse response, TokenService tokens,
    IConfiguration configuration, CancellationToken ct) =>
{
    if (!request.Cookies.TryGetValue("openflix.refresh", out var refreshToken))
        return Results.Unauthorized();
    var issued = await tokens.RotateAsync(refreshToken, ct);
    if (issued is null)
    {
        DeleteRefreshCookie(request, response, configuration);
        return Results.Unauthorized();
    }
    SetRefreshCookie(request, response, configuration, issued.RefreshToken);
    return Results.Ok(issued.Response);
});
auth.MapPost("/logout", async (HttpRequest request, HttpResponse response, TokenService tokens,
    IConfiguration configuration, CancellationToken ct) =>
{
    if (request.Cookies.TryGetValue("openflix.refresh", out var refreshToken))
        await tokens.RevokeAsync(refreshToken, ct);
    DeleteRefreshCookie(request, response, configuration);
    return Results.NoContent();
});

app.MapPut("/internal/users/{id:guid}/subscriber", async (Guid id, bool active, HttpRequest request,
    UserManager<AppUser> users, IConfiguration configuration) =>
{
    var supplied = request.Headers["X-Internal-Key"].ToString();
    var expected = configuration["InternalApi:Key"] ?? "";
    if (expected.Length < 32 || supplied.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected))) return Results.Unauthorized();
    var user = await users.FindByIdAsync(id.ToString());
    if (user is null) return Results.NotFound();
    var isSubscriber = await users.IsInRoleAsync(user, "Subscriber");
    if (active && !isSubscriber) await users.AddToRoleAsync(user, "Subscriber");
    if (!active && isSubscriber) await users.RemoveFromRoleAsync(user, "Subscriber");
    return Results.NoContent();
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDb>();
    await db.Database.EnsureCreatedAsync();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var role in new[] { "Member", "Subscriber", "Admin" })
        if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole<Guid>(role));
}

app.MapDefaultEndpoints();
app.Run();

static void SetRefreshCookie(HttpRequest request, HttpResponse response, IConfiguration configuration, string token)
{
    response.Cookies.Append("openflix.refresh", token, RefreshCookieOptions(request, configuration, DateTimeOffset.UtcNow.AddDays(30)));
}

static void DeleteRefreshCookie(HttpRequest request, HttpResponse response, IConfiguration configuration)
{
    response.Cookies.Delete("openflix.refresh", RefreshCookieOptions(request, configuration, DateTimeOffset.UnixEpoch));
}

static CookieOptions RefreshCookieOptions(HttpRequest request, IConfiguration configuration, DateTimeOffset expires) => new()
{
    HttpOnly = true,
    IsEssential = true,
    SameSite = SameSiteMode.Strict,
    Secure = request.IsHttps || configuration.GetValue("Auth:SecureCookies", false),
    Path = "/api/auth",
    Expires = expires
};

public partial class Program;
