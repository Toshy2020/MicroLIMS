using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

// Material lines carry the picked lot; PreparedSolution lines the picked
// prepared bottle (SourcePreparationId/Code); Text lines only Text.
public record SolutionPreparationComponentResponse(
    int Id, int Order, SolutionComponentKind Kind, int? MaterialMasterEntryId, string EntryCode, string EntryName,
    decimal? RecipeQuantity, SolutionComponentUnit? RecipeUnit,
    int? MaterialId, string? LotBatchNumber, MaterialUnit? LotUnit, decimal? QuantityUsed,
    int? SourceSolutionMasterId = null, string? Text = null, int? SourcePreparationId = null, string? SourcePreparationCode = null);

public record SolutionPreparationStatusHistoryResponse(
    SolutionPreparationStatus? FromStatus, SolutionPreparationStatus ToStatus,
    int? ChangedByUserId, string? ChangedByUserName, DateTime ChangedAt, string? Reason);

public class SolutionPreparationResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public int SectionId { get; init; }
    public string? SectionName { get; init; }
    public string? Code { get; init; }
    public int SolutionMasterId { get; init; }
    public string SolutionMasterName { get; init; } = string.Empty;
    public SolutionType Type { get; init; }
    public int? HplcMethodId { get; init; }
    public string? HplcMethodAbbreviation { get; init; }
    public SolutionPreparationStatus Status { get; init; }
    public SolutionPreparationStatus EffectiveStatus { get; init; }
    public int StartedByUserId { get; init; }
    public string? StartedByUserName { get; init; }
    public DateTime StartedAt { get; init; }
    public int? PreparedByUserId { get; init; }
    public string? PreparedByUserName { get; init; }
    public DateTime? PreparedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public decimal? FinalVolumeMl { get; init; }
    public decimal? MeasuredPh { get; init; }
    public int? AutoclaveEquipmentId { get; init; }
    public string? AutoclaveEquipmentName { get; init; }
    public string? AutoclaveProgram { get; init; }
    public string? LoadType { get; init; }
    public decimal? AutoclaveTemperature { get; init; }
    public int? AutoclaveCycleTime { get; init; }
    public int? AutoclaveCycleNumber { get; init; }
    // Null when the recipe needs no release.
    public ApprovalGateStatus? ApprovalStatus { get; init; }
    public int? ReleasedByUserId { get; init; }
    public string? ReleasedByUserName { get; init; }
    public DateTime? ReleasedAt { get; init; }
    public string? ReleaseComment { get; init; }
    public string RecipeSnapshotJson { get; init; } = "{}";
    public List<SolutionPreparationComponentResponse> Components { get; init; } = new();
    public List<SolutionPreparationStatusHistoryResponse> StatusHistory { get; init; } = new();
    // Titrant only (HPLC chain S5) - the latest passed, still-relevant
    // standardization. Null for Mobile Phase / Diluent preparations.
    public CurrentFactorDto? CurrentFactor { get; init; }

    // A Prepared record past its expiry reads as Expired everywhere, even
    // before SolutionPreparationExpiryWorker has flipped the row (Review
    // Focus: "Opening a Prepared record after its expiry time but before
    // the worker ran").
    public static SolutionPreparationStatus EffectiveStatusOf(SolutionPreparation p, DateTime nowUtc) =>
        p.Status == SolutionPreparationStatus.Prepared && p.ExpiresAt.HasValue && p.ExpiresAt.Value <= nowUtc
            ? SolutionPreparationStatus.Expired
            : p.Status;

    public static SolutionPreparationResponse From(SolutionPreparation p, IReadOnlyDictionary<int, string> userNames, DateTime nowUtc, CurrentFactorDto? currentFactor = null)
    {
        string? NameOf(int? userId) => userId.HasValue && userNames.TryGetValue(userId.Value, out var n) ? n : null;

        return new SolutionPreparationResponse
        {
            Id = p.Id,
            Version = p.Version,
            SectionId = p.SectionId,
            SectionName = p.Section?.Name,
            Code = p.Code,
            SolutionMasterId = p.SolutionMasterId,
            SolutionMasterName = p.SolutionMaster?.Name ?? string.Empty,
            Type = p.Type,
            HplcMethodId = p.HplcMethodId,
            HplcMethodAbbreviation = p.HplcMethod?.Abbreviation,
            Status = p.Status,
            EffectiveStatus = EffectiveStatusOf(p, nowUtc),
            StartedByUserId = p.StartedByUserId,
            StartedByUserName = NameOf(p.StartedByUserId),
            StartedAt = p.StartedAt,
            PreparedByUserId = p.PreparedByUserId,
            PreparedByUserName = NameOf(p.PreparedByUserId),
            PreparedAt = p.PreparedAt,
            ExpiresAt = p.ExpiresAt,
            FinalVolumeMl = p.FinalVolumeMl,
            MeasuredPh = p.MeasuredPh,
            AutoclaveEquipmentId = p.AutoclaveEquipmentId,
            AutoclaveEquipmentName = p.AutoclaveEquipment?.Name,
            AutoclaveProgram = p.AutoclaveProgram,
            LoadType = p.LoadType,
            AutoclaveTemperature = p.AutoclaveTemperature,
            AutoclaveCycleTime = p.AutoclaveCycleTime,
            AutoclaveCycleNumber = p.AutoclaveCycleNumber,
            ApprovalStatus = p.ApprovalStatus,
            ReleasedByUserId = p.ReleasedByUserId,
            ReleasedByUserName = NameOf(p.ReleasedByUserId),
            ReleasedAt = p.ReleasedAt,
            ReleaseComment = p.ReleaseComment,
            RecipeSnapshotJson = p.RecipeSnapshotJson,
            Components = p.Components
                .OrderBy(c => c.Order)
                .Select(c => new SolutionPreparationComponentResponse(
                    c.Id, c.Order, c.Kind, c.MaterialMasterEntryId, c.EntryCode, c.EntryName,
                    c.RecipeQuantity, c.RecipeUnit,
                    c.MaterialId, c.Material?.LotLabel, c.Material?.Unit, c.QuantityUsed,
                    c.SourceSolutionMasterId, c.Text, c.SourcePreparationId, c.SourcePreparation?.Code))
                .ToList(),
            StatusHistory = p.StatusHistory
                .OrderBy(h => h.ChangedAt)
                .Select(h => new SolutionPreparationStatusHistoryResponse(
                    h.FromStatus, h.ToStatus, h.ChangedByUserId, NameOf(h.ChangedByUserId), h.ChangedAt, h.Reason))
                .ToList(),
            CurrentFactor = currentFactor,
        };
    }
}

public record SolutionPreparationListItem(
    int Id, string? Code, SolutionType Type, string SolutionMasterName, string? HplcMethodAbbreviation,
    SolutionPreparationStatus EffectiveStatus, DateTime? PreparedAt, DateTime? ExpiresAt, string? PreparedByUserName,
    ApprovalGateStatus? ApprovalStatus = null);   // PendingReview shows as "Awaiting release"
