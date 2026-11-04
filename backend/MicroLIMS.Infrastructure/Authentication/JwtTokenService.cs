using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MicroLIMS.Infrastructure.Authentication;

public interface IJwtTokenService
{
    string IssueToken(string userId, string role, IEnumerable<string>? permissionCodes = null, int? roleId = null, int? permissionVersion = null);
}

public class JwtTokenService : IJwtTokenService
{
    public const string RoleIdClaim = "rid";
    public const string PermissionVersionClaim = "pv";

    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _lifetime;

    public JwtTokenService(string key, string issuer, string audience, TimeSpan lifetime)
    {
        _key = key;
        _issuer = issuer;
        _audience = audience;
        _lifetime = lifetime;
    }

    // Adds one "permission" claim per code, alongside the existing Role
    // claim (unchanged) - additive, so anything still checking the Role
    // claim via [Authorize(Roles=...)] keeps working exactly as before.
    //
    // roleId and permissionVersion become the "rid" and "pv" claims: which
    // role the user held and which revision of its grants the permission
    // claims came from. AccessTokenRevalidator refuses a token whose pair no
    // longer matches the database, so a token issued without them never
    // passes the API (the callers that omit them are tests of this class).
    public string IssueToken(string userId, string role, IEnumerable<string>? permissionCodes = null, int? roleId = null, int? permissionVersion = null)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role),
            // Lets the API reject a token issued before the user's last
            // password change (AccessTokenRevalidator).
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        if (roleId is { } rid)
            claims.Add(new Claim(RoleIdClaim, rid.ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer32));
        if (permissionVersion is { } pv)
            claims.Add(new Claim(PermissionVersionClaim, pv.ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer32));
        if (permissionCodes is not null)
            claims.AddRange(permissionCodes.Select(code => new Claim("permission", code)));

        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(_issuer, _audience, claims, notBefore: now, expires: now.Add(_lifetime), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
