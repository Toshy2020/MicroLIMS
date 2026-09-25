using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.API.Authorization;

// A signed access token only proves who the user WAS when it was issued:
// it carries their role and permission claims, and signature validation
// never looks at the database. Without this check a user who was disabled,
// locked, moved to another role or whose password was changed kept their
// old access until the token expired - revocation only ever touched
// refresh tokens.
//
// Runs once per authenticated request (JwtBearerEvents.OnTokenValidated,
// see JwtAuthenticationExtensions), before any [Authorize] role or policy
// check, so a stale role claim is never honoured. A rejected token is a
// 401; the frontend's apiClient then calls /api/auth/refresh, which issues
// a token with the user's CURRENT role, or - for a disabled or locked
// account - fails and signs them out (RefreshAsync already refuses those).
public sealed class AccessTokenRevalidator
{
    private readonly MicroLimsDbContext _db;

    public AccessTokenRevalidator(MicroLimsDbContext db)
    {
        _db = db;
    }

    // Null when the token still describes the account; otherwise why not.
    // The reason is logged, never returned to the caller.
    public async Task<string?> GetRejectionReasonAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            return "Token carries no user id.";

        // Tokens issued before this check existed have no iat. Rejecting them
        // costs one silent refresh; accepting them would exempt every
        // pre-existing token from the password-change rule below.
        if (!long.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Iat)?.Value, out var issuedAtUnix))
            return "Token carries no issue time.";

        var account = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.IsActive,
                u.LockedUntil,
                RoleType = u.Role == null ? null : (Domain.Enums.RoleType?)u.Role.Type,
                u.PasswordChangedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
            return "Account no longer exists.";
        if (!account.IsActive)
            return "Account is disabled.";
        if (account.LockedUntil is { } lockedUntil && lockedUntil > DateTime.UtcNow)
            return "Account is locked.";

        // Same fallback AuthenticationService uses when it issues the token.
        var currentRole = account.RoleType?.ToString() ?? "Analyst";
        if (!string.Equals(principal.FindFirst(ClaimTypes.Role)?.Value, currentRole, StringComparison.Ordinal))
            return "Role has changed since the token was issued.";

        // Whole seconds, as iat is. A token issued in the same second as the
        // change is accepted, so signing in straight after a password change
        // is never refused.
        if (account.PasswordChangedAt is { } changedAt
            && issuedAtUnix < new DateTimeOffset(DateTime.SpecifyKind(changedAt, DateTimeKind.Utc)).ToUnixTimeSeconds())
            return "Password was changed after the token was issued.";

        return null;
    }
}
