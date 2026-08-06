using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class ElectronicSignatureService : IElectronicSignatureService
{
    private readonly MicroLimsDbContext _db;

    public ElectronicSignatureService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<ElectronicSignature> SignAsync(int userId, string password, SignatureMeaning meaning, string entityType, int entityId, string? comment, string? ipAddress)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);

        // Same generic failure for "no such user", "inactive", "locked",
        // and "wrong password" - a signature failure must not leak which
        // of those it was, same reasoning as the login endpoint.
        if (user is null || !user.IsActive || user.IsLocked || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            // Deliberately NOT touching FailedLoginAttempts/LockedUntil -
            // a mistyped signature must not lock someone out of the whole
            // system mid-workflow. Still audit the failed attempt on its
            // own, independent of whatever the caller was about to do.
            _db.AuditLogs.Add(new AuditLog
            {
                EntityName = "ElectronicSignature",
                EntityId = $"{entityType}:{entityId}",
                Action = "SignatureFailed",
                UserId = userId,
                NewValue = $"Failed signature attempt ({meaning}) for {entityType} #{entityId}.",
                TestOrderId = entityType == "TestOrder" ? entityId : null
            });
            await _db.SaveChangesAsync();
            throw new InvalidOperationException("Password verification failed. The signature was not applied.");
        }

        var signature = new ElectronicSignature
        {
            UserId = user.Id,
            UserFullNameSnapshot = user.FullName,
            UsernameSnapshot = user.Username,
            RoleSnapshot = user.Role?.Type.ToString() ?? "Unknown",
            MeaningOfSignature = meaning,
            EntityType = entityType,
            EntityId = entityId,
            Comment = comment,
            IpAddress = ipAddress
        };
        _db.ElectronicSignatures.Add(signature);
        return signature;
    }
}
