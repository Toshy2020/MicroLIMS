using MicroLIMS.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services;

public class ElectronicSignatureService : IElectronicSignatureService
{
    // Signature failures deliberately never lock the account (see below),
    // so without a limit here every signature endpoint was an unlimited
    // password-guessing oracle for anyone holding a signed-in session.
    // After this many failures in the window, further attempts are refused
    // without the password being checked; the login account is untouched.
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan FailedAttemptWindow = TimeSpan.FromMinutes(15);

    private const string AuditEntityName = "ElectronicSignature";
    private const string FailedAction = "SignatureFailed";
    private const string ThrottledAction = "SignatureThrottled";

    // A refused signature must still count towards the throttle when the
    // command it was signing is rolled back - see UnitOfWork.
    internal static bool IsRefusedAttemptRecord(AuditLog entry) =>
        entry.EntityName == AuditEntityName && (entry.Action == FailedAction || entry.Action == ThrottledAction);

    private readonly IMicroLimsDbContext _db;
    private readonly TimeProvider _time;

    public ElectronicSignatureService(IMicroLimsDbContext db, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
    }

    public async Task<ElectronicSignature> SignAsync(int userId, string password, SignatureMeaning meaning, string entityType, int entityId, string? comment, string? ipAddress)
    {
        var windowStart = _time.GetUtcNow().UtcDateTime - FailedAttemptWindow;
        var recentFailures = await _db.AuditLogs.CountAsync(a =>
            a.EntityName == AuditEntityName && a.Action == FailedAction
            && a.UserId == userId && a.Timestamp >= windowStart);
        if (recentFailures >= MaxFailedAttempts)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                EntityName = AuditEntityName,
                EntityId = $"{entityType}:{entityId}",
                Action = ThrottledAction,
                UserId = userId,
                NewValue = $"Signature attempt ({meaning}) for {entityType} #{entityId} refused: {recentFailures} failed attempts in the last {FailedAttemptWindow.TotalMinutes:0} minutes.",
                TestOrderId = entityType == "TestOrder" ? entityId : null
            });
            await _db.SaveChangesAsync();
            throw new SignatureVerificationException(
                $"Too many failed signature attempts. Wait {FailedAttemptWindow.TotalMinutes:0} minutes, then try again. The signature was not applied.");
        }

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
                EntityName = AuditEntityName,
                EntityId = $"{entityType}:{entityId}",
                Action = FailedAction,
                UserId = userId,
                NewValue = $"Failed signature attempt ({meaning}) for {entityType} #{entityId}.",
                TestOrderId = entityType == "TestOrder" ? entityId : null
            });
            await _db.SaveChangesAsync();
            throw new SignatureVerificationException("Password verification failed. The signature was not applied.");
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

    // The signature trail for a record, oldest first.
    public Task<List<SignatureDto>> GetTrailAsync(string entityType, int entityId) =>
        _db.ElectronicSignatures
            .Where(s => s.EntityType == entityType && s.EntityId == entityId)
            .OrderBy(s => s.SignedAt)
            .Select(s => new SignatureDto(s.UserFullNameSnapshot, s.UsernameSnapshot, s.RoleSnapshot, s.MeaningOfSignature.ToString(), s.SignedAt, s.Comment))
            .ToListAsync();
}
