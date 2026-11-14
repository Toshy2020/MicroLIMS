namespace MicroLIMS.Domain.Entities;

// Fire-once ledger recording that an EscalationRule fired for a specific record.
public class EscalationEvent
{
    public int Id { get; set; }
    public int EscalationRuleId { get; set; }
    public EscalationRule? EscalationRule { get; set; }
    public string RecordType { get; set; } = string.Empty;
    public int RecordId { get; set; }
    public DateTime FiredAt { get; set; }
    public string RecipientUserIds { get; set; } = "[]";
    public bool NoRecipient { get; set; }
}
