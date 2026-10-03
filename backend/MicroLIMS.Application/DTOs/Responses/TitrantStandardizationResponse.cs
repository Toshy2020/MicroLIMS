using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

public record TitrantStandardizationReplicateResponse(
    int Id, int ReplicateNo,
    int? StandardMaterialId, string? StandardLotBatchNumber, decimal? StandardWeightMg, decimal? StandardPurityPercent,
    int? ReferencePreparationId, string? ReferencePreparationCode, decimal? ReferenceVolumeMl, decimal? ReferenceFactor,
    decimal TitrantVolumeMl, decimal? BlankMl, decimal Factor);

// The preparation's standardization status as displayed on the record page:
// "NotStandardized" (never standardized, or every record failed),
// "Valid" (a passed record whose ValidUntil is in the future),
// "Due" (a passed record whose ValidUntil has passed),
// "BeforeEachUse" (a passed record with ValidityDays = 0 - ValidUntil is
// always null, so this titrant must be restandardized before every use).
public record CurrentFactorDto(decimal? Factor, DateTime? StandardizedAt, DateTime? ValidUntil, string State, decimal? TemperatureC = null);

public class TitrantStandardizationResponse
{
    public int Id { get; init; }
    public int SolutionPreparationId { get; init; }
    public StandardizationMode Mode { get; init; }
    public decimal MeanFactor { get; init; }
    public decimal? RsdPercent { get; init; }
    public bool Passed { get; init; }
    public string? FailureReasons { get; init; }
    public int StandardizedByUserId { get; init; }
    public string? StandardizedByUserName { get; init; }
    public DateTime StandardizedAt { get; init; }
    public DateTime? ValidUntil { get; init; }
    public decimal? TemperatureC { get; init; }
    public List<TitrantStandardizationReplicateResponse> Replicates { get; init; } = new();

    public static TitrantStandardizationResponse From(TitrantStandardization r, string? standardizedByUserName) => new()
    {
        Id = r.Id,
        SolutionPreparationId = r.SolutionPreparationId,
        Mode = r.Mode,
        MeanFactor = r.MeanFactor,
        RsdPercent = r.RsdPercent,
        Passed = r.Passed,
        FailureReasons = r.FailureReasons,
        StandardizedByUserId = r.StandardizedByUserId,
        StandardizedByUserName = standardizedByUserName,
        StandardizedAt = r.StandardizedAt,
        ValidUntil = r.ValidUntil,
        TemperatureC = r.TemperatureC,
        Replicates = r.Replicates
            .OrderBy(x => x.ReplicateNo)
            .Select(x => new TitrantStandardizationReplicateResponse(
                x.Id, x.ReplicateNo,
                x.StandardMaterialId, x.StandardMaterial?.LotLabel, x.StandardWeightMg, x.StandardPurityPercent,
                x.ReferencePreparationId, x.ReferencePreparation?.Code, x.ReferenceVolumeMl, x.ReferenceFactor,
                x.TitrantVolumeMl, x.BlankMl, x.Factor))
            .ToList(),
    };
}
