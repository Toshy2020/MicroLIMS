using MicroLIMS.Application.DTOs.DocumentControl;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface IDocumentAuditService
{
    Task<DocumentAuditResponse> SearchAuditLogsAsync(DocumentAuditFilterRequest filter, int userId);
    Task<List<DocumentAuditItemDto>> GetRecordAuditHistoryAsync(int documentMasterId, int? revisionId, int userId);
    Task<(byte[] Content, string ContentType, string FileName)> ExportAuditTrailAsync(DocumentAuditFilterRequest filter, int userId);
}
