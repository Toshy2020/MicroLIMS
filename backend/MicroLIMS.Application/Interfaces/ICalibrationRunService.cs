using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Interfaces;

public interface ICalibrationRunService
{
    Task<CalibrationRunPreviewResult> PreviewAsync(
        PreviewCalibrationRunRequest request,
        int userId,
        CancellationToken ct = default);

    Task<CalibrationRunView> CreateAsync(
        CreateCalibrationRunRequest request,
        Stream fileStream,
        string originalFileName,
        string declaredContentType,
        int userId,
        string? ipAddress,
        CancellationToken ct = default);

    Task<CalibrationRunWithdrawResponse> WithdrawAsync(
        int id,
        WithdrawCalibrationRunRequest request,
        int userId,
        string? ipAddress,
        CancellationToken ct = default);


    Task<List<CalibrationRunView>> GetAllAsync(
        CalibrationRunFilter filter,
        int userId,
        CancellationToken ct = default);

    Task<CalibrationRunView?> GetByIdAsync(
        int id,
        int userId,
        CancellationToken ct = default);

    Task<CalibrationRunReportDetailsDto> GetReportDetailsAsync(
        int runId,
        int userId,
        CancellationToken ct = default);

    Task<(CalibrationRunDocumentView Document, byte[] Content)> GetDocumentContentAsync(
        int runId,
        int userId,
        CancellationToken ct = default);
}
