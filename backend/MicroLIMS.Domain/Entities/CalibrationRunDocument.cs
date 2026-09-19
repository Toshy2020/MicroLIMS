namespace MicroLIMS.Domain.Entities;

public class CalibrationRunDocument
{
    public int Id { get; set; }

    public int CalibrationRunId { get; set; }
    public CalibrationRun? CalibrationRun { get; set; }

    public string StorageKey { get; set; } = string.Empty; // calibration-runs/{runId}/{guid}{ext}
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;

    public int UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }

    public DateTime UploadedAt { get; set; }
}
