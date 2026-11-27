using System.Text.Json.Serialization;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.DTOs;

// One test's analyst in a batch assignment; null unassigns the test.
public record TestAnalystAssignment(int TestOrderId, int? AnalystUserId);

// Someone the acting user may put on at least one of the sample's tests.
public record AssignableUserDto(int Id, string FullName, string Username, string RoleName, string RoleType);

// A test on the sample as the Assign Analyst dialog shows it. CanAssign is
// false for a closed test or one in a laboratory section the acting user
// isn't a member of; EligibleUserIds are the users who may take this test.
public record AssignableTestDto(
    int TestOrderId,
    string TestCode,
    int SectionId,
    string SectionName,
    string Status,
    int? AssignedAnalystId,
    string? AssignedAnalystName,
    bool CanAssign,
    IReadOnlyList<int> EligibleUserIds,
    IReadOnlyList<AssignmentCompetencyRefusalDto> CompetencyRefusals);

public sealed record AssignmentCompetencyRefusalDto(
    int UserId, string Code, IReadOnlyDictionary<string, string> Params, string Reason);

public record SampleAssignmentOptionsDto(
    int SampleId,
    IReadOnlyList<AssignableTestDto> Tests,
    IReadOnlyList<AssignableUserDto> Users);

public sealed record BulkAssignRequest(IReadOnlyList<int> SampleIds, int AnalystUserId, string? Reason);

public sealed record BulkAssignRowResult(int SampleId, string ReferenceNumber, bool Assigned, string? RefusedReason)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefusedCode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ApiError>? ErrorDetails { get; init; }
}

public sealed record BulkAssignResult(int AssignedCount, int RefusedCount, IReadOnlyList<BulkAssignRowResult> Rows);

