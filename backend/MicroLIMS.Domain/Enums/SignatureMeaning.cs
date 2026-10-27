namespace MicroLIMS.Domain.Enums;

public enum SignatureMeaning
{
    Reviewed,
    Approved,
    Rejected,
    RetestRequested,
    InvestigationOrdered,
    PreparationConfirmed,

    // Appended, never inserted: persisted as int.
    SampleCorrected,
    SampleVoided,
    MasterDataChanged,
    SuitabilityRunPerformed,
    ResultRecorded,
    CalibrationRunPerformed, // retired (slice R)
    CalibrationRunWithdrawn, // retired (slice R)
    TestingClosed,
    LaboratoryAdded,
    TitrantStandardized,
    TitrantDueAcknowledged,
    BiologicalWasteDestroyed,
    MonitoringPlanApproved,
    MonitoringOccurrenceSkipped,
    MonitoringOccurrenceWindowChanged,
    MonitoringDeviationClosed,
    EquipmentPlanApproved,
    CalibrationRecorded,
    MaintenanceEntryRecorded,
    MaintenanceEntryVoided,
    // Retired: user hard delete was removed (inactive users are kept for GxP
    // attribution). Kept so persisted enum ints do not shift.
    UserDeleted,
    UserRoleChanged,
    RolePermissionsChanged,
    RoleDeleted,
    SystemSettingChanged
}
