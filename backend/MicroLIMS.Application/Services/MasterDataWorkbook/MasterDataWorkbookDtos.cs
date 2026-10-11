namespace MicroLIMS.Application.Services.MasterDataWorkbook;

public sealed record MasterDataImportDto(
    int Id,
    string FileName,
    string FileSha256,
    string ImportedBy,
    DateTime ImportedAt,
    string? Comment,
    string Status,
    string? VerifiedBy,
    DateTime? VerifiedAt,
    IReadOnlyList<WorkbookSheetSummary> Sheets,
    IReadOnlyList<string> ItemsWithoutSpecifications);

public sealed record MasterDataImportResult(bool Committed, WorkbookReport Report, MasterDataImportDto? Import);

public sealed record VerifyMasterDataImportRequest(string Password, string? Comment);
