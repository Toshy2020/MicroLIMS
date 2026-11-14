namespace MicroLIMS.Application.DTOs;

public sealed record ListExportLogDto(int ExportId, DateTime ServerTimeUtc, string ExportedBy);
