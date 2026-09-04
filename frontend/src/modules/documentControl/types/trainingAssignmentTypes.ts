export type AssignmentType = "Reading" | "Retraining" | "PeriodicRecertification";

export type TrainingAssignmentStatus =
  | "Assigned"
  | "Reading"
  | "Acknowledged"
  | "CompletedPassed"
  | "Failed"
  | "Overdue"
  | "SupersededIncomplete"
  | "TrainedOnSupersededOnly"
  | "Cancelled";

export interface DocumentTrainingAssignmentDto {
  id: number;
  documentMasterId: number;
  microLimsDocumentId: string;
  companyDocumentCode: string;
  documentTitle: string;
  documentRevisionId: number;
  revisionNumber: string;
  assignedUserId: number;
  assignedUserName: string;
  assignmentType: AssignmentType;
  status: TrainingAssignmentStatus;
  assignedDateUtc: string;
  dueDateUtc: string;
  acknowledgedAtUtc?: string | null;
  statementText?: string | null;
  acknowledgedByUserId?: number | null;
  acknowledgedByUserName?: string | null;
  completedAtUtc?: string | null;
  supersededAtUtc?: string | null;
  closedReason?: string | null;
  sourceAssignmentId?: number | null;
  assignmentReason?: string | null;
  createdByUserId: number;
  createdByUserName: string;
  createdAtUtc: string;
  isOverdue: boolean;
  daysRemainingOrOverdue: number;
}

export interface DocumentTrainingAssignmentFilter {
  documentMasterId?: number;
  documentRevisionId?: number;
  assignedUserId?: number;
  status?: TrainingAssignmentStatus;
  assignmentType?: AssignmentType;
  overdueOnly?: boolean;
  page?: number;
  pageSize?: number;
}
