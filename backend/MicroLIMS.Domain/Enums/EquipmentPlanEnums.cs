namespace MicroLIMS.Domain.Enums;

public enum IntervalUnit
{
    Weeks,
    Months
}

public enum EquipmentTaskType
{
    Calibration,
    PreventiveMaintenance
}

public enum EquipmentTaskEventType
{
    Created,
    Rescheduled,
    Completed,
    Retired
}

public enum MaintenanceEntryType
{
    PreventiveMaintenance,
    BreakdownRepair,
    Other
}

public enum CalendarEventType
{
    VendorVisit,
    BreakdownRepair,
    Audit,
    ProficiencyTesting,
    Other
}

public enum CalendarEventStatus
{
    Active,
    Cancelled
}
