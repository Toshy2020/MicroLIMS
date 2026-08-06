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
    DateTime Date, decimal? NumericValue, string ReportedValue, bool IsBelowDetectionLimit,
    decimal? DetectionLimit, ResultLevel ResultLevel, string? AlertLimit, string? ActionLimit, string? SpecLimit);

public record TrendStatistics(int Count, decimal? Latest, decimal? Mean, decimal? StandardDeviation, decimal? Min, decimal? Max, int ImputedPointCount);

public record TrendResult(string TestCode, string TestDisplayName, string SubjectName, string? Unit, List<TrendPoint> Points, TrendStatistics Statistics);

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
            r.ResultEnteredAt, r.NumericValue, r.ReportedValue, r.IsBelowDetectionLimit,
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
