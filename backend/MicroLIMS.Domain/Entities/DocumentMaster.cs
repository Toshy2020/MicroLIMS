using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class DocumentMaster
{
    public int Id { get; set; }
    public string MicroLimsDocumentId { get; set; } = string.Empty;
    public string CompanyDocumentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public int DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = null!;

    public int DepartmentId { get; set; }
    public DocumentDepartment Department { get; set; } = null!;

    public int SectionId { get; set; }
    public DocumentSection Section { get; set; } = null!;

    public int DocumentOwnerUserId { get; set; }
    public User DocumentOwnerUser { get; set; } = null!;

    public DocumentConfidentiality Confidentiality { get; set; }
    public string? Category { get; set; }
    public RecordOrigin RecordOrigin { get; set; } = RecordOrigin.Native;
    public DocumentRecordStatus RecordStatus { get; set; } = DocumentRecordStatus.Active;

    public int? CurrentEffectiveRevisionId { get; set; }
    public DocumentRevision? CurrentEffectiveRevision { get; set; }

    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? ModifiedByUserId { get; set; }
    public User? ModifiedByUser { get; set; }
    public DateTime? ModifiedAt { get; set; }

    public int? VoidedByUserId { get; set; }
    public User? VoidedByUser { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }

    public ICollection<DocumentRevision> Revisions { get; set; } = new List<DocumentRevision>();
    public ICollection<DocumentMasterAssignment> Assignments { get; set; } = new List<DocumentMasterAssignment>();
    public ICollection<DocumentKeyword> Keywords { get; set; } = new List<DocumentKeyword>();
}
