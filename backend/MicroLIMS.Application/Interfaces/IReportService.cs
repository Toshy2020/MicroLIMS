namespace MicroLIMS.Application.Interfaces;

public interface IReportService
{
    Task<byte[]> GenerateProductReportPdfAsync(int sampleId);
    Task<byte[]> GenerateWaterReportPdfAsync(DateTime date, IReadOnlyCollection<int>? sectionIds = null);
    Task<byte[]> GenerateEMReportPdfAsync(DateTime date);
    Task<byte[]> GenerateAfterCleaningReportPdfAsync(int sampleId);
}
