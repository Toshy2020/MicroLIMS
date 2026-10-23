using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Monitoring;

// Signs as the acting user and as a second, different person who holds the
// monitoring plan approve permission. A wrong second password gives the same
// generic refusal as any failed signature. Shared by every change to the plan
// that two people must sign (skip, window change, closing a deviation).
public static class DualSignature
{
    public static async Task<(ElectronicSignature First, ElectronicSignature Second, int SecondUserId)> SignAsync(
        IMicroLimsDbContext db, IElectronicSignatureService signatures,
        SignatureMeaning meaning, string entityType, int entityId, string reason, int userId, string password,
        string secondUsername, string secondPassword, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(secondUsername))
            throw new BusinessRuleException("A second person must sign as well. Enter their username and password.");

        var second = await db.Users.AsNoTracking().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == secondUsername.Trim());
        if (second is null || !second.IsActive)
            throw new SignatureVerificationException("The second signature could not be verified. Nothing was changed.");
        if (second.Id == userId)
            throw new BusinessRuleException("The second signature must come from a different person.");

        var mayApprove = await db.RolePermissions
            .AnyAsync(rp => rp.RoleId == second.RoleId && rp.Permission!.Code == PermissionConstants.MonitoringPlanApprove);
        if (!mayApprove)
            throw new BusinessRuleException($"{second.Username} does not hold the permission to sign monitoring plan changes.");

        var first = await signatures.SignAsync(userId, password, meaning, entityType, entityId, reason, ipAddress);
        var other = await signatures.SignAsync(second.Id, secondPassword, meaning, entityType, entityId, reason, ipAddress);
        return (first, other, second.Id);
    }
}
