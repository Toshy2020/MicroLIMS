// Where clicking a notification should take the user when it is not about a
// specific sample.
export const NOTIFICATION_ROUTES: Record<string, string> = {
  MediaExpiry: "/laboratory-configuration/media",
  SolutionExpiry: "/preparation",
  StrainExpiry: "/laboratory-configuration/cryovials",
  CalibrationDue: "/inventory/equipment",
  EquipmentCalibrationOverdue: "/inventory/equipment",
  EquipmentPmOverdue: "/inventory/equipment",
  IncubationReady: "/receiving-testing",
  ApprovalWaiting: "/receiving-testing?status=UnderApproval",
  ReviewWaiting: "/receiving-testing?status=UnderReview",
  ConfigurationApprovalWaiting: "/receiving-testing",
  PendingPreparationConfigApproval: "/receiving-testing",
  TestReturnedForRevision: "/receiving-testing",
  TestReturned: "/my-work",
  TestAssigned: "/my-work",
  ItemOverdue: "/my-work",
  Escalation: "/my-work",
  DocumentTask: "/my-work",
  TrainingAssigned: "/document-control/my-reading-list",
  DocumentReassigned: "/document-control/reassignments",
  ReassignmentOverdue: "/document-control/reassignments",
  TestReturnedForBiochemical: "/receiving-testing",
  Discussion: "/discussions",
  DiscussionComment: "/discussions",
  DiscussionPostUpdated: "/discussions",
  DirectMessage: "/messages",
  MonitoringOverdue: "/monitoring-plan",
  MonitoringDeviationsOpen: "/monitoring-plan",
  MonitoringDueThisMonth: "/monitoring-plan"
};

export interface NotificationRouteTarget {
  type: string;
  sampleId?: number | null;
  testOrderId?: number | null;
  target?: {
    sampleId?: number | null;
    testOrderId?: number | null;
  } | null;
}

// A notification about one test on one sample opens that sample (and test)
// in the workspace; everything else goes to its type's page.
export function resolveNotificationTarget(notification: NotificationRouteTarget): string | undefined {
  const sampleId = notification.target?.sampleId ?? notification.sampleId;
  const testOrderId = notification.target?.testOrderId ?? notification.testOrderId;
  if (sampleId) {
    const testPart = testOrderId ? `&testOrderId=${testOrderId}` : "";
    return `/receiving-testing?sampleId=${sampleId}${testPart}`;
  }
  return NOTIFICATION_ROUTES[notification.type];
}

export interface NotificationTypeOption {
  value: string;
  label: string;
}

export const NOTIFICATION_TYPE_OPTIONS: readonly NotificationTypeOption[] = [
  { value: "ReviewWaiting", label: "Sample awaiting review" },
  { value: "ApprovalWaiting", label: "Sample awaiting approval" },
  { value: "TestReturned", label: "Test returned to analyst" },
  { value: "TestAssigned", label: "Test assigned" },
  { value: "IncubationReady", label: "Incubation ready to read" },
  { value: "MediaExpiry", label: "Media lot expiring or expired" },
  { value: "SolutionExpiry", label: "Solution lot expiring or expired" },
  { value: "StrainExpiry", label: "Reference strain expiring or expired" },
  { value: "CalibrationDue", label: "Equipment calibration due" },
  { value: "ItemOverdue", label: "Item overdue" },
  { value: "Escalation", label: "Escalation triggered" },
  { value: "DocumentTask", label: "Document task assigned" },
  { value: "TrainingAssigned", label: "Training assigned" },
  { value: "DirectMessage", label: "New direct message" },
  { value: "Discussion", label: "Discussion activity" },
  { value: "ConfigurationApprovalWaiting", label: "Configuration awaiting approval" },
  { value: "PendingPreparationConfigApproval", label: "Preparation configuration awaiting approval" },
  { value: "TestReturnedForRevision", label: "Test returned for revision" },
  { value: "TestReturnedForBiochemical", label: "Test returned for biochemical confirmation" },
  { value: "DiscussionPostUpdated", label: "Discussion post updated" },
  { value: "DiscussionComment", label: "New discussion comment" },
  { value: "DocumentReassigned", label: "Document responsibility reassigned" },
  { value: "ReassignmentOverdue", label: "Document reassignment overdue" },
  { value: "MonitoringOverdue", label: "Monitoring occurrence overdue" },
  { value: "MonitoringDeviationsOpen", label: "Open monitoring deviations" },
  { value: "MonitoringDueThisMonth", label: "Monitoring due this month" },
  { value: "EquipmentCalibrationOverdue", label: "Equipment calibration task overdue" },
  { value: "EquipmentPmOverdue", label: "Equipment preventive maintenance overdue" }
];
