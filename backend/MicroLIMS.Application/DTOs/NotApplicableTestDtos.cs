using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public record CreateNotApplicableTestRequest(
    int ItemId,
    string? Grade,
    string TestCode,
    string Justification,
    string Criticality,
    string ImmediateAction,
    string LongTermAction,
    string? Reference);

public record NotApplicableTestDto(
    int Id,
    int ItemId,
    string? Grade,
    string TestCode,
    string Justification,
    string Criticality,
    string ImmediateAction,
    string LongTermAction,
    string? Reference,
    NotApplicableStatus Status,
    int? HeadSignatureId,
    int? FinalSignatureId,
    int CreatedByUserId,
    DateTime CreatedAt,
    string TestDisplayName,
    string? HeadSignedBy,
    DateTime? HeadSignedAt,
    string? FinalSignedBy,
    DateTime? FinalSignedAt);

public record NotApplicableSignRequest(string Password, string? Comment);

public record NotApplicableWithdrawRequest(string Password, string Reason);
