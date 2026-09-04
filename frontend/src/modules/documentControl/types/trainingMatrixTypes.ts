export interface TrainingMatrixCellDto {
  userId: number;
  userName: string;
  documentMasterId: number;
  companyDocumentCode: string;
  documentTitle: string;
  currentEffectiveRevisionId?: number | null;
  currentEffectiveRevisionNumber?: string | null;
  assignmentId?: number | null;
  assignedRevisionId?: number | null;
  assignedRevisionNumber?: string | null;
  cellStatus: "Qualified" | "Pending" | "Overdue" | "TrainedOnSupersededOnly" | "SupersededIncomplete" | "NotAssigned" | string;
  dueDateUtc?: string | null;
  acknowledgedAtUtc?: string | null;
  isOverdue: boolean;
  daysRemainingOrOverdue?: number | null;
  isRequiredByCurriculum: boolean;
}

export interface TrainingMatrixUserHeaderDto {
  userId: number;
  fullName: string;
  username: string;
  roleId: number;
  roleName: string;
  departmentId?: number | null;
  departmentName?: string | null;
}

export interface TrainingMatrixDocumentHeaderDto {
  documentMasterId: number;
  companyDocumentCode: string;
  microLimsDocumentId: string;
  title: string;
  documentTypeName: string;
  currentEffectiveRevisionId?: number | null;
  currentEffectiveRevisionNumber?: string | null;
}

export interface TrainingMatrixFilterDto {
  departmentId?: number | null;
  roleId?: number | null;
  userId?: number | null;
  documentMasterId?: number | null;
  documentTypeId?: number | null;
  complianceStatus?: string | null;
}

export interface TrainingMatrixGridDto {
  users: TrainingMatrixUserHeaderDto[];
  documents: TrainingMatrixDocumentHeaderDto[];
  cells: TrainingMatrixCellDto[];
  totalUsers: number;
  totalDocuments: number;
  totalCompliantCells: number;
  totalPendingCells: number;
  totalOverdueCells: number;
  totalSupersededGapCells: number;
}

export interface DepartmentComplianceKpiDto {
  departmentId: number;
  departmentName: string;
  requiredCount: number;
  completedCount: number;
  overdueCount: number;
  complianceRatePercentage: number;
}

export interface TopOverdueDocumentKpiDto {
  documentMasterId: number;
  companyDocumentCode: string;
  title: string;
  overdueCount: number;
}

export interface ComplianceKpiSummaryDto {
  totalTrackedUsers: number;
  totalEffectiveDocuments: number;
  totalRequiredAssignments: number;
  completedAssignments: number;
  pendingAssignments: number;
  overdueAssignments: number;
  supersededGapCount: number;
  overallComplianceRatePercentage: number;
  departmentCompliance: DepartmentComplianceKpiDto[];
  topOverdueDocuments: TopOverdueDocumentKpiDto[];
}

export interface DocumentComplianceDetailDto {
  documentMasterId: number;
  companyDocumentCode: string;
  microLimsDocumentId: string;
  title: string;
  currentEffectiveRevisionId?: number | null;
  currentEffectiveRevisionNumber?: string | null;
  assignedUserCount: number;
  completedUserCount: number;
  pendingUserCount: number;
  overdueUserCount: number;
  supersededGapUserCount: number;
  userStatuses: TrainingMatrixCellDto[];
}

export interface UserComplianceDetailDto {
  userId: number;
  fullName: string;
  username: string;
  roleName: string;
  departmentName?: string | null;
  totalRequiredDocuments: number;
  completedDocuments: number;
  pendingDocuments: number;
  overdueDocuments: number;
  supersededGapDocuments: number;
  complianceRatePercentage: number;
  documentStatuses: TrainingMatrixCellDto[];
}
