using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record ResultRecordSearchRequest(
    string? Search,
    SampleCategory? Category,
    string? TestCode,
    ResultLevel? ResultLevel,
    SampleStatus? SampleStatus,
    string? ApprovalStatus,
    DateTime? FromDate,
    DateTime? ToDate,
    string? SubjectName,
    ResultKind? ResultKind,
    int Page = 1,
    int PageSize = 25,
    string SortBy = "ResultEnteredAt",
    bool SortDescending = true);

public record ResultRecordSearchResult(List<ResultRecord> Items, int TotalCount, int Page, int PageSize);

public record TestCodeOption(string TestCode, string TestDisplayName);

public record FilterOptionsResult(List<SampleCategory> Categories, List<TestCodeOption> TestCodes, List<string> SubjectNames, List<string> Units);

public record ExportQueryResult(List<ResultRecord> Items, int TotalCount, bool Exceeded);

public record TrendPoint(
    int RecordId, string ReferenceNumber, DateTime Date, decimal? NumericValue, string ReportedValue, bool IsBelowDetectionLimit,
    decimal? DetectionLimit, ResultLevel ResultLevel, string? AlertLimit, string? ActionLimit, string? SpecLimit);

public record TrendStatistics(int Count, decimal? Latest, decimal? Mean, decimal? StandardDeviation, decimal? Min, decimal? Max, int ImputedPointCount);

public record TrendResult(string TestCode, string TestDisplayName, string SubjectName, string? Unit, List<TrendPoint> Points, TrendStatistics Statistics);

public record OverviewCategoryItem(SampleCategory Category, int Count, int Percentage);
public record OverviewTestItem(string TestCode, string TestName, int Count);
public record OverviewLocationItem(string Location, int Count, int Percentage);
public record OverviewRecentResultItem(
    int Id, string ReferenceNumber, string SubjectName, string? SubjectDetail,
    SampleCategory Category, string TestCode, string TestDisplayName,
    DateTime ResultEnteredAt, string ResultEnteredByName, SampleStatus SampleStatus,
    string ApprovalStatus);

public record OverviewAggregateResult(
    int TotalTests,
    int ApprovedCount,
    int PendingReviewCount,
    int PendingApprovalCount,
    int OutOfSpecCount,
    int AlertActionCount,
    List<OverviewCategoryItem> CategoryDistribution,
    List<OverviewTestItem> TestDistribution,
    List<OverviewLocationItem> LocationDistribution,
    List<OverviewRecentResultItem> RecentResults);

public record QualitativeEventItem(
    int Id, string ReferenceNumber, SampleCategory Category,
    string SubjectName, string? SubjectDetail,
    string TestCode, string TestDisplayName,
    string ReportedValue, DateTime ResultEnteredAt,
    string ResultEnteredByName, SampleStatus SampleStatus,
    string ApprovalStatus, string? ApprovedByName, DateTime? ApprovedAt);

public record QualitativeEventResult(
    string TestCode, string TestDisplayName,
    List<QualitativeEventItem> Events);

// One calendar month, with counts for that month this year and the same
// month one year earlier - the year-over-year comparison the KPI page's
// "Tests Completed by Month" chart renders.
public record MonthlyCompletionPoint(string Month, int PriorYearCount, int CurrentYearCount);

// One product/item or location/point's aggregate stats for a single test
// code - the Quick Compare dialog's table row. MeanValue is populated only
// when IsNumeric; PercentDetected only when it's not - callers should read
// the field matching CompareResult.IsNumeric rather than both.
public record CompareSubjectStat(
    string SubjectName, int TestsEvaluated, decimal? MeanValue, double? PercentDetected,
    int AlertActionCount, int OosCount, double CompliancePercent);

public record CompareResult(string TestCode, string TestDisplayName, bool IsNumeric, List<CompareSubjectStat> Subjects);

// Read-only query layer over the ResultRecord flattened projection - the
// data source ReportingController's search/trend endpoints (and,
// eventually, the Reports module) read from. Kept separate from
// ResultProjectionService, which only ever writes the projection.
public class ReportingQueryService
{
    private readonly MicroLimsDbContext _db;

    public ReportingQueryService(MicroLimsDbContext db)
    {
        _db = db;
    }

    // "Approved"/"Pending"/"Rejected", derived from SampleStatus rather
    // than stored - Received/InTesting/UnderReview/UnderApproval/
    // RetestRequested are all still "Pending" from a reporting standpoint.
    public static string DeriveApprovalStatus(SampleStatus status) => status switch
    {
        SampleStatus.Approved => "Approved",
        SampleStatus.Rejected => "Rejected",
        _ => "Pending"
    };

    public async Task<ResultRecordSearchResult> SearchAsync(ResultRecordSearchRequest request)
    {
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 1, 200);
        var page = request.Page <= 0 ? 1 : request.Page;

        var query = ApplySort(BuildFilteredQuery(request), request);

        var totalCount = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new ResultRecordSearchResult(items, totalCount, page, pageSize);
    }

    // Values that actually appear in ResultRecords - not the full master
    // data lists - so a filter dropdown never offers a choice that comes
    // back with zero rows.
    public async Task<FilterOptionsResult> GetFilterOptionsAsync()
    {
        var categories = await _db.ResultRecords.Select(r => r.Category).Distinct().OrderBy(c => c).ToListAsync();

        var testCodes = await _db.ResultRecords
            .Select(r => new { r.TestCode, r.TestDisplayName })
            .Distinct()
            .OrderBy(t => t.TestCode)
            .ToListAsync();

        var subjectNames = await _db.ResultRecords.Select(r => r.SubjectName).Distinct().OrderBy(s => s).ToListAsync();
        var units = await _db.ResultRecords.Where(r => r.Unit != null).Select(r => r.Unit!).Distinct().OrderBy(u => u).ToListAsync();

        return new FilterOptionsResult(
            categories,
            testCodes.Select(t => new TestCodeOption(t.TestCode, t.TestDisplayName)).ToList(),
            subjectNames,
            units);
    }

    // Every matching row (ignoring paging) for CSV export, capped so a
    // too-broad filter can't be used to pull an unbounded dump - the
    // caller (ReportingController) must narrow its filters instead of
    // silently getting a truncated file.
    public async Task<ExportQueryResult> GetForExportAsync(ResultRecordSearchRequest request, int maxRows)
    {
        var query = ApplySort(BuildFilteredQuery(request), request);

        var totalCount = await query.CountAsync();
        if (totalCount > maxRows)
            return new ExportQueryResult(new List<ResultRecord>(), totalCount, Exceeded: true);

        var items = await query.ToListAsync();
        return new ExportQueryResult(items, totalCount, Exceeded: false);
    }

    private IQueryable<ResultRecord> BuildFilteredQuery(ResultRecordSearchRequest request)
    {
        var query = _db.ResultRecords.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(r =>
                r.ReferenceNumber.ToLower().Contains(term) ||
                r.SubjectName.ToLower().Contains(term) ||
                (r.BatchNumber != null && r.BatchNumber.ToLower().Contains(term)) ||
                (r.ControlNumber != null && r.ControlNumber.ToLower().Contains(term)));
        }

        if (request.Category is not null) query = query.Where(r => r.Category == request.Category);
        if (!string.IsNullOrWhiteSpace(request.TestCode)) query = query.Where(r => r.TestCode == request.TestCode);
        if (request.ResultLevel is not null) query = query.Where(r => r.ResultLevel == request.ResultLevel);
        if (request.SampleStatus is not null) query = query.Where(r => r.SampleStatus == request.SampleStatus);
        if (request.FromDate is not null) query = query.Where(r => r.ResultEnteredAt >= request.FromDate);
        if (request.ToDate is not null) query = query.Where(r => r.ResultEnteredAt <= request.ToDate);
        if (!string.IsNullOrWhiteSpace(request.SubjectName)) query = query.Where(r => r.SubjectName == request.SubjectName);
        if (request.ResultKind is not null) query = query.Where(r => r.ResultKind == request.ResultKind);

        // Derived, not stored - filter by mapping the requested bucket back
        // onto the SampleStatus values that fall into it.
        if (!string.IsNullOrWhiteSpace(request.ApprovalStatus))
        {
            var wantedStatuses = request.ApprovalStatus switch
            {
                "Approved" => new[] { SampleStatus.Approved },
                "Rejected" => new[] { SampleStatus.Rejected },
                "Pending" => new[]
                {
                    SampleStatus.Received, SampleStatus.InTesting,
                    SampleStatus.UnderReview, SampleStatus.UnderApproval,
                    SampleStatus.RetestRequested
                },
                _ => Array.Empty<SampleStatus>()
            };
            if (wantedStatuses.Length > 0)
                query = query.Where(r => wantedStatuses.Contains(r.SampleStatus));
        }

        return query;
    }

    private static IQueryable<ResultRecord> ApplySort(IQueryable<ResultRecord> query, ResultRecordSearchRequest request) =>
        request.SortBy switch
        {
            "ReferenceNumber" => request.SortDescending ? query.OrderByDescending(r => r.ReferenceNumber) : query.OrderBy(r => r.ReferenceNumber),
            "SubjectName" => request.SortDescending ? query.OrderByDescending(r => r.SubjectName) : query.OrderBy(r => r.SubjectName),
            "TestCode" => request.SortDescending ? query.OrderByDescending(r => r.TestCode) : query.OrderBy(r => r.TestCode),
            "Category" => request.SortDescending ? query.OrderByDescending(r => r.Category) : query.OrderBy(r => r.Category),
            "ResultLevel" => request.SortDescending ? query.OrderByDescending(r => r.ResultLevel) : query.OrderBy(r => r.ResultLevel),
            "SampleStatus" => request.SortDescending ? query.OrderByDescending(r => r.SampleStatus) : query.OrderBy(r => r.SampleStatus),
            "NumericValue" => request.SortDescending ? query.OrderByDescending(r => r.NumericValue) : query.OrderBy(r => r.NumericValue),
            _ => request.SortDescending ? query.OrderByDescending(r => r.ResultEnteredAt) : query.OrderBy(r => r.ResultEnteredAt)
        };

    public async Task<TrendResult> GetTrendAsync(string testCode, string subjectName, DateTime? fromDate, DateTime? toDate)
    {
        if (string.IsNullOrWhiteSpace(testCode)) throw new InvalidOperationException("testCode is required.");
        if (string.IsNullOrWhiteSpace(subjectName)) throw new InvalidOperationException("subjectName is required.");

        var testDefinition = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Code == testCode)
            ?? throw new InvalidOperationException($"Test code \"{testCode}\" is not configured in Test Master.");

        // Enforced here, server-side, rather than left to the UI to avoid
        // requesting it - a trend chart over Detected/Absent has no meaning.
        if (testDefinition.WorkflowType != WorkflowType.CountTest)
            throw new InvalidOperationException($"Trending is only available for numeric results. {testCode} produces qualitative results.");

        var query = _db.ResultRecords.Where(r => r.TestCode == testCode && r.SubjectName == subjectName);
        if (fromDate is not null) query = query.Where(r => r.ResultEnteredAt >= fromDate);
        if (toDate is not null) query = query.Where(r => r.ResultEnteredAt <= toDate);

        var records = await query.OrderBy(r => r.ResultEnteredAt).ToListAsync();

        var points = records.Select(r => new TrendPoint(
            r.Id, r.ReferenceNumber, r.ResultEnteredAt, r.NumericValue, r.ReportedValue, r.IsBelowDetectionLimit,
            r.DetectionLimit, r.ResultLevel, r.AlertLimit, r.ActionLimit, r.SpecLimit)).ToList();

        // Imputation (substituting DetectionLimit/2 for a below-detection-
        // limit point) happens only here, at query time - the projection
        // itself always stores the real calculated NumericValue.
        var imputedValues = points
            .Select(Impute)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
        var imputedPointCount = points.Count(p => p.IsBelowDetectionLimit);

        var latest = points.Count > 0 ? Impute(points[^1]) : null;

        var statistics = new TrendStatistics(
            Count: imputedValues.Count,
            Latest: latest,
            Mean: imputedValues.Count > 0 ? imputedValues.Average() : null,
            StandardDeviation: StandardDeviation(imputedValues),
            Min: imputedValues.Count > 0 ? imputedValues.Min() : null,
            Max: imputedValues.Count > 0 ? imputedValues.Max() : null,
            ImputedPointCount: imputedPointCount);

        var unit = records.Select(r => r.Unit).FirstOrDefault(u => u is not null);

        return new TrendResult(testCode, testDefinition.DisplayName, subjectName, unit, points, statistics);
    }

    public async Task<ResultRecord?> GetByIdAsync(int id) =>
        await _db.ResultRecords.FirstOrDefaultAsync(r => r.Id == id);

    public async Task<OverviewAggregateResult> GetOverviewAggregateAsync(DateTime? fromDate, DateTime? toDate)
    {
        var query = _db.ResultRecords.AsQueryable();
        if (fromDate is not null) query = query.Where(r => r.ResultEnteredAt >= fromDate);
        if (toDate is not null) query = query.Where(r => r.ResultEnteredAt <= toDate);

        var totalTests = await query.CountAsync();
        var approvedCount = await query.CountAsync(r => r.SampleStatus == SampleStatus.Approved);
        var pendingReviewCount = await query.CountAsync(r => r.SampleStatus == SampleStatus.UnderReview);
        var pendingApprovalCount = await query.CountAsync(r => r.SampleStatus == SampleStatus.UnderApproval);
        var outOfSpecCount = await query.CountAsync(r => r.ResultLevel == ResultLevel.OutOfSpecification);
        var alertActionCount = await query.CountAsync(r => r.ResultLevel == ResultLevel.AlertLevel || r.ResultLevel == ResultLevel.ActionLevel);

        var categoryCounts = await query
            .GroupBy(r => r.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync();

        var categoryDistribution = categoryCounts
            .Select(c => new OverviewCategoryItem(
                c.Category,
                c.Count,
                totalTests > 0 ? (int)Math.Round((double)c.Count / totalTests * 100) : 0))
            .OrderByDescending(c => c.Count)
            .ToList();

        var testCounts = await query
            .GroupBy(r => new { r.TestCode, r.TestDisplayName })
            .Select(g => new { g.Key.TestCode, g.Key.TestDisplayName, Count = g.Count() })
            .OrderByDescending(t => t.Count)
            .Take(10)
            .ToListAsync();

        var testDistribution = testCounts
            .Select(t => new OverviewTestItem(t.TestCode, t.TestDisplayName, t.Count))
            .ToList();

        var locationCounts = await query
            .GroupBy(r => r.SubjectName)
            .Select(g => new { Location = g.Key, Count = g.Count() })
            .OrderByDescending(l => l.Count)
            .Take(7)
            .ToListAsync();

        var locationDistribution = locationCounts
            .Select(l => new OverviewLocationItem(
                l.Location,
                l.Count,
                totalTests > 0 ? (int)Math.Round((double)l.Count / totalTests * 100) : 0))
            .ToList();

        var recentRecords = await query
            .OrderByDescending(r => r.ResultEnteredAt)
            .Take(5)
            .ToListAsync();

        var recentResults = recentRecords
            .Select(r => new OverviewRecentResultItem(
                r.Id,
                r.ReferenceNumber,
                r.SubjectName,
                r.SubjectDetail,
                r.Category,
                r.TestCode,
                r.TestDisplayName,
                r.ResultEnteredAt,
                r.ResultEnteredByName,
                r.SampleStatus,
                DeriveApprovalStatus(r.SampleStatus)))
            .ToList();

        return new OverviewAggregateResult(
            totalTests, approvedCount, pendingReviewCount, pendingApprovalCount,
            outOfSpecCount, alertActionCount, categoryDistribution, testDistribution,
            locationDistribution, recentResults);
    }

    public async Task<QualitativeEventResult> GetQualitativeEventsAsync(string? testCode, string? subjectName, SampleCategory? category, DateTime? fromDate, DateTime? toDate)
    {
        var query = _db.ResultRecords.Where(r => r.ResultKind == ResultKind.Qualitative && r.ReportedValue == "Detected");

        if (!string.IsNullOrWhiteSpace(testCode)) query = query.Where(r => r.TestCode == testCode);
        if (!string.IsNullOrWhiteSpace(subjectName)) query = query.Where(r => r.SubjectName == subjectName);
        if (category.HasValue) query = query.Where(r => r.Category == category.Value);
        if (fromDate is not null) query = query.Where(r => r.ResultEnteredAt >= fromDate);
        if (toDate is not null) query = query.Where(r => r.ResultEnteredAt <= toDate);

        var records = await query.OrderByDescending(r => r.ResultEnteredAt).ToListAsync();

        var testDisplayName = records.FirstOrDefault()?.TestDisplayName
            ?? (!string.IsNullOrWhiteSpace(testCode) ? (await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Code == testCode))?.DisplayName ?? testCode : "Qualitative Pathogen Detection");

        var events = records.Select(r => new QualitativeEventItem(
            r.Id,
            r.ReferenceNumber,
            r.Category,
            r.SubjectName,
            r.SubjectDetail,
            r.TestCode,
            r.TestDisplayName,
            r.ReportedValue,
            r.ResultEnteredAt,
            r.ResultEnteredByName,
            r.SampleStatus,
            DeriveApprovalStatus(r.SampleStatus),
            r.ApprovedByName,
            r.ApprovedAt)).ToList();

        return new QualitativeEventResult(testCode ?? "ALL_QUALITATIVE", testDisplayName, events);
    }

    // "Completed" = the sample-level approval that finalizes a TestOrder -
    // grouped by TestOrderId (not raw ResultRecord rows) so an EM/After
    // Cleaning batch order with several SampleLocation rows counts once,
    // matching CompletionStatsDto's own TestOrder-level definition of
    // "Approved" rather than counting per-location.
    public async Task<List<MonthlyCompletionPoint>> GetCompletedByMonthAsync(int months = 6)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowStart = currentMonthStart.AddMonths(-(months - 1)).AddYears(-1);

        var approvedTestOrders = await _db.ResultRecords
            .Where(r => r.SampleStatus == SampleStatus.Approved && r.ApprovedAt != null && r.ApprovedAt >= windowStart)
            .Select(r => new { r.TestOrderId, ApprovedAt = r.ApprovedAt!.Value })
            .Distinct()
            .ToListAsync();

        var points = new List<MonthlyCompletionPoint>();
        for (var i = 0; i < months; i++)
        {
            var monthStart = currentMonthStart.AddMonths(-(months - 1) + i);
            var monthEnd = monthStart.AddMonths(1);
            var priorYearStart = monthStart.AddYears(-1);
            var priorYearEnd = monthEnd.AddYears(-1);

            var currentYearCount = approvedTestOrders.Count(a => a.ApprovedAt >= monthStart && a.ApprovedAt < monthEnd);
            var priorYearCount = approvedTestOrders.Count(a => a.ApprovedAt >= priorYearStart && a.ApprovedAt < priorYearEnd);

            points.Add(new MonthlyCompletionPoint(monthStart.ToString("MMM"), priorYearCount, currentYearCount));
        }
        return points;
    }

    // Compares every distinct subject (product/item, or location/point -
    // whichever ResultRecord.SubjectName represents for this category) that
    // has results for one given test code, within the same
    // category+dateRange scope as the Trending panel's own criteria - a
    // single shared query, not a per-product/per-location fetch.
    public async Task<CompareResult> GetCompareBySubjectAsync(string testCode, SampleCategory category, DateTime? fromDate, DateTime? toDate)
    {
        if (string.IsNullOrWhiteSpace(testCode)) throw new InvalidOperationException("testCode is required.");

        var testDefinition = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Code == testCode)
            ?? throw new InvalidOperationException($"Test code \"{testCode}\" is not configured in Test Master.");
        var isNumeric = testDefinition.WorkflowType == WorkflowType.CountTest;

        var query = _db.ResultRecords.Where(r => r.TestCode == testCode && r.Category == category);
        if (fromDate is not null) query = query.Where(r => r.ResultEnteredAt >= fromDate);
        if (toDate is not null) query = query.Where(r => r.ResultEnteredAt <= toDate);

        var records = await query.ToListAsync();

        var subjects = records
            .GroupBy(r => r.SubjectName)
            .Select(g =>
            {
                var list = g.ToList();
                var testsEvaluated = list.Count;

                decimal? meanValue = null;
                double? percentDetected = null;
                double compliancePercent;

                if (isNumeric)
                {
                    var imputed = list
                        .Select(r => r.IsBelowDetectionLimit && r.DetectionLimit.HasValue ? r.DetectionLimit.Value / 2 : r.NumericValue)
                        .Where(v => v.HasValue)
                        .Select(v => v!.Value)
                        .ToList();
                    meanValue = imputed.Count > 0 ? Math.Round(imputed.Average(), 2) : null;

                    var eligibleList = list.Where(r => r.ResultLevel != ResultLevel.LimitsNotConfigured).ToList();
                    var eligibleCount = eligibleList.Count;
                    var withinSpecCount = eligibleList.Count(r => r.ResultLevel == ResultLevel.WithinLimit);
                    compliancePercent = eligibleCount > 0 ? Math.Round((double)withinSpecCount / eligibleCount * 100, 1) : 0;
                }
                else
                {
                    // Two conventions coexist in ReportedValue depending on
                    // which workflow wrote the record: a single-sample
                    // Pathogen session (ResultProjectionService's biochemical
                    // finalization) writes "Detected" / "Not Detected" /
                    // "Pending Confirmation"; an EM/After-Cleaning batch
                    // location result writes "Detected" / "Absent"
                    // (location.ReportedResult ?? location.Status). Both
                    // negative strings must count as compliant here.
                    var detectedCount = list.Count(r => r.ReportedValue == "Detected");
                    percentDetected = testsEvaluated > 0 ? Math.Round((double)detectedCount / testsEvaluated * 100, 1) : null;

                    var notDetectedCount = list.Count(r => r.ReportedValue == "Not Detected" || r.ReportedValue == "Absent");
                    compliancePercent = testsEvaluated > 0 ? Math.Round((double)notDetectedCount / testsEvaluated * 100, 1) : 0;
                }

                // Alert/Action and OOS are always 0 for a qualitative subject -
                // ResultProjectionService never assigns those ResultLevel
                // values to a pathogen result (spec/alert/action limits are a
                // numeric concept) - an honest 0, not a fabricated one.
                var alertActionCount = list.Count(r => r.ResultLevel == ResultLevel.AlertLevel || r.ResultLevel == ResultLevel.ActionLevel);
                var oosCount = list.Count(r => r.ResultLevel == ResultLevel.OutOfSpecification);

                return new CompareSubjectStat(g.Key, testsEvaluated, meanValue, percentDetected, alertActionCount, oosCount, compliancePercent);
            })
            .OrderByDescending(s => s.TestsEvaluated)
            .ToList();

        return new CompareResult(testCode, testDefinition.DisplayName, isNumeric, subjects);
    }

    private static decimal? Impute(TrendPoint point) =>
        point.IsBelowDetectionLimit && point.DetectionLimit.HasValue ? point.DetectionLimit.Value / 2 : point.NumericValue;

    // Sample standard deviation (n-1) - undefined (returned as 0) for
    // fewer than two points.
    private static decimal? StandardDeviation(List<decimal> values)
    {
        if (values.Count < 2) return 0m;
        var mean = values.Average();
        var sumOfSquares = values.Sum(v => (v - mean) * (v - mean));
        return (decimal)Math.Sqrt((double)(sumOfSquares / (values.Count - 1)));
    }
}
