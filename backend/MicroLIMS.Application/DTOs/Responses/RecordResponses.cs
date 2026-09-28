using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

// Response types for roles, login history, archived records and the
// reporting result rows.

public class RoleResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public RoleType Type { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public bool IsSystemRole { get; init; }
    public bool IsActive { get; init; }

    public static RoleResponse From(Role e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Type = e.Type,
        Name = e.Name,
        Description = e.Description,
        IsSystemRole = e.IsSystemRole,
        IsActive = e.IsActive,
    };
}

public class LoginHistoryResponse
{
    public int Id { get; init; }
    public int? UserId { get; init; }
    public string Username { get; init; } = null!;
    public bool Success { get; init; }
    public string? FailureReason { get; init; }
    public string? IpAddress { get; init; }
    public DateTime Timestamp { get; init; }

    public static LoginHistoryResponse From(LoginHistory e) => new()
    {
        Id = e.Id,
        UserId = e.UserId,
        Username = e.Username,
        Success = e.Success,
        FailureReason = e.FailureReason,
        IpAddress = e.IpAddress,
        Timestamp = e.Timestamp,
    };
}

public class ArchivedRecordResponse
{
    public int Id { get; init; }
    public string EntityType { get; init; } = null!;
    public int EntityId { get; init; }
    public string DocumentId { get; init; } = null!;
    public string FileName { get; init; } = null!;
    public string StoragePath { get; init; } = null!;
    public long SizeBytes { get; init; }
    public string ContentSha256 { get; init; } = null!;
    public string Reason { get; init; } = null!;
    public int GeneratedByUserId { get; init; }
    public string GeneratedByNameSnapshot { get; init; } = null!;
    public DateTime GeneratedAt { get; init; }

    public static ArchivedRecordResponse From(ArchivedRecord e) => new()
    {
        Id = e.Id,
        EntityType = e.EntityType,
        EntityId = e.EntityId,
        DocumentId = e.DocumentId,
        FileName = e.FileName,
        StoragePath = e.StoragePath,
        SizeBytes = e.SizeBytes,
        ContentSha256 = e.ContentSha256,
        Reason = e.Reason,
        GeneratedByUserId = e.GeneratedByUserId,
        GeneratedByNameSnapshot = e.GeneratedByNameSnapshot,
        GeneratedAt = e.GeneratedAt,
    };
}

public class ResultRecordResponse
{
    public int Id { get; init; }
    public int SampleId { get; init; }
    public int TestOrderId { get; init; }
    public string SourceTable { get; init; } = null!;
    public int SourceId { get; init; }
    public string ReferenceNumber { get; init; } = null!;
    public SampleCategory Category { get; init; }
    public string SubjectName { get; init; } = null!;
    public string? SubjectDetail { get; init; }
    public string? BatchNumber { get; init; }
    public string? ControlNumber { get; init; }
    public string TestCode { get; init; } = null!;
    public string TestDisplayName { get; init; } = null!;
    public ResultKind ResultKind { get; init; }
    public decimal? NumericValue { get; init; }
    public string ReportedValue { get; init; } = null!;
    public string? Unit { get; init; }
    public bool IsBelowDetectionLimit { get; init; }
    public decimal? DetectionLimit { get; init; }
    public string? AlertLimit { get; init; }
    public string? ActionLimit { get; init; }
    public string? SpecLimit { get; init; }
    public ResultLevel ResultLevel { get; init; }
    public DateTime ResultEnteredAt { get; init; }
    public int ResultEnteredByUserId { get; init; }
    public string ResultEnteredByName { get; init; } = null!;
    public SampleStatus SampleStatus { get; init; }
    public int? ApprovedByUserId { get; init; }
    public string? ApprovedByName { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public int Round { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static ResultRecordResponse From(ResultRecord e) => new()
    {
        Id = e.Id,
        SampleId = e.SampleId,
        TestOrderId = e.TestOrderId,
        SourceTable = e.SourceTable,
        SourceId = e.SourceId,
        ReferenceNumber = e.ReferenceNumber,
        Category = e.Category,
        SubjectName = e.SubjectName,
        SubjectDetail = e.SubjectDetail,
        BatchNumber = e.BatchNumber,
        ControlNumber = e.ControlNumber,
        TestCode = e.TestCode,
        TestDisplayName = e.TestDisplayName,
        ResultKind = e.ResultKind,
        NumericValue = e.NumericValue,
        ReportedValue = e.ReportedValue,
        Unit = e.Unit,
        IsBelowDetectionLimit = e.IsBelowDetectionLimit,
        DetectionLimit = e.DetectionLimit,
        AlertLimit = e.AlertLimit,
        ActionLimit = e.ActionLimit,
        SpecLimit = e.SpecLimit,
        ResultLevel = e.ResultLevel,
        ResultEnteredAt = e.ResultEnteredAt,
        ResultEnteredByUserId = e.ResultEnteredByUserId,
        ResultEnteredByName = e.ResultEnteredByName,
        SampleStatus = e.SampleStatus,
        ApprovedByUserId = e.ApprovedByUserId,
        ApprovedByName = e.ApprovedByName,
        ApprovedAt = e.ApprovedAt,
        Round = e.Round,
        UpdatedAt = e.UpdatedAt,
    };
}
