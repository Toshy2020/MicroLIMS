using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Quality;

public class QualityRecordCore(
    IMicroLimsDbContext db,
    PermissionService permissions,
    IUserSectionScopeService scope,
    ReferenceNumberGenerator numbers,
    IElectronicSignatureService signatures,
    TimeProvider clock)
{
    public async Task<QualityRecordDto> CreateDtoAsync(QualityRecordType type, CreateQualityRecordRequest request,
        int userId, CancellationToken ct = default) => QualityRecordDto.From(await CreateAsync(type, request, userId, ct));

    public Task<QualityRecord> CreateAsync(QualityRecordType type, CreateQualityRecordRequest request, int userId,
        CancellationToken ct = default) => CreateAsync(type, request, userId, ct, null, false);

    internal Task<QualityRecord> CreateAsync(QualityRecordType type, CreateQualityRecordRequest request, int userId,
        CancellationToken ct, DeviationDetail? initialDeviation, bool fromSource) => UnitOfWork.RunAsync(db, async () =>
    {
        if (!fromSource) await EnsureCreateAccessAsync(type, userId, ct);
        if (request.SectionId is int sectionId)
        {
            var memberSections = await scope.GetMemberSectionIdsAsync(userId, ct);
            if (!memberSections.Contains(sectionId)) throw Denied();
            if (!await db.DocumentSections.AsNoTracking().AnyAsync(s => s.Id == sectionId, ct))
                throw Missing();
        }

        var record = new QualityRecord
        {
            Type = type,
            Title = RequiredText(request.Title, 200),
            Description = RequiredText(request.Description, 4000),
            SectionId = request.SectionId,
            RaisedByUserId = userId,
            RaisedAtUtc = clock.GetUtcNow().UtcDateTime,
            DeviationDetail = initialDeviation
        };
        var targetReader = new QualityLinkTargets(db, permissions, scope, this);
        foreach (var link in (request.Links ?? []).Distinct())
        {
            await targetReader.EnsureReadableAsync(link.TargetType, link.TargetId, userId, ct);
            record.Links.Add(new QualityRecordLink
            {
                TargetType = link.TargetType, TargetId = link.TargetId, AddedByUserId = userId,
                AddedAtUtc = record.RaisedAtUtc
            });
        }

        record.Number = await numbers.GenerateQualityNumberAsync(type, ct);
        db.QualityRecords.Add(record);
        await db.SaveChangesAsync(ct);
        return record;
    }, ct);

    private async Task EnsureCreateAccessAsync(QualityRecordType type, int userId, CancellationToken ct)
    {
        var allowed = type switch
        {
            QualityRecordType.Deviation => await HoldsAsync(userId, PermissionConstants.QualityRaise, ct),
            QualityRecordType.ChangeControl => await HoldsAsync(userId, PermissionConstants.QualityRaise, ct)
                && await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Role != null &&
                    (u.Role.Type == RoleType.SectionHead || u.Role.Type == RoleType.QcManager || u.Role.Type == RoleType.Qa), ct),
            QualityRecordType.Capa => await HoldsAsync(userId, PermissionConstants.QualityInvestigate, ct)
                || await HoldsAsync(userId, PermissionConstants.QualityApprove, ct),
            QualityRecordType.Complaint => await HoldsAsync(userId, PermissionConstants.QualityApprove, ct),
            _ => throw new BusinessRuleException(MessageCodes.QualityInvalidType, "Unknown quality record type.")
        };
        if (!allowed) throw Denied();
    }

    internal async Task EnsureViewAsync(int userId, CancellationToken ct)
    {
        if (!await HoldsAsync(userId, PermissionConstants.QualityView, ct)) throw Missing();
    }

    internal sealed record ReadAccess(RoleType? Role, IReadOnlySet<string> Permissions, IReadOnlyList<int>? Sections)
    {
        internal bool Holds(string permission) => Permissions.Contains(permission);
        internal bool ReadsSection(int? section) => section is null || Sections is null || Sections.Contains(section.Value);
    }

    internal async Task<ReadAccess> GetReadAccessAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive
            && u.Role != null && u.Role.IsActive)
            .Select(u => new
            {
                Role = u.Role!.Type,
                Codes = db.RolePermissions.AsNoTracking().Where(p => p.RoleId == u.RoleId && p.Permission != null)
                    .Select(p => p.Permission!.Code).ToList()
            }).SingleOrDefaultAsync(ct);
        var sections = user?.Codes.Contains(PermissionConstants.QualityView) == true
            ? await scope.GetReadableSectionIdsAsync(userId, ct) : [];
        return new(user?.Role, (user?.Codes ?? []).ToHashSet(StringComparer.Ordinal), sections);
    }

    internal async Task<IQueryable<QualityRecord>> ReadableRecordsAsync(int userId, CancellationToken ct) =>
        ReadableRecords(userId, await GetReadAccessAsync(userId, ct));

    internal IQueryable<QualityRecord> ReadableRecords(int userId, ReadAccess access)
    {
        var query = db.QualityRecords.AsNoTracking();
        if (!access.Holds(PermissionConstants.QualityView)) return query.Where(_ => false);

        var sections = access.Sections;
        if (sections is not null)
            query = query.Where(r => r.SectionId == null || sections.Contains(r.SectionId.Value));

        if (access.Role == RoleType.Analyst && !access.Holds(PermissionConstants.QualityInvestigate)
            && !access.Holds(PermissionConstants.QualityLabConclude)
            && !access.Holds(PermissionConstants.QualityApprove))
            query = query.Where(r => r.RaisedByUserId == userId || r.OwnerUserId == userId ||
                r.Actions.Any(a => a.ResponsibleUserId == userId));

        return query;
    }

    public async Task<QualityRecord> LoadForReadAsync(int id, int userId, CancellationToken ct = default) =>
        await (await ReadableRecordsAsync(userId, ct)).Include(r => r.Steps).SingleOrDefaultAsync(r => r.Id == id, ct)
        ?? throw Missing();

    public async Task<QualityRecordDto> ReadDtoAsync(int id, int userId, CancellationToken ct = default)
    {
        var records = await ReadableRecordsAsync(userId, ct);
        return await records.Where(r => r.Id == id)
            .Select(r => new QualityRecordDto(r.Id, r.Number, r.Type, r.Title, r.Description, r.SectionId,
                r.RaisedByUserId, r.RaisedAtUtc, r.OwnerUserId, r.Version)
            {
                Attachments = r.Attachments.OrderBy(a => a.Id).Select(a => new QualityAttachmentDto(a.Id,
                    a.OriginalFileName, a.ContentType, a.FileSizeBytes, a.UploadedByUserId, a.UploadedAtUtc,
                    a.VoidedAtUtc, a.VoidedByUserId, a.VoidReason)).ToList()
            }).SingleOrDefaultAsync(ct) ?? throw Missing();
    }

    public async Task<QualityRecord> LoadForWriteAsync(int id, uint expectedVersion, int userId,
        CancellationToken ct = default)
    {
        var readable = await ReadableRecordsAsync(userId, ct);
        var version = await readable.Where(r => r.Id == id).Select(r => (uint?)r.Version).SingleOrDefaultAsync(ct)
            ?? throw Missing();
        if (version != expectedVersion) throw Stale();
        var record = await db.QualityRecords.Include(r => r.Steps).SingleAsync(r => r.Id == id, ct);
        if (record.Version != expectedVersion) throw Stale();
        return record;
    }

    internal Task<T> MutateAsync<T>(int id, uint expectedVersion, int userId, bool destructive,
        Func<QualityRecord, Task<T>> command, CancellationToken ct) => UnitOfWork.RunAsync(db, async () =>
    {
        var record = await LockForWriteAsync(id, expectedVersion, userId, ct);
        if (destructive)
        {
            if (record.OwnerUserId != userId && !await HoldsAsync(userId, PermissionConstants.QualityInvestigate, ct))
                throw Denied();
        }
        else if (!await HoldsAsync(userId, PermissionConstants.QualityRaise, ct))
            throw Denied();

        // A child edit advances the aggregate's xmin, even when no header value changes.
        AdvanceVersion(record);
        return await command(record);
    }, ct);

    internal async Task<QualityRecord> LockForWriteAsync(int id, uint expectedVersion, int userId, CancellationToken ct)
    {
        var readable = await ReadableRecordsAsync(userId, ct);
        if (!await readable.AnyAsync(r => r.Id == id, ct)) throw Missing();
        await LockAsync(id, ct);
        return await LoadForWriteAsync(id, expectedVersion, userId, ct);
    }

    internal void AdvanceVersion(QualityRecord record) => db.Entry(record).Property(r => r.Title).IsModified = true;

    internal Task<QualityRecordStep> CancelAsync(QualityRecord record, string reason, string password, int userId,
        string? ip, CancellationToken ct)
    {
        record.CancelReason = RequiredText(reason, 4000);
        record.IsCancelled = true;
        db.SetNextAuditReason(record, record.CancelReason);
        return SignStepAsync(record, QualityStepKind.QualityCancelled, SignatureMeaning.QualityCancelled,
            new { record.CancelReason }, password, record.CancelReason, userId, ip, ct);
    }

    internal async Task<QualityRecordStep> SignStepAsync(QualityRecord record, QualityStepKind kind, SignatureMeaning meaning,
        object snapshot, string password, string? comment, int userId, string? ip, CancellationToken ct = default,
        Action? afterVerification = null)
    {
        if (db.Database.CurrentTransaction is null)
            throw new BusinessRuleException(MessageCodes.QualityTransactionRequired, "Signing requires a quality record transaction.");

        await LockAsync(record.Id, ct);
        var version = await db.QualityRecords.AsNoTracking().Where(r => r.Id == record.Id)
            .Select(r => (uint?)r.Version).SingleOrDefaultAsync(ct) ?? throw Missing();
        if (version != record.Version) throw Stale();
        var tracked = await db.QualityRecords.SingleOrDefaultAsync(r => r.Id == record.Id, ct) ?? throw Missing();
        if (tracked.Version != version) throw Stale();

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT "Id" FROM "QualityRecordLinks" WHERE "RecordId" = {record.Id} ORDER BY "Id" FOR UPDATE""", ct);
        var data = JsonSerializer.SerializeToElement(snapshot);
        if (comment is not null) comment = RequiredText(comment, 4000);
        var signature = await signatures.SignAsync(userId, password, meaning, nameof(QualityRecord), record.Id, comment, ip, ct);
        afterVerification?.Invoke();
        AdvanceVersion(tracked);
        signature.SignedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        var step = new QualityRecordStep
        {
            RecordId = record.Id, Kind = kind, SignatureId = signature.Id, SignedByUserId = userId,
            SignedAtUtc = signature.SignedAt, Comment = comment,
            SnapshotJson = JsonSerializer.Serialize(new { tracked.Version, Data = data })
        };
        db.QualityRecordSteps.Add(step);
        await db.SaveChangesAsync(ct);
        if (!ReferenceEquals(record, tracked))
        {
            record.Version = tracked.Version;
            record.Steps.Add(step);
        }
        return step;
    }

    public void EnsureNotRaiserOrOwner(QualityRecord record, int userId)
    {
        if (record.RaisedByUserId == userId || record.OwnerUserId == userId)
            throw new BusinessRuleException(MessageCodes.QualityRaiserOrOwner,
                "You raised or own this record, so you cannot approve or close it.");
    }

    public bool IsStepSigned(QualityRecord record, QualityStepKind kind) => record.Steps.Any(s => s.Kind == kind);

    internal static string RequiredText(string? text, int max)
    {
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > max)
            throw new BusinessRuleException(MessageCodes.QualityInvalidText, $"Enter between 1 and {max} characters.",
                new Dictionary<string, string> { ["max"] = max.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        return value;
    }

    internal static NotFoundException Missing() => new(MessageCodes.QualityNotFound, "Quality record or related resource was not found.");
    private sealed class StaleQualityRecordException() : DbUpdateConcurrencyException(
        "The quality record changed. Reload it and try again."), ICodedMessage
    {
        public string Code => MessageCodes.QualityStale;
        public IReadOnlyDictionary<string, string>? Parameters => null;
    }

    internal static DbUpdateConcurrencyException Stale() => new StaleQualityRecordException();
    internal static CodedAccessDeniedException Denied() => new(MessageCodes.QualityAccessDenied, "You do not have permission for this quality record operation.");
    private Task<bool> HoldsAsync(int userId, string permission, CancellationToken ct) =>
        permissions.UserHasPermissionAsync(userId, permission, ct);

    internal Task LockAsync(int id, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync(
        $"""SELECT "Id" FROM "QualityRecords" WHERE "Id" = {id} FOR UPDATE""", ct);
}
