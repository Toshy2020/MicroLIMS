using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MicroLIMS.Infrastructure.Authentication;

public interface IJwtTokenService
{
    string IssueToken(string userId, string role, IEnumerable<string>? permissionCodes = null);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenService(string key, string issuer, string audience)
    {
        _key = key;
        _issuer = issuer;
        _audience = audience;
    }

    // Adds one "permission" claim per code, alongside the existing Role
    // claim (unchanged) - additive, so anything still checking the Role
    // claim via [Authorize(Roles=...)] keeps working exactly as before.
    public string IssueToken(string userId, string role, IEnumerable<string>? permissionCodes = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role)
        };
        if (permissionCodes is not null)
            claims.AddRange(permissionCodes.Select(code => new Claim("permission", code)));

        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(_issuer, _audience, claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
