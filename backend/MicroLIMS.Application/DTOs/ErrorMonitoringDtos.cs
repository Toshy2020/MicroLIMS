using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

// Filters for the admin incident list. All optional - an empty query is
// "everything, newest activity first".
public record IncidentQuery(
    ErrorSeverity? Severity = null,
    ErrorSource? Source = null,
    IncidentStatus? Status = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 25);

// One row in the incident list. OccurrenceCount and Sources are rolled up
// from the children so the admin sees "one thing went wrong, N times,
// across these tiers" without expanding anything.
public record IncidentListItemDto(
    Guid Id,
    string CorrelationId,
    ErrorSeverity Severity,
    IncidentStatus Status,
    string Summary,
    DateTime FirstSeenUtc,
    DateTime LastSeenUtc,
    int OccurrenceCount,
    IReadOnlyList<ErrorSource> Sources,
    DateTime? ResolvedAtUtc,
    string? ResolvedByUserName,
    string? ResolutionNotes);

public record IncidentListResult(
    IReadOnlyList<IncidentListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record ErrorLogDto(
    Guid Id,
    ErrorSource Source,
    ErrorSeverity Severity,
    ErrorSeverity? SeverityOverride,
    // What the rollup actually uses: SeverityOverride ?? Severity. Sent
    // explicitly so the UI never has to re-derive the rule.
    ErrorSeverity EffectiveSeverity,
    string ExceptionType,
    string Message,
    string? StackTrace,
    string? RequestPath,
    string? HttpMethod,
    int? StatusCode,
    int? UserId,
    string? UserName,
    DateTime OccurredAtUtc,
    string? RawContext);

public record IncidentDetailDto(
    Guid Id,
    string CorrelationId,
    ErrorSeverity Severity,
    IncidentStatus Status,
    string Summary,
    DateTime FirstSeenUtc,
    DateTime LastSeenUtc,
    DateTime? ResolvedAtUtc,
    string? ResolvedByUserName,
    string? ResolutionNotes,
    IReadOnlyList<ErrorLogDto> ErrorLogs);

public record ResolveIncidentRequest(string? ResolutionNotes);

// Null Severity clears the override and returns the row to its
// auto-classified value.
public record SeverityOverrideRequest(ErrorSeverity? Severity);
