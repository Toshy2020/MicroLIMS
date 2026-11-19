namespace MicroLIMS.Domain.Entities;

// One record a user opened recently (sample, test, item, document, equipment, material, media lot,
// solution preparation). A convenience list, not an access log: no label is stored, the label is
// resolved at read time after re-checking that the user may read the record. Limits are enforced
// by RecentRecordService.
public class RecentRecord
{
    public const int MaxPerUser = 20;
    public const int MaxRecordTypeLength = 30;

    public int UserId { get; set; }
    public User? User { get; set; }
    public string RecordType { get; set; } = string.Empty;
    public int RecordId { get; set; }
    public DateTime OpenedAt { get; set; }
}
