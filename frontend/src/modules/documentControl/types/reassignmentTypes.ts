// Succession for deactivated users' document responsibilities (GET/POST /api/document-control/reassignments).

export type ReassignmentItemType = "Custodian" | "Assignment" | "ReviewTask" | "ApprovalTask" | "PeriodicReview";

export interface ReassignmentItem {
  itemType: ReassignmentItemType;
  itemId: number;
  departmentId: number;
  departmentName: string;
  documentMasterId: number;
  documentCode: string;
  documentTitle: string;
  revisionNumber: string | null;
  // The role the new holder takes over, and the permission their role must hold for it.
  roleNeeded: string;
  requiredPermission: string;
  // Always "Inactive user"; holderName is for the people who hand the work over.
  holderLabel: string;
  holderName: string | null;
  inactiveSince: string | null;
  isOverdue: boolean;
}

export interface ReassignmentQueue {
  items: ReassignmentItem[];
  total: number;
  overdue: number;
  // DocumentControl.ReassignmentEscalationDays
  escalationDays: number;
}

export interface ReassignmentCount {
  total: number;
  overdue: number;
}

export interface ReassignmentCandidate {
  userId: number;
  fullName: string;
  jobTitle: string | null;
  roleName: string;
}

export interface ReassignRequest {
  itemType: ReassignmentItemType;
  itemId: number;
  newUserId: number;
  reason: string;
}
