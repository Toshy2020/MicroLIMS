using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Interfaces;

public interface ICalibrationRunService
{
    Task<CalibrationRunPreviewResult> PreviewAsync(
        PreviewCalibrationRunRequest request,
        int userId,
        CancellationToken ct = default);

    Task<CalibrationRun> CreateAsync(
        CreateCalibrationRunRequest request,
        Stream fileStream,
        string originalFileName,
        string declaredContentType,
        int userId,
        string? ipAddress,
        CancellationToken ct = default);

    Task<CalibrationRun> WithdrawAsync(
        int id,
        WithdrawCalibrationRunRequest request,
        int userId,
        string? ipAddress,
        CancellationToken ct = default);


    Task<List<CalibrationRun>> GetAllAsync(
        CalibrationRunFilter filter,
        int userId,
        CancellationToken ct = default);

    Task<CalibrationRun?> GetByIdAsync(
        int id,
        int userId,
        CancellationToken ct = default);

    Task<CalibrationRunReportDetailsDto> GetReportDetailsAsync(
        int runId,
        int userId,
        CancellationToken ct = default);

    Task<(CalibrationRunDocument Document, byte[] Content)> GetDocumentContentAsync(
        int runId,
        int userId,
        CancellationToken ct = default);
}
