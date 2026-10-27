export interface AuditLogItem {
  id: number;
  entityName: string;
  entityId: string;
  action: string;
  previousValue: string | null;
  newValue: string | null;
  userId: number;
  userName: string;
  // The user's role today, not necessarily when the event happened.
  userCurrentRole: string | null;
  userUsername: string | null;
  timestamp: string;
  batchNumber: string | null;
  controlNumber: string | null;
  sampleReferenceNumber: string | null;
  mediaLotNumber: string | null;
  referenceStrainCode: string | null;
  cryovialCode: string | null;
  sampleId: number | null;
  testOrderId: number | null;
  actionCategory: string | null;
  actorType: string | null;
  correlationId: string | null;
  reason: string | null;
  // EntityId is negative: a temporary key was recorded before the insert (decision D8).
  entityIdNegative: boolean;
  // Id of the appended correction row for a negative-EntityId row, when one exists.
  correctedByAuditLogId: number | null;
}

export interface AuditSearchPageResult {
  items: AuditLogItem[];
  nextCursor: number | null;
  hasMore: boolean;
}

export interface SecurityEventItem {
  id: number;
  occurredAtUtc: string;
  eventCode: string;
  outcome: string;
  actorUserId: number | null;
  actorIsSystem: boolean;
  targetUserId: number | null;
  targetUsername: string | null;
  correlationId: string | null;
  ipAddress: string | null;
  requestPath: string | null;
  source: string;
  reason: string | null;
}

export interface LoginHistoryItem {
  id: number;
  userId: number | null;
  username: string;
  success: boolean;
  failureReason: string | null;
  ipAddress: string | null;
  timestamp: string;
}

export interface CursorPage<T> {
  items: T[];
  nextCursor: number | null;
  hasMore: boolean;
}

// Mirrors PermissionConstants.SystemViewSecurityAudit; the backend enforces it.
export const SECURITY_AUDIT_PERMISSION = "System.ViewSecurityAudit";

export const ACTION_CATEGORIES = [
  "Document", "Review", "Approval", "Training", "Kaf", "Configuration", "Security", "ImportExport", "Other"
] as const;

export interface AuditSearchFilterState {
  fromDate: string;
  toDate: string;
  batchNumber: string;
  controlNumber: string;
  sampleReferenceNumber: string;
  mediaLotNumber: string;
  referenceStrainCode: string;
  cryovialCode: string;
  entityName: string;
  action: string;
  userId: string;
  actionCategory: string;
  actorType: string;
  correlationId: string;
  reason: string;
  negativeEntityId: boolean;
}

export interface AuditFieldDiff {
  fieldName: string;
  displayName: string;
  oldValue: any;
  newValue: any;
  hasChanged: boolean;
}

export interface AuditTraceabilityNode {
  nodeType: string;
  identifier: string;
  title: string;
  status: string | null;
  description: string | null;
  entityId: number | null;
  navigationTarget: string | null;
  timestamp: string | null;
}

export interface AuditTraceabilityResult {
  primaryCategory: string;
  rootIdentifier: string;
  nodes: AuditTraceabilityNode[];
}

export const ENTITY_DISPLAY_NAMES: Record<string, string> = {
  Sample: "Sample Record",
  TestOrder: "Test Order",
  Result: "Test Result",
  WorkflowStepResult: "Workflow Result",
  ResultRecord: "Result Record Projection",
  ReviewWorkflowEvent: "Review / Approval Event",
  ElectronicSignature: "Electronic Signature",
  ReportSnapshot: "COA Report Snapshot",
  Media: "Media Batch",
  MediaEvaluation: "Media Growth Promotion",
  Material: "Material Lot",
  MaterialDocument: "Material Document / COA",
  Cryovial: "Working Cryovial Culture",
  Organism: "Master Reference Strain",
  EquipmentInventory: "Equipment Record",
  EquipmentDocument: "Calibration Certificate",
  EquipmentStatusHistory: "Equipment Status Transition",
  Item: "Item Master",
  ItemPreparationConfiguration: "Preparation Configuration",
  WaterSamplingPoint: "Water Sampling Point",
  Department: "EM Department",
  Room: "EM Room"
};

export const FIELD_DISPLAY_NAMES: Record<string, string> = {
  Status: "Operational / Record Status",
  CalculatedResult: "Calculated Result",
  NumericResult: "Numeric Value",
  EvaluationStatus: "Evaluation Result",
  PreparationStatus: "Preparation Status",
  CurrentStep: "Workflow Step",
  QuantityRemaining: "Remaining Quantity",
  QuantityReceived: "Received Quantity",
  BatchNumber: "Batch Number",
  ControlNumber: "Control Number",
  ReferenceNumber: "Sample Reference",
  Location: "Location",
  StorageCondition: "Storage Condition",
  ExpiryDate: "Expiry Date",
  ReceivingDate: "Receiving Date",
  PreparationDate: "Preparation Date",
  SetPointTemperature: "Set Point Temperature",
  CalibrationDueDate: "Calibration Due Date",
  Comment: "Comment / Reason",
  Decision: "Review Decision",
  GrowthObserved: "Growth Observed",
  ApprovedByUserId: "Approver ID",
  ReviewedByUserId: "Reviewer ID",
  ReceivedByUserId: "Receiver ID",
  AssignedAnalystId: "Assigned Analyst ID"
};
