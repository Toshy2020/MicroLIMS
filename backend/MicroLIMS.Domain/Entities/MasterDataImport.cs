using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One signed load of the master data workbook: who loaded which file
// (kept in file storage, identified by its SHA-256), what it created, and
// who verified it. The created rows themselves carry their own audit
// trail; this record ties them to the file they came from.
public class MasterDataImport
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public int ImportedByUserId { get; set; }
    public User? ImportedByUser { get; set; }
    public DateTime ImportedAt { get; set; }
    public string? Comment { get; set; }
    // Per-sheet created/unchanged counts, JSON (WorkbookSheetSummary[]).
    public string SummaryJson { get; set; } = "[]";
    public MasterDataImportStatus Status { get; set; }
    public int? VerifiedByUserId { get; set; }
    public User? VerifiedByUser { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerificationComment { get; set; }
    public List<MasterDataImportRecord> Records { get; set; } = new();
}

public class MasterDataImportRecord
{
    public int Id { get; set; }
    public int MasterDataImportId { get; set; }
    public MasterDataImport? MasterDataImport { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Sheet { get; set; } = string.Empty;
    public int RowNumber { get; set; }
}
