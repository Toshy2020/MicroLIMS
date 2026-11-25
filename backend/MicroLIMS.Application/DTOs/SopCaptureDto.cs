namespace MicroLIMS.Application.DTOs;

public sealed record SopCaptureDto(
    string Status, int? DocumentMasterId, string? DocumentCode, string? Title,
    int? RevisionId, string? RevisionNumber, DateTime? EffectiveDateUtc, DateTime? CapturedAtUtc);

public sealed record NewerSopRevisionDto(string RevisionNumber, DateTime EffectiveDateUtc);
