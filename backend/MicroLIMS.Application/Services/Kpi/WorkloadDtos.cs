namespace MicroLIMS.Application.Services.Kpi;

// Ratios (Utilisation, ForwardLoad, WeightedOnTime, WeightedRightFirstTime) are fractions: 1.0 = 100%.
// A rate is null when fewer than KpiConstants.MinSampleForRate tests stand behind it.
public sealed record AnalystWorkloadRowDto(int UserId, string FullName, decimal CapacityUnits, decimal DeliveredUnits, decimal? Utilisation,
    bool UtilisationCheckProfile, decimal OpenUnits, decimal ForwardCapacityUnits, decimal? ForwardLoad, bool ForwardLoadOverCapacity,
    decimal? WeightedOnTime, decimal? WeightedRightFirstTime, int DeliveredTests, int UnweightedTests, int UnweightedOpenTests, int TestsOnDefaultTarget);

public sealed record AnalystWorkloadDto(DateTime From, DateTime To, bool CapacityUsesDefaults, bool OnlyOwnRow,
    int UnassignedOpenTests, IReadOnlyList<AnalystWorkloadRowDto> Rows, IReadOnlyList<KpiGap> Gaps);

public sealed record KpiSettingsDto(int SectionId, decimal HoursPerAnalystDay, int WorkingWeekdays, int LoadHorizonDays, bool IsDefault, uint Version);

public sealed record UpdateKpiSettingsRequest(decimal HoursPerAnalystDay, int WorkingWeekdays, int LoadHorizonDays, string Reason);

public sealed record AnalystAvailabilityDto(int Id, int UserId, string UserName, DateOnly FromDate, DateOnly ToDate,
    decimal FractionAvailable, string Reason, string EnteredByName, DateTime EnteredAt);

public sealed record SaveAvailabilityRequest(int UserId, DateOnly FromDate, DateOnly ToDate, decimal FractionAvailable, string Reason);
