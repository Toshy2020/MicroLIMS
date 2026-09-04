using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface IDocumentFileService
{
    Task<RevisionFileDto> UploadRevisionFileAsync(int revisionId, FileRole fileRole, string originalFileName, string declaredContentType, byte[] content, int userId);
    Task<(RevisionFileDto Metadata, byte[] Content)> GetFileContentAsync(int fileId, int userId, bool isDownload = false);
}
