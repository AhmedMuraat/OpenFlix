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
    public async Task<IssuedTokens> IssueAsync(AppUser user, CancellationToken ct)
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
        await db.RefreshTokens
            .Where(x => x.UserId == user.Id && (x.ExpiresAt <= DateTime.UtcNow ||
                x.RevokedAt != null && x.RevokedAt <= DateTime.UtcNow.AddDays(-7)))
            .ExecuteDeleteAsync(ct);
        db.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = Hash(rawRefresh), UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync(ct);
        var response = new AuthResponse(new JwtSecurityTokenHandler().WriteToken(jwt), expires,
            new UserView(user.Id, user.Email!, user.DisplayName, roles.ToArray()));
        return new IssuedTokens(response, rawRefresh);
    }

    public async Task<IssuedTokens?> RotateAsync(string rawToken, CancellationToken ct)
    {
        var token = await db.RefreshTokens.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == Hash(rawToken), ct);
        if (token is null || token.ExpiresAt <= DateTime.UtcNow) return null;
        if (token.RevokedAt is not null)
        {
            await db.RefreshTokens.Where(x => x.UserId == token.UserId && x.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
            return null;
        }
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
