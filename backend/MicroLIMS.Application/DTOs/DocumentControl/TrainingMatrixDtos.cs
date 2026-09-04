using System;
using System.Collections.Generic;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public record TrainingMatrixCellDto(
    int UserId,
    string UserName,
    int DocumentMasterId,
    string CompanyDocumentCode,
    string DocumentTitle,
    int? CurrentEffectiveRevisionId,
    string? CurrentEffectiveRevisionNumber,
    int? AssignmentId,
    int? AssignedRevisionId,
    string? AssignedRevisionNumber,
    string CellStatus, // "Qualified", "Pending", "Overdue", "TrainedOnSupersededOnly", "SupersededIncomplete", "NotAssigned"
    DateTime? DueDateUtc,
    DateTime? AcknowledgedAtUtc,
    bool IsOverdue,
    double? DaysRemainingOrOverdue,
    bool IsRequiredByCurriculum
);

public record TrainingMatrixUserHeaderDto(
    int UserId,
    string FullName,
    string Username,
    int RoleId,
    string RoleName,
    int? DepartmentId,
    string? DepartmentName
);

public record TrainingMatrixDocumentHeaderDto(
    int DocumentMasterId,
    string CompanyDocumentCode,
    string MicroLimsDocumentId,
    string Title,
    string DocumentTypeName,
    int? CurrentEffectiveRevisionId,
    string? CurrentEffectiveRevisionNumber
);

public record TrainingMatrixFilterDto(
    int? DepartmentId = null,
    int? RoleId = null,
    int? UserId = null,
    int? DocumentMasterId = null,
    int? DocumentTypeId = null,
    string? ComplianceStatus = null // "Qualified", "Pending", "Overdue", "TrainedOnSupersededOnly", "NotAssigned"
);

public record TrainingMatrixGridDto(
    List<TrainingMatrixUserHeaderDto> Users,
    List<TrainingMatrixDocumentHeaderDto> Documents,
    List<TrainingMatrixCellDto> Cells,
    int TotalUsers,
    int TotalDocuments,
    int TotalCompliantCells,
    int TotalPendingCells,
    int TotalOverdueCells,
    int TotalSupersededGapCells
);

public record ComplianceKpiSummaryDto(
    int TotalTrackedUsers,
    int TotalEffectiveDocuments,
    int TotalRequiredAssignments,
    int CompletedAssignments,
    int PendingAssignments,
    int OverdueAssignments,
    int SupersededGapCount,
    double OverallComplianceRatePercentage, // (Completed / TotalRequired) * 100
    List<DepartmentComplianceKpiDto> DepartmentCompliance,
    List<TopOverdueDocumentKpiDto> TopOverdueDocuments
);

public record DepartmentComplianceKpiDto(
    int DepartmentId,
    string DepartmentName,
    int RequiredCount,
    int CompletedCount,
    int OverdueCount,
    double ComplianceRatePercentage
);

public record TopOverdueDocumentKpiDto(
    int DocumentMasterId,
    string CompanyDocumentCode,
    string Title,
    int OverdueCount
);

public record DocumentComplianceDetailDto(
    int DocumentMasterId,
    string CompanyDocumentCode,
    string MicroLimsDocumentId,
    string Title,
    int? CurrentEffectiveRevisionId,
    string? CurrentEffectiveRevisionNumber,
    int AssignedUserCount,
    int CompletedUserCount,
    int PendingUserCount,
    int OverdueUserCount,
    int SupersededGapUserCount,
    List<TrainingMatrixCellDto> UserStatuses
);

public record UserComplianceDetailDto(
    int UserId,
    string FullName,
    string Username,
    string RoleName,
    string? DepartmentName,
    int TotalRequiredDocuments,
    int CompletedDocuments,
    int PendingDocuments,
    int OverdueDocuments,
    int SupersededGapDocuments,
    double ComplianceRatePercentage,
    List<TrainingMatrixCellDto> DocumentStatuses
);
