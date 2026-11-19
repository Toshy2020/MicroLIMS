using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class Result
{
    public int Id { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public string RawValue { get; set; } = string.Empty;
    public string? InterpretedValue { get; set; }
    public ResultType Type { get; set; }
    public int EnteredByUserId { get; set; }
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;

    // A result invalidated by a testing-session reset (user decision 2026-11-15):
    // the row is kept with who, when and why instead of being deleted. Normal reads
    // never see it (global query filter in ResultConfiguration); a history view
    // reads it with IgnoreQueryFilters().
    public DateTime? InvalidatedAt { get; set; }
    public int? InvalidatedByUserId { get; set; }
    public string? InvalidationReason { get; set; }
}
