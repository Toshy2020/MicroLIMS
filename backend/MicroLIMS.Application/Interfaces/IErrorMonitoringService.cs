using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces;

// Read and triage side of the error monitoring page. Separate from
// IErrorCaptureService, which only writes: capture runs on the hot path
// of a failing request and must stay minimal, while this is ordinary
// admin querying.
public interface IErrorMonitoringService
{
    Task<IncidentListResult> SearchAsync(IncidentQuery query, CancellationToken cancellationToken = default);

    Task<IncidentDetailDto?> GetAsync(Guid incidentId, CancellationToken cancellationToken = default);

    Task<bool> ResolveAsync(Guid incidentId, int userId, string? notes, CancellationToken cancellationToken = default);

    Task<bool> ReopenAsync(Guid incidentId, CancellationToken cancellationToken = default);

    // Null severity clears the override. Recomputes the parent Incident,
    // which may lower it - unlike capture, which only ever raises.
    Task<bool> OverrideSeverityAsync(
        Guid errorLogId, ErrorSeverity? severity, CancellationToken cancellationToken = default);

    Task<string> ExportCsvAsync(IncidentQuery query, CancellationToken cancellationToken = default);
}
