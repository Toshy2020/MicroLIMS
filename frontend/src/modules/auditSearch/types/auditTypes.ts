export interface AuditLogItem {
  id: number;
  entityName: string;
  entityId: string;
  action: string;
  previousValue: string | null;
  newValue: string | null;
  userId: number;
  userName: string;
  userRole: string | null;
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
}

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
