namespace MicroLIMS.Domain.Entities;

public class DocumentTrainingConfiguration
{
    public int Id { get; set; }

    public int? DocumentTypeId { get; set; }
    public DocumentType? DocumentType { get; set; }

    public int? DocumentMasterId { get; set; }
    public DocumentMaster? DocumentMaster { get; set; }

    public bool RequiresReading { get; set; } = true;
    public bool RequiresRetrainingOnRevision { get; set; } = true;
    public int DefaultGracePeriodDays { get; set; } = 14;
    public string DefaultAcknowledgementStatement { get; set; } =
        "I confirm that I have read, understood, and agree to adhere to the contents of this controlled document revision.";

    public int EscalationDaysBeforeDue { get; set; } = 3;
    public int EscalationDaysAfterDue { get; set; } = 1;

    public int ModifiedByUserId { get; set; }
    public User ModifiedByUser { get; set; } = null!;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
}
