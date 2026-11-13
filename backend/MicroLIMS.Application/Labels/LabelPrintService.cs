using System.Globalization;
using System.Text;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Labels;

public sealed record PrintLabelItem(LabelEntityType EntityType, int EntityId, int Copies);

public sealed record PrintLabelsRequest(IReadOnlyList<PrintLabelItem> Items, string? Reason);

public sealed record LabelPrintResult(byte[] Pdf, string FileName, Guid BatchId);

public sealed record LabelPrintStatusDto(
    LabelEntityType EntityType,
    int EntityId,
    bool CanPrint,
    string? BlockedReason,
    int PrintCount,
    int DefaultCopies);

public interface ILabelPrintService
{
    Task<LabelPrintResult> PrintAsync(
        PrintLabelsRequest request,
        int userId,
        IReadOnlySet<string> permissions,
        CancellationToken ct);

    Task<IReadOnlyList<LabelPrintStatusDto>> GetStatusAsync(
        IReadOnlyList<(LabelEntityType Type, int Id)> records,
        int userId,
        CancellationToken ct);
}

/// <summary>
/// Validates label print requests, enforces record-type permissions and reprint rules,
/// records append-only print logs with explicit labelling audit events, and renders PDFs.
/// </summary>
public sealed class LabelPrintService(
    IMicroLimsDbContext db,
    ILabelDataSource dataSource,
    ILabelRenderer renderer,
    IAuditEventService audit,
    TimeProvider time,
    ILabClock clock) : ILabelPrintService
{
    private const int MinItems = 1;
    private const int MaxItems = 100;
    private const int MinCopies = 1;
    private const int MaxCopies = 50;
    private const int MinReprintReasonLength = 10;
    private const int MaxReasonLength = 500;
    private const string PrintNumberUniqueIndexName = "IX_LabelPrintLogs_EntityType_EntityId_PrintNumber";

    private readonly IMicroLimsDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly ILabelDataSource _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    private readonly ILabelRenderer _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    private readonly IAuditEventService _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private readonly ILabClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    private sealed record ValidatedPrintItem(
        PrintLabelItem Request,
        LabelRecord Record,
        string Code,
        LabelContent Content,
        LabelFormat Format);

    public async Task<LabelPrintResult> PrintAsync(
        PrintLabelsRequest request,
        int userId,
        IReadOnlySet<string> permissions,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(permissions);

        // 1. Validate shape, duplicates, and per-item copy counts.
        ValidateRequestItems(request.Items);

        // 2. Load every record via ILabelDataSource; missing or out-of-scope -> NotFound; Code == null -> BusinessRule.
        var loaded = new List<LabelRecord>(request.Items.Count);
        foreach (var item in request.Items)
        {
            var record = await _dataSource.FindByIdAsync(item.EntityType, item.EntityId, userId, ct)
                ?? throw new NotFoundException($"{item.EntityType} #{item.EntityId} was not found.");
            loaded.Add(record);
        }

        var uncoded = loaded.FirstOrDefault(r => string.IsNullOrWhiteSpace(r.Code) || r.Content is null);
        if (uncoded is not null)
        {
            throw new BusinessRuleException(
                $"{uncoded.Type} #{uncoded.Id} ({uncoded.DisplayName}) cannot be printed: {uncoded.BlockedReason ?? "no code has been assigned yet."}");
        }

        // 3 & 4. Count existing logs, check per-item CreatePermission (or Labels.Reprint), and enforce reprint rules.
        var initialCounts = await LoadPrintCountsAsync(request.Items, ct);
        var hasReprint = request.Items.Any(i => initialCounts[(i.EntityType, i.EntityId)] > 0);

        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var record = loaded[i];
            var isItemReprint = initialCounts[(item.EntityType, item.EntityId)] > 0;
            var hasCreate = HasCreatePermission(permissions, record);

            if (!isItemReprint && !hasCreate)
            {
                throw new UnauthorizedAccessException(
                    $"Missing permission '{record.CreatePermission}' to print {record.Type} labels.");
            }

            if (isItemReprint && !hasCreate && !permissions.Contains(PermissionConstants.LabelsReprint))
            {
                throw new UnauthorizedAccessException(
                    $"Missing permission '{PermissionConstants.LabelsReprint}' to reprint labels.");
            }
        }

        string? trimmedReason;
        if (hasReprint)
        {
            if (!permissions.Contains(PermissionConstants.LabelsReprint))
            {
                throw new UnauthorizedAccessException(
                    $"Missing permission '{PermissionConstants.LabelsReprint}' to reprint labels.");
            }

            trimmedReason = request.Reason?.Trim();
            if (string.IsNullOrEmpty(trimmedReason) || trimmedReason.Length < MinReprintReasonLength)
            {
                throw new BusinessRuleException(
                    $"A reprint reason of at least {MinReprintReasonLength} characters is required.");
            }

            if (trimmedReason.Length > MaxReasonLength)
            {
                throw new BusinessRuleException($"Reason cannot exceed {MaxReasonLength} characters.");
            }
        }
        else
        {
            trimmedReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            if (trimmedReason is not null && trimmedReason.Length > MaxReasonLength)
            {
                throw new BusinessRuleException($"Reason cannot exceed {MaxReasonLength} characters.");
            }
        }

        var entityTypes = request.Items.Select(i => i.EntityType).Distinct().ToList();
        var formats = await _db.LabelFormats
            .AsNoTracking()
            .Where(f => entityTypes.Contains(f.EntityType))
            .ToDictionaryAsync(f => f.EntityType, ct);

        var validatedItems = new List<ValidatedPrintItem>(request.Items.Count);
        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var record = loaded[i];
            if (!formats.TryGetValue(item.EntityType, out var format))
            {
                throw new NotFoundException($"Label format for {item.EntityType} was not found.");
            }

            // Validated in step 2 above.
            if (record.Code is null || record.Content is null)
            {
                throw new BusinessRuleException(
                    $"{record.Type} #{record.Id} ({record.DisplayName}) cannot be printed: no code has been assigned yet.");
            }

            validatedItems.Add(new ValidatedPrintItem(item, record, record.Code, record.Content, format));
        }

        // 7 (pre-check). Dry render before any DB write so LabelDoesNotFitException aborts cleanly.
        var printDate = _clock.LabToday;
        var dryPdf = _renderer.Render(BuildRenderItems(validatedItems, initialCounts, printDate));

        // 5 & 6. Persist print log rows and explicit audit events; retry once on print-number unique-index collision.
        var batchId = Guid.NewGuid();
        var printedAt = _time.GetUtcNow().UtcDateTime;
        var activeCounts = initialCounts;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var ownsTransaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction is null;
            await using var tx = ownsTransaction ? await _db.Database.BeginTransactionAsync(ct) : null;
            var addedLogs = new List<LabelPrintLog>(validatedItems.Count);

            try
            {
                _db.CurrentUserId = userId;

                foreach (var entry in validatedItems)
                {
                    var existingCount = activeCounts[(entry.Request.EntityType, entry.Request.EntityId)];
                    var printNumber = existingCount + 1;
                    var isReprint = existingCount > 0;

                    var log = new LabelPrintLog
                    {
                        EntityType = entry.Request.EntityType,
                        EntityId = entry.Request.EntityId,
                        Code = entry.Code,
                        PrintNumber = printNumber,
                        IsReprint = isReprint,
                        Copies = entry.Request.Copies,
                        LabelFormatId = entry.Format.Id,
                        LabelFormatVersion = entry.Format.Version,
                        Reason = trimmedReason,
                        PrintedByUserId = userId,
                        PrintedAt = printedAt,
                        BatchId = batchId
                    };
                    addedLogs.Add(log);
                    _db.LabelPrintLogs.Add(log);
                }

                // Write explicit labelling audit events (the first call saves all pending LabelPrintLog rows too).
                foreach (var entry in validatedItems)
                {
                    var existingCount = activeCounts[(entry.Request.EntityType, entry.Request.EntityId)];
                    var printNumber = existingCount + 1;
                    var isReprint = existingCount > 0;

                    await _audit.RecordUserEventAsync(
                        actionCode: isReprint ? "LABEL_REPRINTED" : "LABEL_PRINTED",
                        actionCategory: AuditActionCategory.Labelling,
                        recordType: entry.Request.EntityType.ToString(),
                        reason: trimmedReason,
                        correlationId: batchId,
                        changes: new AuditFieldChange[]
                        {
                            new("Code", null, entry.Code),
                            new("Copies", null, entry.Request.Copies.ToString(CultureInfo.InvariantCulture)),
                            new("PrintNumber", null, printNumber.ToString(CultureInfo.InvariantCulture)),
                            new("LabelFormatVersion", null, entry.Format.Version.ToString(CultureInfo.InvariantCulture))
                        },
                        entityId: entry.Request.EntityId.ToString(CultureInfo.InvariantCulture),
                        cancellationToken: ct,
                        sampleId: entry.Record.SampleId);
                }

                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }

                break;
            }
            catch (DbUpdateException ex) when (IsPrintNumberUniqueViolation(ex))
            {
                if (tx is not null)
                {
                    await tx.RollbackAsync(ct);
                }

                DetachPendingEntries(addedLogs);

                if (attempt == 2 || !ownsTransaction)
                {
                    throw new DbUpdateConcurrencyException(
                        "A concurrent label print claimed the next print number. Please try again.",
                        ex);
                }

                activeCounts = await LoadPrintCountsAsync(request.Items, ct);

                var needsReprintAfterRace = activeCounts.Values.Any(c => c > 0);
                if (needsReprintAfterRace &&
                    (!permissions.Contains(PermissionConstants.LabelsReprint) ||
                     string.IsNullOrEmpty(trimmedReason) ||
                     trimmedReason.Length < MinReprintReasonLength))
                {
                    throw new DbUpdateConcurrencyException(
                        "A concurrent label print claimed the next print number. Please try again.",
                        ex);
                }
            }
            catch
            {
                if (tx is not null)
                {
                    await tx.RollbackAsync(ct);
                }

                DetachPendingEntries(addedLogs);
                throw;
            }
        }

        // 7 (final). Reuse dry-render bytes only if print numbers stayed identical; otherwise re-render outside tx.
        var printNumbersChanged = validatedItems.Any(v =>
            activeCounts[(v.Request.EntityType, v.Request.EntityId)] !=
            initialCounts[(v.Request.EntityType, v.Request.EntityId)]);

        var finalPdf = printNumbersChanged
            ? _renderer.Render(BuildRenderItems(validatedItems, activeCounts, printDate))
            : dryPdf;

        var fileName = validatedItems.Count == 1
            ? $"label-{LabelPayload.PrefixOf(validatedItems[0].Request.EntityType)}-{SanitizeFileName(validatedItems[0].Code)}.pdf"
            : $"labels-{batchId:N}.pdf";

        return new LabelPrintResult(finalPdf, fileName, batchId);
    }

    public async Task<IReadOnlyList<LabelPrintStatusDto>> GetStatusAsync(
        IReadOnlyList<(LabelEntityType Type, int Id)> records,
        int userId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
        {
            return Array.Empty<LabelPrintStatusDto>();
        }

        if (records.Count > MaxItems)
        {
            throw new BusinessRuleException($"Cannot query status for more than {MaxItems} records at once.");
        }

        var loaded = new List<LabelRecord>(records.Count);
        foreach (var (type, id) in records)
        {
            var record = await _dataSource.FindByIdAsync(type, id, userId, ct)
                ?? throw new NotFoundException($"{type} #{id} was not found.");
            loaded.Add(record);
        }

        var items = records.Select(r => new PrintLabelItem(r.Type, r.Id, 1)).ToList();
        var counts = await LoadPrintCountsAsync(items, ct);

        var entityTypes = records.Select(r => r.Type).Distinct().ToList();
        var defaultCopiesByType = await _db.LabelFormats
            .AsNoTracking()
            .Where(f => entityTypes.Contains(f.EntityType))
            .ToDictionaryAsync(f => f.EntityType, f => f.DefaultCopies, ct);

        var result = new List<LabelPrintStatusDto>(records.Count);
        for (var i = 0; i < records.Count; i++)
        {
            var (type, id) = records[i];
            var record = loaded[i];
            var canPrint = !string.IsNullOrWhiteSpace(record.Code) && record.Content is not null;
            var blockedReason = canPrint
                ? null
                : record.BlockedReason ?? "Record does not have a code yet.";
            var printCount = counts[(type, id)];
            var defaultCopies = defaultCopiesByType.TryGetValue(type, out var copies) ? copies : 1;

            result.Add(new LabelPrintStatusDto(type, id, canPrint, blockedReason, printCount, defaultCopies));
        }

        return result;
    }

    private static void ValidateRequestItems(IReadOnlyList<PrintLabelItem>? items)
    {
        if (items is null || items.Count < MinItems || items.Count > MaxItems)
        {
            throw new BusinessRuleException(
                $"Items: Print request must contain between {MinItems} and {MaxItems} items.");
        }

        var errors = new List<string>();
        var seen = new HashSet<(LabelEntityType Type, int Id)>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!Enum.IsDefined(item.EntityType))
            {
                errors.Add($"Items[{i}].EntityType: Invalid label entity type.");
            }

            if (item.EntityId <= 0)
            {
                errors.Add($"Items[{i}].EntityId: EntityId must be positive.");
            }

            if (item.Copies < MinCopies || item.Copies > MaxCopies)
            {
                errors.Add($"Items[{i}].Copies: Copies must be between {MinCopies} and {MaxCopies}.");
            }

            if (!seen.Add((item.EntityType, item.EntityId)))
            {
                errors.Add($"Items[{i}]: Duplicate record ({item.EntityType} #{item.EntityId}).");
            }
        }

        if (errors.Count > 0)
        {
            throw new BusinessRuleException(string.Join(" ", errors));
        }
    }

    private static bool HasCreatePermission(IReadOnlySet<string> permissions, LabelRecord record) =>
        permissions.Contains(record.CreatePermission) ||
        (record.Type == LabelEntityType.Sample && permissions.Contains(PermissionConstants.SamplesReceive));

    private async Task<Dictionary<(LabelEntityType Type, int Id), int>> LoadPrintCountsAsync(
        IReadOnlyList<PrintLabelItem> items,
        CancellationToken ct)
    {
        var counts = new Dictionary<(LabelEntityType Type, int Id), int>(items.Count);
        foreach (var item in items)
        {
            counts[(item.EntityType, item.EntityId)] = 0;
        }

        foreach (var group in items.GroupBy(i => i.EntityType))
        {
            var entityType = group.Key;
            var ids = group.Select(i => i.EntityId).Distinct().ToList();

            var rows = await _db.LabelPrintLogs
                .AsNoTracking()
                .Where(l => l.EntityType == entityType && ids.Contains(l.EntityId))
                .GroupBy(l => l.EntityId)
                .Select(g => new { EntityId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                counts[(entityType, row.EntityId)] = row.Count;
            }
        }

        return counts;
    }

    private static IReadOnlyList<LabelRenderItem> BuildRenderItems(
        IReadOnlyList<ValidatedPrintItem> items,
        IReadOnlyDictionary<(LabelEntityType Type, int Id), int> counts,
        DateOnly printDate)
    {
        var renderItems = new List<LabelRenderItem>(items.Count);
        foreach (var item in items)
        {
            var printNumber = counts[(item.Request.EntityType, item.Request.EntityId)] + 1;
            renderItems.Add(new LabelRenderItem(
                item.Content,
                item.Format.WidthMm,
                item.Format.HeightMm,
                item.Format.Layout,
                item.Request.Copies,
                printNumber,
                printDate));
        }

        return renderItems;
    }

    private void DetachPendingEntries(IReadOnlyList<LabelPrintLog> addedLogs)
    {
        foreach (var entry in _db.ChangeTracker.Entries<AuditEventChange>()
                     .Where(e => e.State == EntityState.Added)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in _db.ChangeTracker.Entries<AuditLog>()
                     .Where(e => e.State == EntityState.Added)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var log in addedLogs)
        {
            var entry = _db.Entry(log);
            if (entry.State != EntityState.Detached)
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private static bool IsPrintNumberUniqueViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains(PrintNumberUniqueIndexName, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileName(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' ? ch : '-');
        }

        return sb.Length == 0 ? "label" : sb.ToString();
    }
}
