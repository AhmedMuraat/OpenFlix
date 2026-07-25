using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace OpenFlix.Identity;

public sealed class TokenService(IConfiguration configuration, IdentityDb db, UserManager<AppUser> users)
{
    public async Task<AuthResponse> IssueAsync(AppUser user, CancellationToken ct)
    {
        var roles = await users.GetRolesAsync(user);
        var expires = DateTime.UtcNow.AddMinutes(15);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new("name", user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var jwt = new JwtSecurityToken(
            configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims,
            expires: expires, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var rawRefresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        db.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = Hash(rawRefresh), UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync(ct);
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(jwt), rawRefresh, expires,
            new UserView(user.Id, user.Email!, user.DisplayName, roles.ToArray()));
    }

    public async Task<AuthResponse?> RotateAsync(string rawToken, CancellationToken ct)
    {
        var token = await db.RefreshTokens.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == Hash(rawToken), ct);
        if (token is null || token.RevokedAt is not null || token.ExpiresAt <= DateTime.UtcNow) return null;
        token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await IssueAsync(token.User, ct);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct)
    {
        var token = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == Hash(rawToken), ct);
        if (token is null) return;
        token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
