namespace MicroLIMS.Application.DTOs;

public record StandardComparisonContextDto(string ResponseMode, string? StageRole, int? SampleReplicates, int? StandardReplicates, decimal SampleWeighInTolerancePercent, decimal? MaxPreparationRsdPercent, string? Message);
