using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MicroLIMS.Infrastructure.Authentication;

public interface IJwtTokenService
{
    string IssueToken(string userId, string role);
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

    public string IssueToken(string userId, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        };

        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(_issuer, _audience, claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
