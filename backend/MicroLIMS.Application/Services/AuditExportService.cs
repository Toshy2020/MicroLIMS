using System.Globalization;
using System.Text.Json;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// What an export will contain, fixed before any byte is streamed.
public record AuditExportPlan(int RowCount, bool Truncated, int? UpperIdExclusive, AuditSearchRequest Filter);

// CSV export of the audit trail for the current filter. The export itself is
// an audit event (who, filter, row count) written BEFORE the file is streamed,
// so an aborted download still leaves its trace.
public class AuditExportService
{
    // Hard ceiling per export. A larger result is cut at the newest rows and the CSV says so.
    public const int MaxExportRows = 50_000;
    private const int PageSize = 1000;
    public const string ExportActionCode = "AuditExported";

    private readonly AuditSearchService _search;
    private readonly IAuditEventService _audit;

    public AuditExportService(AuditSearchService search, IAuditEventService audit)
    {
        _search = search;
        _audit = audit;
    }

    public async Task<AuditExportPlan> PrepareAsync(AuditSearchRequest request, CancellationToken ct = default)
    {
        // The cursor and page size are paging concerns, not part of the export's filter.
        var filter = request with { BeforeId = null, Take = PageSize };
        var total = await _search.CountAsync(filter);
        var rowCount = Math.Min(total, MaxExportRows);
        var truncated = total > MaxExportRows;

        // Freeze the upper bound so rows appended while streaming do not change the count we recorded.
        int? upper = null;
        if (total > 0)
        {
            var newest = await _search.ReadPageForExportAsync(filter, 1);
            upper = newest.Count > 0 ? newest[0].Id + 1 : null;
        }

        var filterJson = JsonSerializer.Serialize(new
        {
            filter.FromDate, filter.ToDate, filter.BatchNumber, filter.ControlNumber, filter.MediaLotNumber,
            filter.SampleReferenceNumber, filter.ReferenceStrainCode, filter.CryovialCode, filter.SampleId,
            filter.TestOrderId, filter.UserId, filter.EntityName, filter.Action,
            ActionCategory = filter.ActionCategory?.ToString(), ActorType = filter.ActorType?.ToString(),
            filter.CorrelationId, filter.Reason, filter.NegativeEntityId
        });

        await _audit.RecordUserEventAsync(
            ExportActionCode, AuditActionCategory.ImportExport, "AuditLog",
            reason: $"Audit trail CSV export: {rowCount} row(s){(truncated ? $" (truncated at {MaxExportRows} of {total})" : "")}",
            sourceContext: "AuditExport",
            changes: new[]
            {
                new AuditFieldChange("Filter", null, filterJson),
                new AuditFieldChange("RowCount", null, rowCount.ToString(CultureInfo.InvariantCulture)),
                new AuditFieldChange("Truncated", null, truncated ? "true" : "false")
            },
            entityId: "export",
            cancellationToken: ct);

        return new AuditExportPlan(rowCount, truncated, upper, filter);
    }

    private static readonly string[] Header =
    {
        "Id", "Timestamp (UTC)", "EntityName", "EntityId", "Action", "ActionCategory", "ActorType", "UserId", "User",
        "UserCurrentRole", "Reason", "CorrelationId", "SampleReferenceNumber", "BatchNumber", "ControlNumber",
        "MediaLotNumber", "ReferenceStrainCode", "CryovialCode", "SampleId", "TestOrderId",
        "PreviousValue", "NewValue", "EntityIdNegative", "CorrectedByAuditLogId"
    };

    public async Task WriteCsvAsync(AuditExportPlan plan, TextWriter writer, CancellationToken ct = default)
    {
        await writer.WriteLineAsync(string.Join(",", Header));
        var written = 0;
        var cursor = plan.UpperIdExclusive;
        while (written < plan.RowCount && cursor is not null)
        {
            ct.ThrowIfCancellationRequested();
            var page = await _search.ReadPageForExportAsync(plan.Filter with { BeforeId = cursor }, Math.Min(PageSize, plan.RowCount - written));
            if (page.Count == 0) break;
            foreach (var r in page)
            {
                await writer.WriteLineAsync(string.Join(",", new[]
                {
                    r.Id.ToString(CultureInfo.InvariantCulture),
                    Cell(r.Timestamp.ToString("o", CultureInfo.InvariantCulture)),
                    Cell(r.EntityName), Cell(r.EntityId), Cell(r.Action),
                    Cell(r.ActionCategory?.ToString()), Cell(r.ActorType?.ToString()),
                    r.UserId.ToString(CultureInfo.InvariantCulture), Cell(r.UserName), Cell(r.UserCurrentRole),
                    Cell(r.Reason), Cell(r.CorrelationId?.ToString()),
                    Cell(r.SampleReferenceNumber), Cell(r.BatchNumber), Cell(r.ControlNumber), Cell(r.MediaLotNumber),
                    Cell(r.ReferenceStrainCode), Cell(r.CryovialCode),
                    r.SampleId?.ToString(CultureInfo.InvariantCulture) ?? "", r.TestOrderId?.ToString(CultureInfo.InvariantCulture) ?? "",
                    Cell(r.PreviousValue), Cell(r.NewValue),
                    r.EntityIdNegative ? "true" : "false", r.CorrectedByAuditLogId?.ToString(CultureInfo.InvariantCulture) ?? ""
                }));
                written++;
            }
            cursor = page[^1].Id;
        }
        if (plan.Truncated)
            await writer.WriteLineAsync(Cell($"# Export truncated at the newest {MaxExportRows} rows; narrow the filter to see the rest."));
    }

    // CSV-quotes a text cell and neutralises spreadsheet formulas: a cell that
    // starts with = + - @ (or a tab / carriage return) gets a leading apostrophe.
    public static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || value[0] == ' ')
            value = "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
