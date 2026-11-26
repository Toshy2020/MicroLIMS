using System.Globalization;
using System.Text;
using System.Text.Json;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Services.Competency;

public enum MatrixCellStatus { NotQualified, Qualified, ExpiringSoon, Expired, Withdrawn }

public sealed record MatrixCellDto(int UserId, int TestDefinitionId, MatrixCellStatus Status, DateOnly? ExpiresOn, int? QualificationId = null, uint? Version = null);

public sealed record MatrixSopDto(string DocumentCode, string RevisionNumber);

public sealed record MatrixTestDto(int TestDefinitionId, string Code, string DisplayName, MatrixSopDto? Sop);

public sealed record MatrixRowDto(int UserId, string AnalystName, IReadOnlyList<MatrixCellDto> Cells);

public sealed record MatrixFilters
{
    public string? AnalystSearch { get; init; }
    public string? TestSearch { get; init; }
    public MatrixCellStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = CompetencyMatrixService.DefaultPageSize;
}

public sealed record CompetencyMatrixDto(
    int SectionId, int Page, int PageSize, int TotalCount,
    IReadOnlyList<MatrixTestDto> Tests, IReadOnlyList<MatrixRowDto> Analysts);

// Analysts x tests. The latest qualification record by Id governs each cell; expiry and
// the "expiring soon" window use lab dates and the section's reminder-days setting.
public sealed class CompetencyMatrixService(
    IMicroLimsDbContext db,
    IUserSectionScopeService scope,
    ILabClock clock,
    PermissionService permissions,
    ItemSopResolver sopResolver,
    DataExportAuditService exportAudit)
{
    public const int DefaultPageSize = 50;

    public async Task<CompetencyMatrixDto> GetMatrixAsync(
        int sectionId, MatrixFilters filters, int actingUserId, CancellationToken cancellationToken = default)
    {
        await RequireViewAsync(actingUserId, cancellationToken);
        var readable = await scope.GetReadableSectionIdsAsync(actingUserId, cancellationToken);
        if (readable is not null && !readable.Contains(sectionId))
            throw SectionScopeDenied();
        // An analyst never sees colleagues' qualifications, even through the laboratory call.
        var isAnalyst = await db.Users.AsNoTracking()
            .AnyAsync(user => user.Id == actingUserId && user.Role != null && user.Role.Type == RoleType.Analyst, cancellationToken);
        return await BuildAsync([sectionId], filters, onlyUserId: isAnalyst ? actingUserId : null, pagedResult: true, cancellationToken);
    }

    public async Task<(string FileName, byte[] Bytes)> ExportCsvAsync(
        int sectionId, MatrixFilters filters, int actingUserId, CancellationToken cancellationToken = default)
    {
        await RequireViewAsync(actingUserId, cancellationToken);
        var readable = await scope.GetReadableSectionIdsAsync(actingUserId, cancellationToken);
        if (readable is not null && !readable.Contains(sectionId))
            throw SectionScopeDenied();
        var isAnalyst = await db.Users.AsNoTracking()
            .AnyAsync(user => user.Id == actingUserId && user.Role != null && user.Role.Type == RoleType.Analyst, cancellationToken);

        var matrix = await BuildAsync([sectionId], filters, onlyUserId: isAnalyst ? actingUserId : null, pagedResult: false, cancellationToken);
        var csv = BuildCsv(matrix);
        var filterJson = JsonSerializer.Serialize(new
        {
            sectionId,
            filters.AnalystSearch,
            filters.TestSearch,
            status = filters.Status?.ToString()
        });
        await exportAudit.LogExportAsync(actingUserId, filterJson, matrix.Analysts.Count, "CompetencyMatrixCsv");
        var fileName = $"competency-matrix-section-{sectionId}-{clock.LabToday:yyyyMMdd}.csv";
        return (fileName, Encoding.UTF8.GetBytes(csv));
    }

    // The acting user's own cells only: userId IS the caller, never a target chosen by the caller.
    public async Task<CompetencyMatrixDto> GetMyAsync(int userId, CancellationToken cancellationToken = default)
    {
        await RequireViewAsync(userId, cancellationToken);
        var sections = await scope.GetMemberSectionIdsAsync(userId, cancellationToken);
        if (sections.Count == 0)
            return new CompetencyMatrixDto(0, 1, DefaultPageSize, 0, [], []);
        return await BuildAsync(sections, new MatrixFilters(), onlyUserId: userId, pagedResult: true, cancellationToken);
    }

    private async Task<CompetencyMatrixDto> BuildAsync(
        IReadOnlyList<int> sectionIds, MatrixFilters filters, int? onlyUserId, bool pagedResult, CancellationToken cancellationToken)
    {
        var labToday = clock.LabToday;
        var (page, pageSize) = Paging.Normalize(filters.Page, filters.PageSize, DefaultPageSize);

        var reminderDays = await db.CompetencySectionSettings.AsNoTracking()
            .Where(setting => sectionIds.Contains(setting.SectionId))
            .Select(setting => new { setting.SectionId, setting.ReminderDaysBeforeExpiry })
            .ToDictionaryAsync(setting => setting.SectionId,
                setting => setting.ReminderDaysBeforeExpiry, cancellationToken);

        var tests = await db.TestDefinitions.AsNoTracking()
            .Where(test => sectionIds.Contains(test.SectionId))
            .OrderBy(test => test.DisplayName).ThenBy(test => test.Code)
            .Select(test => new { test.Id, test.Code, test.DisplayName, test.SectionId })
            .ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(filters.TestSearch))
        {
            var search = filters.TestSearch.Trim();
            tests = tests.Where(test =>
                    test.Code.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || test.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        var testIds = tests.Select(test => test.Id).ToArray();

        var qualifications = testIds.Length == 0
            ? []
            : await db.AnalystQualifications.AsNoTracking()
                .Where(q => testIds.Contains(q.TestDefinitionId) && (!onlyUserId.HasValue || q.UserId == onlyUserId.Value))
                .Select(q => new { q.Id, q.UserId, q.TestDefinitionId, q.ExpiresOn, q.WithdrawnOn, q.Version })
                .ToListAsync(cancellationToken);
        var latest = qualifications
            .GroupBy(q => new { q.UserId, q.TestDefinitionId })
            .ToDictionary(group => group.Key, group => group.MaxBy(q => q.Id)!);

        var departmentIds = await db.DocumentSections.AsNoTracking()
            .Where(section => sectionIds.Contains(section.Id))
            .Select(section => section.DepartmentId).Distinct().ToArrayAsync(cancellationToken);
        var memberIds = await db.Users.AsNoTracking()
            .Where(user => user.IsActive
                && user.Role != null
                && (user.Role.Type == RoleType.Analyst
                    || user.Role.Type == RoleType.Reviewer
                    || user.Role.Type == RoleType.SectionHead)
                && (!onlyUserId.HasValue || user.Id == onlyUserId.Value)
                && db.UserOrgMemberships.Any(membership => membership.UserId == user.Id
                    && ((membership.SectionId != null && sectionIds.Contains(membership.SectionId.Value))
                        || (membership.SectionId == null && departmentIds.Contains(membership.DepartmentId)))))
            .Select(user => user.Id).ToListAsync(cancellationToken);
        var assignedIds = await db.TestOrders.AsNoTracking()
            .Where(order => sectionIds.Contains(order.SectionId) && order.AssignedAnalystId != null
                && (!onlyUserId.HasValue || order.AssignedAnalystId == onlyUserId.Value))
            .Select(order => order.AssignedAnalystId!.Value).ToListAsync(cancellationToken);
        var analystIds = memberIds.Concat(qualifications.Select(q => q.UserId)).Concat(assignedIds)
            .Where(id => !onlyUserId.HasValue || id == onlyUserId.Value)
            .Distinct().ToArray();

        var names = analystIds.Length == 0
            ? new Dictionary<int, string>()
            : await db.Users.AsNoTracking().Where(user => analystIds.Contains(user.Id))
                .Select(user => new { user.Id, user.FullName })
                .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
        var ordered = analystIds
            .Where(id => names.ContainsKey(id))
            .OrderBy(id => names[id]).ThenBy(id => id).ToList();
        if (!string.IsNullOrWhiteSpace(filters.AnalystSearch))
        {
            var search = filters.AnalystSearch.Trim();
            ordered = ordered.Where(id => names[id].Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (filters.Status is { } wantedStatus)
        {
            ordered = ordered
                .Where(userId => tests.Any(test => Cell(userId, test.Id, test.SectionId).Status == wantedStatus))
                .ToList();
        }
        var totalCount = ordered.Count;
        var paged = pagedResult
            ? ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray()
            : ordered.ToArray();

        // SOP facts come only from actual test orders' items via the same resolver the guard uses.
        var orderItems = await db.TestOrders.AsNoTracking()
            .Where(order => sectionIds.Contains(order.SectionId)
                && order.Sample != null && order.Sample.ItemId != null)
            .Select(order => new { order.TestCode, ItemId = order.Sample!.ItemId!.Value })
            .Distinct().ToListAsync(cancellationToken);
        var itemIds = orderItems.Select(item => item.ItemId).Distinct().ToArray();
        IReadOnlyDictionary<int, SopResolution> resolved = itemIds.Length == 0
            ? new Dictionary<int, SopResolution>()
            : await sopResolver.ResolveManyAsync(itemIds, cancellationToken);
        var sopByCode = orderItems
            .Where(item => resolved.TryGetValue(item.ItemId, out var sop)
                && sop.Status == SopResolutionStatus.Effective)
            .GroupBy(item => item.TestCode)
            .ToDictionary(group => group.Key, group =>
            {
                var sop = resolved[group.First().ItemId];
                return new MatrixSopDto(sop.DocumentCode ?? string.Empty, sop.RevisionNumber ?? string.Empty);
            });

        var testDtos = tests.Select(test => new MatrixTestDto(test.Id, test.Code, test.DisplayName,
            sopByCode.GetValueOrDefault(test.Code))).ToList();
        var rows = paged.Select(userId => new MatrixRowDto(userId, names[userId],
            tests.Select(test => Cell(userId, test.Id, test.SectionId)).ToList())).ToList();

        return new CompetencyMatrixDto(sectionIds[0], page, pageSize, totalCount, testDtos, rows);

        MatrixCellDto Cell(int userId, int testId, int sectionId)
        {
            DateOnly? expiresOn = null;
            int? qualificationId = null;
            uint? version = null;
            MatrixCellStatus status;
            if (!latest.TryGetValue(new { UserId = userId, TestDefinitionId = testId }, out var qualification))
            {
                status = MatrixCellStatus.NotQualified;
            }
            else
            {
                qualificationId = qualification.Id;
                version = qualification.Version;
                expiresOn = qualification.ExpiresOn;
                if (qualification.WithdrawnOn is not null)
                    status = MatrixCellStatus.Withdrawn;
                else if (qualification.ExpiresOn is not { } expires)
                    status = MatrixCellStatus.Qualified;
                else if (labToday > expires)
                    status = MatrixCellStatus.Expired;
                else if (reminderDays.TryGetValue(sectionId, out var days) && days is int window
                    && expires <= labToday.AddDays(window))
                    status = MatrixCellStatus.ExpiringSoon;
                else
                    status = MatrixCellStatus.Qualified;
            }
            return new MatrixCellDto(userId, testId, status, expiresOn, qualificationId, version);
        }
    }

    private static string BuildCsv(CompetencyMatrixDto matrix)
    {
        var sb = new StringBuilder();
        var headers = new List<string> { "Analyst" };
        headers.AddRange(matrix.Tests.Select(t => $"{t.Code} ({t.DisplayName})"));
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

        foreach (var row in matrix.Analysts)
        {
            var cellsByTest = row.Cells.ToDictionary(c => c.TestDefinitionId);
            var values = new List<string> { row.AnalystName };
            foreach (var test in matrix.Tests)
            {
                if (!cellsByTest.TryGetValue(test.TestDefinitionId, out var cell))
                {
                    values.Add(MatrixCellStatus.NotQualified.ToString());
                }
                else if (cell.ExpiresOn is { } expires)
                {
                    values.Add($"{cell.Status} ({expires.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})");
                }
                else
                {
                    values.Add(cell.Status.ToString());
                }
            }
            sb.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@')
            text = "'" + text;
        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }

    private async Task RequireViewAsync(int actingUserId, CancellationToken cancellationToken)
    {
        if (!await permissions.UserHasPermissionAsync(actingUserId, PermissionConstants.CompetencyView, cancellationToken))
            throw new CodedAccessDeniedException(MessageCodes.CompetencyViewRequired,
                "You do not have permission to view analyst qualifications.");
    }

    private static CodedAccessDeniedException SectionScopeDenied() =>
        new(MessageCodes.CompetencySectionScopeDenied,
            "You do not have access to competency records for this laboratory section.");
}
