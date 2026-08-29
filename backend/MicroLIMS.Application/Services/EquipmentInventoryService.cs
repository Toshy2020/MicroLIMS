using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record SaveEquipmentInventoryRequest(
    string InstrumentType,
    string ManufacturerName,
    string? SerialNumber,
    string? FirmwareVersion,
    string Code,
    string Location,
    DateTime? CalibrationDueDate,
    EquipmentOperationalStatus Status,
    string? StatusChangeComment = null);

public record EquipmentStatusHistoryDto(
    int Id,
    int EquipmentInventoryId,
    EquipmentOperationalStatus PreviousStatus,
    EquipmentOperationalStatus NewStatus,
    string Comment,
    int ChangedByUserId,
    string ChangedByName,
    DateTime ChangedAt);

public record ActiveEquipmentItemDto(
    int Id,
    string Code,
    string InstrumentType,
    string ManufacturerName,
    string Location,
    string Status,
    DateTime? CalibrationDueDate,
    decimal? SetPointTemperature,
    string PrimaryActivityCategory,
    int ActiveItemCount);

public record EquipmentActivityDto(
    int ActivityId,
    string ItemName,
    string ItemCode,
    string ActivityType,
    string MediaDescription,
    DateTime StartedOn,
    string StartedBy,
    DateTime? ExpectedCompletion,
    DateTime? CompletedOn,
    bool IsActive,
    int? EntityId,
    string? EntityType);

public record HistoricalLocationDto(
    string EquipmentCode,
    string EquipmentName,
    string ActivityType,
    DateTime StartedOn,
    DateTime? CompletedOn,
    string PerformedBy);

public record WhereIsItResultDto(
    string SearchTerm,
    EquipmentActivityDto? CurrentActivity,
    string? CurrentEquipmentCode,
    string? CurrentEquipmentName,
    List<HistoricalLocationDto> History);

public class EquipmentInventoryService
{
    private readonly MicroLimsDbContext _db;

    public EquipmentInventoryService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<EquipmentInventory>> GetAllAsync() =>
        await _db.EquipmentInventories.OrderBy(e => e.InstrumentType).ThenBy(e => e.Code).ToListAsync();

    public async Task<EquipmentInventory?> GetByIdAsync(int id) =>
        await _db.EquipmentInventories.FirstOrDefaultAsync(e => e.Id == id);

    public async Task<List<EquipmentInventory>> GetForPrintAsync() =>
        await _db.EquipmentInventories
            .Where(e => e.Status == EquipmentOperationalStatus.InService)
            .OrderBy(e => e.InstrumentType).ThenBy(e => e.Code)
            .ToListAsync();

    public async Task<EquipmentInventory> CreateAsync(SaveEquipmentInventoryRequest r, int currentUserId)
    {
        if (await _db.EquipmentInventories.AnyAsync(e => e.Code == r.Code))
            throw new InvalidOperationException($"Equipment code \"{r.Code}\" already exists.");

        var entity = new EquipmentInventory
        {
            InstrumentType = r.InstrumentType,
            ManufacturerName = r.ManufacturerName,
            SerialNumber = r.SerialNumber,
            FirmwareVersion = r.FirmwareVersion,
            Code = r.Code,
            Location = r.Location,
            CalibrationDueDate = r.CalibrationDueDate,
            Status = r.Status,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = currentUserId,
            LastModifiedAt = DateTime.UtcNow
        };
        _db.EquipmentInventories.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(int id, SaveEquipmentInventoryRequest r, int currentUserId)
    {
        var entity = await _db.EquipmentInventories.FindAsync(id)
            ?? throw new InvalidOperationException($"Equipment {id} not found.");

        if (r.Code != entity.Code && await _db.EquipmentInventories.AnyAsync(e => e.Code == r.Code))
            throw new InvalidOperationException($"Equipment code \"{r.Code}\" already exists.");

        if (r.Status != entity.Status)
        {
            if (string.IsNullOrWhiteSpace(r.StatusChangeComment))
                throw new InvalidOperationException("A comment explaining the reason for changing the operational status is required.");

            var history = new EquipmentStatusHistory
            {
                EquipmentInventoryId = entity.Id,
                PreviousStatus = entity.Status,
                NewStatus = r.Status,
                Comment = r.StatusChangeComment.Trim(),
                ChangedByUserId = currentUserId,
                ChangedAt = DateTime.UtcNow
            };
            _db.EquipmentStatusHistories.Add(history);
            entity.Status = r.Status;
        }

        entity.InstrumentType = r.InstrumentType;
        entity.ManufacturerName = r.ManufacturerName;
        entity.SerialNumber = r.SerialNumber;
        entity.FirmwareVersion = r.FirmwareVersion;
        entity.Code = r.Code;
        entity.Location = r.Location;
        entity.CalibrationDueDate = r.CalibrationDueDate;
        entity.LastModifiedByUserId = currentUserId;
        entity.LastModifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<List<EquipmentStatusHistoryDto>> GetStatusHistoryAsync(int equipmentId)
    {
        var equipmentExists = await _db.EquipmentInventories.AnyAsync(e => e.Id == equipmentId);
        if (!equipmentExists)
            throw new InvalidOperationException($"Equipment {equipmentId} not found.");

        var historyList = await _db.EquipmentStatusHistories
            .Where(h => h.EquipmentInventoryId == equipmentId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();

        var userIds = historyList.Select(h => h.ChangedByUserId).Distinct().ToList();
        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return historyList.Select(h => new EquipmentStatusHistoryDto(
            h.Id,
            h.EquipmentInventoryId,
            h.PreviousStatus,
            h.NewStatus,
            h.Comment,
            h.ChangedByUserId,
            userMap.GetValueOrDefault(h.ChangedByUserId, "Unknown"),
            h.ChangedAt)).ToList();
    }

    // =========================================================================
    // ACTIVE EQUIPMENT TRACEABILITY IMPLEMENTATION
    // =========================================================================

    public async Task<List<ActiveEquipmentItemDto>> GetActiveEquipmentAsync()
    {
        var allEquipment = await _db.EquipmentInventories.ToListAsync();
        var masterEquipment = await _db.Equipment.ToListAsync();

        var activeList = new List<ActiveEquipmentItemDto>();

        foreach (var eq in allEquipment)
        {
            var activeActivities = await GetActiveActivitiesForEquipmentInternalAsync(eq, masterEquipment);
            if (activeActivities.Count > 0)
            {
                var firstAct = activeActivities.First();
                var primaryCategory = firstAct.ActivityType.Contains("Incubation", StringComparison.OrdinalIgnoreCase) || firstAct.ActivityType.Contains("Pathogen", StringComparison.OrdinalIgnoreCase) ? "Incubation"
                                    : firstAct.ActivityType.Contains("Cryovial", StringComparison.OrdinalIgnoreCase) ? "Cryovial Storage"
                                    : firstAct.ActivityType.Contains("Media", StringComparison.OrdinalIgnoreCase) ? "Media Storage"
                                    : "Laboratory Activity";

                activeList.Add(new ActiveEquipmentItemDto(
                    eq.Id,
                    eq.Code,
                    eq.InstrumentType,
                    eq.ManufacturerName,
                    eq.Location,
                    eq.Status.ToString(),
                    eq.CalibrationDueDate,
                    masterEquipment.FirstOrDefault(m => string.Equals(m.Code, eq.Code, StringComparison.OrdinalIgnoreCase))?.SetPointTemperature,
                    primaryCategory,
                    activeActivities.Count
                ));
            }
        }

        return activeList.OrderBy(e => e.InstrumentType).ThenBy(e => e.Code).ToList();
    }

    public async Task<List<EquipmentActivityDto>> GetActiveActivitiesForEquipmentAsync(int equipmentId)
    {
        var eq = await _db.EquipmentInventories.FirstOrDefaultAsync(e => e.Id == equipmentId)
            ?? throw new InvalidOperationException($"Equipment {equipmentId} not found.");
        var masterEquipment = await _db.Equipment.ToListAsync();
        return await GetActiveActivitiesForEquipmentInternalAsync(eq, masterEquipment);
    }

    public async Task<List<EquipmentActivityDto>> GetHistoricalActivitiesForEquipmentAsync(int equipmentId, string? itemCode = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var eq = await _db.EquipmentInventories.FirstOrDefaultAsync(e => e.Id == equipmentId)
            ?? throw new InvalidOperationException($"Equipment {equipmentId} not found.");
        var masterEquipment = await _db.Equipment.ToListAsync();
        var matchingMasterId = masterEquipment.FirstOrDefault(m => string.Equals(m.Code, eq.Code, StringComparison.OrdinalIgnoreCase))?.Id;

        var allIncubations = await _db.Incubations
            .Include(i => i.TestOrder).ThenInclude(t => t!.Sample).ThenInclude(s => s!.Item)
            .Include(i => i.Media).ThenInclude(m => m!.Material)
            .Where(i => i.IncubatorEquipmentId == eq.Id || (matchingMasterId.HasValue && i.IncubatorEquipmentId == matchingMasterId.Value))
            .ToListAsync();

        var userIds = allIncubations.Where(i => i.StartedByUserId.HasValue).Select(i => i.StartedByUserId!.Value).Distinct().ToList();
        var userMap = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

        var list = new List<EquipmentActivityDto>();

        foreach (var inc in allIncubations)
        {
            var sampleCode = inc.TestOrder?.Sample?.ReferenceNumber ?? inc.TestOrder?.SampleId.ToString() ?? "N/A";
            var itemName = inc.TestOrder?.Sample?.Item?.Name ?? inc.StepName ?? "Laboratory Test";
            var testCode = inc.TestOrder?.TestCode ?? "Test";

            string activityType = (inc.StepName?.Contains("Pathogen", StringComparison.OrdinalIgnoreCase) == true) || testCode.StartsWith("PAT")
                ? "Pathogen Test"
                : (inc.StepName?.Contains("GPT", StringComparison.OrdinalIgnoreCase) == true)
                ? "GPT"
                : "Media Incubation";

            var mediaName = inc.Media?.Material?.MaterialName ?? "N/A";
            var mediaLot = inc.Media?.LotNumber ?? "";
            var mediaDesc = string.IsNullOrWhiteSpace(mediaLot) ? mediaName : $"{mediaName} (Lot {mediaLot})";

            var startedBy = inc.StartedByUserId.HasValue && userMap.TryGetValue(inc.StartedByUserId.Value, out var uName) ? uName : "Laboratory Analyst";
            var startedOn = inc.IncubationStartUtc ?? inc.StartedAt;

            var isTestCompleted = inc.TestOrder != null && (inc.TestOrder.Status == ApprovalStatus.Approved ||
                                                            inc.TestOrder.CurrentStep == WorkflowStep.Ready ||
                                                            inc.TestOrder.CurrentStep == WorkflowStep.Reviewed ||
                                                            inc.TestOrder.CurrentStep == WorkflowStep.Approved);
            var isSampleFinished = inc.TestOrder?.Sample != null && (inc.TestOrder.Sample.Status == SampleStatus.Approved ||
                                                                    inc.TestOrder.Sample.Status == SampleStatus.UnderReview ||
                                                                    inc.TestOrder.Sample.Status == SampleStatus.UnderApproval);

            var completedOn = inc.CompletedAt ?? (isTestCompleted || isSampleFinished
                ? (inc.IncubationEndUtc ?? inc.StartedAt)
                : (inc.IncubationEndUtc.HasValue && inc.IncubationEndUtc.Value <= DateTime.UtcNow ? inc.IncubationEndUtc.Value : (DateTime?)null));

            bool isActive = inc.CompletedAt == null && !isTestCompleted && !isSampleFinished && (inc.IncubationEndUtc == null || inc.IncubationEndUtc > DateTime.UtcNow);

            list.Add(new EquipmentActivityDto(
                inc.Id,
                itemName,
                sampleCode,
                activityType,
                mediaDesc,
                startedOn,
                startedBy,
                inc.IncubationEndUtc ?? inc.ExpectedReadingAt,
                completedOn,
                isActive,
                inc.TestOrder?.SampleId,
                "Sample"
            ));
        }

        var queryable = list.AsQueryable();

        if (!string.IsNullOrWhiteSpace(itemCode))
        {
            var q = itemCode.Trim().ToLower();
            queryable = queryable.Where(a => a.ItemCode.ToLower().Contains(q) || a.ItemName.ToLower().Contains(q) || a.MediaDescription.ToLower().Contains(q));
        }

        if (fromDate.HasValue)
        {
            queryable = queryable.Where(a => a.StartedOn >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            var endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
            queryable = queryable.Where(a => a.StartedOn <= endOfDay);
        }

        return queryable.OrderByDescending(a => a.StartedOn).ToList();
    }

    public async Task<WhereIsItResultDto> WhereIsItAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new WhereIsItResultDto("", null, null, null, new List<HistoricalLocationDto>());

        var q = query.Trim().ToLower();
        var allEquipment = await _db.EquipmentInventories.ToListAsync();

        var historyList = new List<HistoricalLocationDto>();
        EquipmentActivityDto? currentActivity = null;
        string? currentEquipmentCode = null;
        string? currentEquipmentName = null;

        var incubations = await _db.Incubations
            .Include(i => i.TestOrder).ThenInclude(t => t!.Sample).ThenInclude(s => s!.Item)
            .Include(i => i.Media).ThenInclude(m => m!.Material)
            .Include(i => i.IncubatorEquipment)
            .Where(i => (i.TestOrder != null && i.TestOrder.Sample != null && (i.TestOrder.Sample.ReferenceNumber.ToLower().Contains(q) || (i.TestOrder.Sample.Item != null && i.TestOrder.Sample.Item.Name.ToLower().Contains(q))))
                     || (i.Media != null && (i.Media.LotNumber.ToLower().Contains(q) || (i.Media.Material != null && i.Media.Material.MaterialName.ToLower().Contains(q))))
                     || i.StepName.ToLower().Contains(q))
            .OrderByDescending(i => i.IncubationStartUtc ?? i.StartedAt)
            .ToListAsync();

        var userIds = incubations.Where(i => i.StartedByUserId.HasValue).Select(i => i.StartedByUserId!.Value).Distinct().ToList();
        var userMap = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

        foreach (var inc in incubations)
        {
            var eq = allEquipment.FirstOrDefault(e => e.Id == inc.IncubatorEquipmentId)
                  ?? allEquipment.FirstOrDefault(e => inc.IncubatorEquipment != null && e.Code == inc.IncubatorEquipment.Code);
            var eqCode = eq?.Code ?? inc.IncubatorEquipment?.Code ?? "EQ-UNKNOWN";
            var eqName = eq?.InstrumentType ?? inc.IncubatorEquipment?.Name ?? "Equipment";

            var sampleCode = inc.TestOrder?.Sample?.ReferenceNumber ?? inc.TestOrder?.SampleId.ToString() ?? "N/A";
            var itemName = inc.TestOrder?.Sample?.Item?.Name ?? inc.StepName ?? "Laboratory Test";
            var testCode = inc.TestOrder?.TestCode ?? "Test";

            string activityType = (inc.StepName?.Contains("Pathogen", StringComparison.OrdinalIgnoreCase) == true) || testCode.StartsWith("PAT")
                ? "Pathogen Test"
                : (inc.StepName?.Contains("GPT", StringComparison.OrdinalIgnoreCase) == true)
                ? "GPT"
                : "Media Incubation";

            var mediaName = inc.Media?.Material?.MaterialName ?? "N/A";
            var mediaLot = inc.Media?.LotNumber ?? "";
            var mediaDesc = string.IsNullOrWhiteSpace(mediaLot) ? mediaName : $"{mediaName} (Lot {mediaLot})";

            var startedBy = inc.StartedByUserId.HasValue && userMap.TryGetValue(inc.StartedByUserId.Value, out var uName) ? uName : "Laboratory Analyst";
            var startedOn = inc.IncubationStartUtc ?? inc.StartedAt;

            var isTestCompleted = inc.TestOrder != null && (inc.TestOrder.Status == ApprovalStatus.Approved ||
                                                            inc.TestOrder.CurrentStep == WorkflowStep.Ready ||
                                                            inc.TestOrder.CurrentStep == WorkflowStep.Reviewed ||
                                                            inc.TestOrder.CurrentStep == WorkflowStep.Approved);
            var isSampleFinished = inc.TestOrder?.Sample != null && (inc.TestOrder.Sample.Status == SampleStatus.Approved ||
                                                                    inc.TestOrder.Sample.Status == SampleStatus.UnderReview ||
                                                                    inc.TestOrder.Sample.Status == SampleStatus.UnderApproval);

            var completedOn = inc.CompletedAt ?? (isTestCompleted || isSampleFinished
                ? (inc.IncubationEndUtc ?? inc.StartedAt)
                : (inc.IncubationEndUtc.HasValue && inc.IncubationEndUtc.Value <= DateTime.UtcNow ? inc.IncubationEndUtc.Value : (DateTime?)null));

            bool isActive = inc.CompletedAt == null && !isTestCompleted && !isSampleFinished && (inc.IncubationEndUtc == null || inc.IncubationEndUtc > DateTime.UtcNow);

            if (isActive && currentActivity == null)
            {
                currentActivity = new EquipmentActivityDto(
                    inc.Id,
                    itemName,
                    sampleCode,
                    activityType,
                    mediaDesc,
                    startedOn,
                    startedBy,
                    inc.IncubationEndUtc ?? inc.ExpectedReadingAt,
                    null,
                    true,
                    inc.TestOrder?.SampleId,
                    "Sample"
                );
                currentEquipmentCode = eqCode;
                currentEquipmentName = eqName;
            }

            historyList.Add(new HistoricalLocationDto(
                eqCode,
                eqName,
                activityType,
                startedOn,
                completedOn,
                startedBy
            ));
        }

        return new WhereIsItResultDto(query, currentActivity, currentEquipmentCode, currentEquipmentName, historyList);
    }

    private async Task<List<EquipmentActivityDto>> GetActiveActivitiesForEquipmentInternalAsync(EquipmentInventory eq, List<Equipment> masterEquipment)
    {
        var matchingMasterId = masterEquipment.FirstOrDefault(m => string.Equals(m.Code, eq.Code, StringComparison.OrdinalIgnoreCase))?.Id;
        var now = DateTime.UtcNow;
        var activities = new List<EquipmentActivityDto>();

        // 1. Active Incubation records
        var allEqIncubations = await _db.Incubations
            .Include(i => i.TestOrder).ThenInclude(t => t!.Sample).ThenInclude(s => s!.Item)
            .Include(i => i.Media).ThenInclude(m => m!.Material)
            .Where(i => (i.IncubatorEquipmentId == eq.Id || (matchingMasterId.HasValue && i.IncubatorEquipmentId == matchingMasterId.Value))
                     && i.CompletedAt == null && (i.IncubationEndUtc == null || i.IncubationEndUtc > now))
            .ToListAsync();

        var activeIncubations = allEqIncubations.Where(i =>
            i.CompletedAt == null &&
            (i.IncubationEndUtc == null || i.IncubationEndUtc > now) &&
            (i.TestOrder == null || (i.TestOrder.Status != ApprovalStatus.Approved &&
                                     i.TestOrder.CurrentStep != WorkflowStep.Ready &&
                                     i.TestOrder.CurrentStep != WorkflowStep.Reviewed &&
                                     i.TestOrder.CurrentStep != WorkflowStep.Approved)) &&
            (i.TestOrder?.Sample == null || (i.TestOrder.Sample.Status != SampleStatus.Approved &&
                                             i.TestOrder.Sample.Status != SampleStatus.UnderReview &&
                                             i.TestOrder.Sample.Status != SampleStatus.UnderApproval))
        ).ToList();

        var userIds = activeIncubations.Where(i => i.StartedByUserId.HasValue).Select(i => i.StartedByUserId!.Value).Distinct().ToList();
        var userMap = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

        foreach (var inc in activeIncubations)
        {
            var sampleCode = inc.TestOrder?.Sample?.ReferenceNumber ?? inc.TestOrder?.SampleId.ToString() ?? "N/A";
            var itemName = inc.TestOrder?.Sample?.Item?.Name ?? inc.StepName ?? "Laboratory Test";
            var testCode = inc.TestOrder?.TestCode ?? "Test";

            string activityType = (inc.StepName?.Contains("Pathogen", StringComparison.OrdinalIgnoreCase) == true) || testCode.StartsWith("PAT")
                ? "Pathogen Test"
                : (inc.StepName?.Contains("GPT", StringComparison.OrdinalIgnoreCase) == true)
                ? "GPT"
                : "Media Incubation";

            var mediaName = inc.Media?.Material?.MaterialName ?? "N/A";
            var mediaLot = inc.Media?.LotNumber ?? "";
            var mediaDesc = string.IsNullOrWhiteSpace(mediaLot) ? mediaName : $"{mediaName} (Lot {mediaLot})";

            var startedBy = inc.StartedByUserId.HasValue && userMap.TryGetValue(inc.StartedByUserId.Value, out var uName) ? uName : "Laboratory Analyst";

            activities.Add(new EquipmentActivityDto(
                inc.Id,
                itemName,
                sampleCode,
                activityType,
                mediaDesc,
                inc.IncubationStartUtc ?? inc.StartedAt,
                startedBy,
                inc.IncubationEndUtc ?? inc.ExpectedReadingAt,
                null,
                true,
                inc.TestOrder?.SampleId,
                "Sample"
            ));
        }

        // 2. Refrigerator / Media Storage
        if (eq.InstrumentType.Contains("Refrigerator", StringComparison.OrdinalIgnoreCase) || eq.Code.StartsWith("REF", StringComparison.OrdinalIgnoreCase))
        {
            var activeMedia = await _db.Media
                .Include(m => m.Material)
                .Where(m => (m.AutoclaveEquipmentId == eq.Id || (matchingMasterId.HasValue && m.AutoclaveEquipmentId == matchingMasterId.Value) || eq.InstrumentType.Contains("Refrigerator"))
                         && m.Status != Domain.Enums.MediaStatus.Destroyed && m.ExpiryDate > now)
                .ToListAsync();

            var mediaUserIds = activeMedia.Select(m => m.PreparedByUserId).Distinct().ToList();
            var mediaUserMap = await _db.Users.Where(u => mediaUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

            foreach (var m in activeMedia)
            {
                var mediaName = m.Material?.MaterialName ?? "Media Batch";
                var preparedBy = mediaUserMap.TryGetValue(m.PreparedByUserId, out var pName) ? pName : "Laboratory Analyst";

                activities.Add(new EquipmentActivityDto(
                    m.Id,
                    mediaName,
                    m.LotNumber,
                    "Media Storage",
                    $"Storage of {mediaName}",
                    m.PreparedAt,
                    preparedBy,
                    m.ExpiryDate,
                    null,
                    true,
                    m.Id,
                    "Media"
                ));
            }
        }

        // 3. Deep Freezer / Cryovial Storage
        if (eq.InstrumentType.Contains("Freezer", StringComparison.OrdinalIgnoreCase) || eq.Code.StartsWith("COD", StringComparison.OrdinalIgnoreCase) || eq.Code.StartsWith("CRYO", StringComparison.OrdinalIgnoreCase))
        {
            var activeCryovials = await _db.Cryovials
                .Include(c => c.Material)
                .Include(c => c.Organism)
                .Where(c => !c.IsDestroyed && c.VialsRemaining > 0)
                .ToListAsync();

            var cryoUserIds = activeCryovials.Select(c => c.PreparedByUserId).Distinct().ToList();
            var cryoUserMap = await _db.Users.Where(u => cryoUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

            foreach (var c in activeCryovials)
            {
                var organismName = c.Organism?.ScientificName ?? c.OrganismNameSnapshot ?? "Microorganism Strain";
                var preparedBy = cryoUserMap.TryGetValue(c.PreparedByUserId, out var pName) ? pName : "Laboratory Analyst";

                activities.Add(new EquipmentActivityDto(
                    c.Id,
                    organismName,
                    c.Code,
                    "Cryovial Storage",
                    $"Storage of strain: {organismName} ({c.VialsRemaining} vials remaining)",
                    c.PreparedAt,
                    preparedBy,
                    c.ExpiryDate,
                    null,
                    true,
                    c.Id,
                    "Cryovial"
                ));
            }
        }

        // 4. Identity Confirmation Entries for Cryovials in Incubators
        var activeIdentityConfirmations = await _db.IdentityConfirmationEntries
            .Include(i => i.Cryovial)
            .Include(i => i.Media)
            .Where(i => (i.IncubatorEquipmentId == eq.Id || (matchingMasterId.HasValue && i.IncubatorEquipmentId == matchingMasterId.Value))
                     && i.IncubationEnd > now)
            .ToListAsync();

        foreach (var idConf in activeIdentityConfirmations)
        {
            var cryoCode = idConf.Cryovial?.Code ?? "Cryovial Lot";
            var mediaName = idConf.Media?.LotNumber ?? "Confirmation Media";

            activities.Add(new EquipmentActivityDto(
                idConf.Id,
                "Cryovial Identity Confirmation",
                cryoCode,
                "GPT / Confirmation",
                $"Identity confirmation on media {mediaName}",
                idConf.IncubationStart,
                "Laboratory Analyst",
                idConf.IncubationEnd,
                null,
                true,
                idConf.CryovialId,
                "Cryovial"
            ));
        }

        return activities.OrderByDescending(a => a.StartedOn).ToList();
    }
}
