using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EDOC.API.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace EDOC.API.Services;

public sealed class TokenService(IConfiguration config, UserManager<EdocUser> users)
{
    public async Task<string> CreateAsync(EdocUser user)
    {
        var key = config["Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT signing key is not configured.");
        var roles = await users.GetRolesAsync(user);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id), new(JwtRegisteredClaimNames.Email, user.Email ?? ""), new("name", user.DisplayName) };
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(config["Jwt:Issuer"] ?? "EDOC", config["Jwt:Audience"] ?? "EDOC.Mobile", claims, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
