using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

public record SolutionPreparationComponentResponse(
    int Id, int Order, int MaterialMasterEntryId, string EntryCode, string EntryName,
    decimal RecipeQuantity, SolutionComponentUnit RecipeUnit,
    int? MaterialId, string? LotBatchNumber, MaterialUnit? LotUnit, decimal? QuantityUsed);

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
    public string RecipeSnapshotJson { get; init; } = "{}";
    public List<SolutionPreparationComponentResponse> Components { get; init; } = new();
    public List<SolutionPreparationStatusHistoryResponse> StatusHistory { get; init; } = new();

    // A Prepared record past its expiry reads as Expired everywhere, even
    // before SolutionPreparationExpiryWorker has flipped the row (Review
    // Focus: "Opening a Prepared record after its expiry time but before
    // the worker ran").
    public static SolutionPreparationStatus EffectiveStatusOf(SolutionPreparation p, DateTime nowUtc) =>
        p.Status == SolutionPreparationStatus.Prepared && p.ExpiresAt.HasValue && p.ExpiresAt.Value <= nowUtc
            ? SolutionPreparationStatus.Expired
            : p.Status;

    public static SolutionPreparationResponse From(SolutionPreparation p, IReadOnlyDictionary<int, string> userNames, DateTime nowUtc)
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
            RecipeSnapshotJson = p.RecipeSnapshotJson,
            Components = p.Components
                .OrderBy(c => c.Order)
                .Select(c => new SolutionPreparationComponentResponse(
                    c.Id, c.Order, c.MaterialMasterEntryId, c.EntryCode, c.EntryName,
                    c.RecipeQuantity, c.RecipeUnit,
                    c.MaterialId, c.Material?.BatchNumber, c.Material?.Unit, c.QuantityUsed))
                .ToList(),
            StatusHistory = p.StatusHistory
                .OrderBy(h => h.ChangedAt)
                .Select(h => new SolutionPreparationStatusHistoryResponse(
                    h.FromStatus, h.ToStatus, h.ChangedByUserId, NameOf(h.ChangedByUserId), h.ChangedAt, h.Reason))
                .ToList(),
        };
    }
}

public record SolutionPreparationListItem(
    int Id, string? Code, SolutionType Type, string SolutionMasterName, string? HplcMethodAbbreviation,
    SolutionPreparationStatus EffectiveStatus, DateTime? PreparedAt, DateTime? ExpiresAt, string? PreparedByUserName);
