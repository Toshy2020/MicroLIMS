using MicroLIMS.Application.DTOs.DocumentControl;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface IDocumentEffectiveDateService
{
    /// <summary>
    /// Evaluates and transitions all DocumentRevision records currently in FutureEffective status
    /// whose EffectiveDate is less than or equal to current UTC time (DC-URS-177, DC-URS-178, DC-URS-181).
    /// </summary>
    /// <param name="utcNowOverride">Optional UTC reference clock for deterministic testing or boundary simulation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution summary report detailing all activated transitions and isolated failures.</returns>
    Task<EffectiveDateProcessingResultDto> ProcessMaturedRevisionsAsync(
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default);
}
