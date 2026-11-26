using System.Globalization;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Services;

public enum SopResolutionStatus
{
    NoSopNumber,
    NotFound,
    NoEffectiveRevision,
    Effective,
    Ambiguous
}

public sealed record SopResolution(
    SopResolutionStatus Status,
    string? SopNumber = null,
    int? DocumentMasterId = null,
    string? DocumentCode = null,
    string? Title = null,
    string? TypeCode = null,
    int? RevisionId = null,
    string? RevisionNumber = null,
    int? RevisionSequence = null,
    DateTime? EffectiveDate = null,
    int? PdfFileId = null,
    string? PdfFileName = null,
    int MatchingMasterCount = 0,
    IReadOnlyList<string>? MatchingCodes = null)
{
    internal int? PreviousSupersededRevisionId { get; init; }
}

public sealed class ItemSopResolver(IMicroLimsDbContext db)
{
    public async Task<SopResolution> ResolveAsync(int itemId, CancellationToken cancellationToken = default) =>
        (await ResolveManyAsync([itemId], cancellationToken))[itemId];

    public async Task<IReadOnlyDictionary<int, SopResolution>> ResolveManyAsync(
        IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
    {
        if (itemIds.Count == 0)
            return new Dictionary<int, SopResolution>();

        var items = await db.Items.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.SopNumber })
            .ToDictionaryAsync(i => i.Id, i => (i.SopNumber ?? string.Empty).Trim(), cancellationToken);
        foreach (var id in itemIds)
            if (!items.ContainsKey(id))
                throw ItemNotFound(id);

        var keys = items.Values.Where(sop => sop.Length > 0).Select(sop => sop.ToUpperInvariant()).Distinct().ToArray();
        if (keys.Length == 0)
            return items.ToDictionary(item => item.Key, _ => new SopResolution(SopResolutionStatus.NoSopNumber));

        var masters = await db.DocumentMasters.AsNoTracking()
            .Where(m => m.RecordStatus == DocumentRecordStatus.Active && keys.Contains(m.CompanyDocumentCode.ToUpper()))
            .Select(m => new
            {
                m.Id,
                m.CompanyDocumentCode,
                Key = m.CompanyDocumentCode.ToUpper(),
                m.Title,
                TypeCode = m.DocumentType.Code,
                Revision = m.CurrentEffectiveRevision == null
                    || m.CurrentEffectiveRevision.RevisionStatus != DocumentRevisionStatus.Effective
                    ? null
                    : new
                    {
                        m.CurrentEffectiveRevision.Id,
                        m.CurrentEffectiveRevision.RevisionNumber,
                        m.CurrentEffectiveRevision.RevisionSequence,
                        m.CurrentEffectiveRevision.EffectiveDate,
                        PreviousId = m.Revisions
                            .Where(r => r.RevisionStatus == DocumentRevisionStatus.Superseded
                                && r.RevisionSequence < m.CurrentEffectiveRevision.RevisionSequence)
                            .OrderByDescending(r => r.RevisionSequence)
                            .Select(r => (int?)r.Id)
                            .FirstOrDefault(),
                        Pdf = m.CurrentEffectiveRevision.Files
                            .Where(f => f.FileRole == FileRole.ControlledPdf && f.IsActive)
                            .OrderByDescending(f => f.FileVersion)
                            .Select(f => new { f.Id, f.FileName })
                            .FirstOrDefault()
                    }
            }).ToListAsync(cancellationToken);

        var byKey = masters.ToLookup(master => master.Key, StringComparer.Ordinal);
        var results = new Dictionary<int, SopResolution>();
        foreach (var (id, sop) in items)
        {
            if (sop.Length == 0)
            {
                results.Add(id, new(SopResolutionStatus.NoSopNumber));
                continue;
            }

            var matches = byKey[sop.ToUpperInvariant()].ToArray();
            if (matches.Length == 0)
            {
                results.Add(id, new(SopResolutionStatus.NotFound, sop));
                continue;
            }

            // CONF-212 enforces case-insensitive uniqueness on DocumentMasters.CompanyDocumentCode
            // at the database level (and blocks installation if clashes exist), so Ambiguous can
            // no longer occur for new data; the branch is retained for defensive completeness.
            if (matches.Length > 1)
            {
                results.Add(id, new(SopResolutionStatus.Ambiguous, sop,
                    MatchingMasterCount: matches.Length,
                    MatchingCodes: matches.Select(m => m.CompanyDocumentCode).Order(StringComparer.Ordinal).ToArray()));
                continue;
            }

            var master = matches[0];
            var revision = master.Revision;
            results.Add(id, new(
                revision is null ? SopResolutionStatus.NoEffectiveRevision : SopResolutionStatus.Effective,
                sop, master.Id, master.CompanyDocumentCode, master.Title, master.TypeCode,
                revision?.Id, revision?.RevisionNumber, revision?.RevisionSequence, revision?.EffectiveDate,
                revision?.Pdf?.Id, revision?.Pdf?.FileName)
            {
                PreviousSupersededRevisionId = revision?.PreviousId
            });
        }

        return results;
    }

    internal async Task<string> GetSopNumberAsync(int itemId, CancellationToken cancellationToken)
    {
        var item = await db.Items.AsNoTracking()
            .Where(i => i.Id == itemId)
            .Select(i => new { i.SopNumber })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw ItemNotFound(itemId);
        return (item.SopNumber ?? string.Empty).Trim();
    }

    // Item STM preserves the original unordered first-match query, including
    // the original projection. Competency and revision capture use ResolveManyAsync.
    internal async Task<ItemStmResponse> ResolveForItemStmAsync(
        string sop, bool canDownload, CancellationToken cancellationToken)
    {
        var key = sop.ToUpperInvariant();
        var master = await db.DocumentMasters.AsNoTracking()
            .Where(m => m.RecordStatus == DocumentRecordStatus.Active && m.CompanyDocumentCode.ToUpper() == key)
            .Select(m => new
            {
                m.Id,
                m.CompanyDocumentCode,
                m.Title,
                TypeCode = m.DocumentType.Code,
                Revision = m.CurrentEffectiveRevision == null
                    || m.CurrentEffectiveRevision.RevisionStatus != DocumentRevisionStatus.Effective
                    ? null
                    : new
                    {
                        m.CurrentEffectiveRevision.RevisionNumber,
                        m.CurrentEffectiveRevision.EffectiveDate,
                        Pdf = m.CurrentEffectiveRevision.Files
                            .Where(f => f.FileRole == FileRole.ControlledPdf && f.IsActive)
                            .OrderByDescending(f => f.FileVersion)
                            .Select(f => new { f.Id, f.FileName })
                            .FirstOrDefault()
                    }
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (master == null)
            return new(ItemStmStatus.NotFound, sop, null, null, null, null, null, null, null, null, canDownload);

        if (master.Revision?.Pdf == null)
            return new(ItemStmStatus.NoEffectiveRevision, sop, master.Id, master.CompanyDocumentCode,
                master.Title, master.TypeCode, null, null, null, null, canDownload);

        return new(ItemStmStatus.Effective, sop, master.Id, master.CompanyDocumentCode,
            master.Title, master.TypeCode, master.Revision.RevisionNumber, master.Revision.EffectiveDate,
            master.Revision.Pdf.Id, master.Revision.Pdf.FileName, canDownload);
    }

    private static NotFoundException ItemNotFound(int itemId) => new(
        MessageCodes.MonitoringItemNotFound,
        $"Item {itemId} not found.",
        new Dictionary<string, string> { ["itemId"] = itemId.ToString(CultureInfo.InvariantCulture) });
}
