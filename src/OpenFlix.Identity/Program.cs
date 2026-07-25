using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenFlix.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<IdentityDb>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("identity")));
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
auth.MapPost("/register", async (RegisterRequest request, UserManager<AppUser> users, TokenService tokens, CancellationToken ct) =>
{
    var user = new AppUser
    {
        Id = Guid.NewGuid(), UserName = request.Email.Trim().ToLowerInvariant(),
        Email = request.Email.Trim().ToLowerInvariant(), DisplayName = request.DisplayName.Trim()
    };
    var result = await users.CreateAsync(user, request.Password);
    if (!result.Succeeded)
        return Results.ValidationProblem(result.Errors
            .GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
    await users.AddToRoleAsync(user, "Member");
    return Results.Ok(await tokens.IssueAsync(user, ct));
});
auth.MapPost("/login", async (LoginRequest request, UserManager<AppUser> users,
    SignInManager<AppUser> signIn, TokenService tokens, CancellationToken ct) =>
{
    var user = await users.FindByEmailAsync(request.Email.Trim());
    if (user is null) return Results.Unauthorized();
    var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
    return result.Succeeded ? Results.Ok(await tokens.IssueAsync(user, ct)) : Results.Unauthorized();
});
auth.MapPost("/refresh", async (RefreshRequest request, TokenService tokens, CancellationToken ct) =>
    await tokens.RotateAsync(request.RefreshToken, ct) is { } response ? Results.Ok(response) : Results.Unauthorized());
auth.MapPost("/logout", async (RefreshRequest request, TokenService tokens, CancellationToken ct) =>
{
    await tokens.RevokeAsync(request.RefreshToken, ct);
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

public partial class Program;
