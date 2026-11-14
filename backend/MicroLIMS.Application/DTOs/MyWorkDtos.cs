namespace MicroLIMS.Application.DTOs;

// Lower value = more urgent; the order is the sort order inside a group.
public enum MyWorkUrgencyLevel { Overdue = 0, Now = 1, Today = 2, Later = 3, None = 4 }

// Where a row leads. Type is one of the canonical target types the frontend maps to a route.
public sealed record MyWorkTarget(string Type, int? SampleId = null, int? TestOrderId = null, int? IncubationId = null,
    int? DocumentId = null, int? TaskId = null, int? EquipmentId = null, int? MediaId = null);

public sealed record MyWorkItem(string Key, string GroupKey, string Identifier, string Title, string? Context,
    DateTime? DueAtUtc, MyWorkUrgencyLevel Urgency, DateTime SortAtUtc, bool Locked, string? LockReason,
    MyWorkTarget Target, IReadOnlyList<string> Actions, int? SectionId, string? SectionName, string? Lab);

public sealed record MyWorkGroup(string Key, string Title, int Count, int LockedCount, IReadOnlyList<MyWorkItem> Items, bool HasMore);

public sealed record MyWorkResponse(DateTime ServerTimeUtc, string TimeZoneId, IReadOnlyList<MyWorkGroup> Groups, IReadOnlyList<string> UnavailableSources);

// Null counts mean "unknown": at least one source failed, so any number would be wrong.
public sealed record MyWorkSummary(int? Total, int? Overdue, int? Now, bool Complete);
