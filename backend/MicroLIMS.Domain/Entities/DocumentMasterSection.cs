namespace MicroLIMS.Domain.Entities;

/// <summary>
/// A laboratory section a document belongs to. The master's own SectionId stays the
/// primary section (approvals, periodic review); this table lists ALL the sections,
/// including the primary one.
/// </summary>
public class DocumentMasterSection
{
    public int DocumentMasterId { get; set; }
    public DocumentMaster DocumentMaster { get; set; } = null!;

    public int SectionId { get; set; }
    public DocumentSection Section { get; set; } = null!;
}
