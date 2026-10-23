namespace MicroLIMS.Application.DTOs.Responses;

// How an item's STM resolves against the Document Library.
public enum ItemStmStatus { NotLinked, NotFound, NoEffectiveRevision, Effective }

public record ItemStmResponse(
    ItemStmStatus Status,
    string? SopNumber,
    int? DocumentMasterId,
    string? CompanyDocumentCode,
    string? Title,
    string? DocumentTypeCode,
    string? RevisionNumber,
    DateTime? EffectiveDate,
    int? ControlledPdfFileId,
    string? ControlledPdfFileName,
    bool CanDownload);
