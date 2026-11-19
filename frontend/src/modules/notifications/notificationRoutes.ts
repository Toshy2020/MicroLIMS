import type { TFunction } from "i18next";
import { i18n } from "../../i18n";

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

export function notificationTypeOptions(t: TFunction): readonly NotificationTypeOption[] {
  return [
    { value: "ReviewWaiting", label: t("types.ReviewWaiting") },
    { value: "ApprovalWaiting", label: t("types.ApprovalWaiting") },
    { value: "TestReturned", label: t("types.TestReturned") },
    { value: "TestAssigned", label: t("types.TestAssigned") },
    { value: "IncubationReady", label: t("types.IncubationReady") },
    { value: "MediaExpiry", label: t("types.MediaExpiry") },
    { value: "SolutionExpiry", label: t("types.SolutionExpiry") },
    { value: "StrainExpiry", label: t("types.StrainExpiry") },
    { value: "CalibrationDue", label: t("types.CalibrationDue") },
    { value: "ItemOverdue", label: t("types.ItemOverdue") },
    { value: "Escalation", label: t("types.Escalation") },
    { value: "DocumentTask", label: t("types.DocumentTask") },
    { value: "TrainingAssigned", label: t("types.TrainingAssigned") },
    { value: "DirectMessage", label: t("types.DirectMessage") },
    { value: "Discussion", label: t("types.Discussion") },
    { value: "ConfigurationApprovalWaiting", label: t("types.ConfigurationApprovalWaiting") },
    { value: "PendingPreparationConfigApproval", label: t("types.PendingPreparationConfigApproval") },
    { value: "TestReturnedForRevision", label: t("types.TestReturnedForRevision") },
    { value: "TestReturnedForBiochemical", label: t("types.TestReturnedForBiochemical") },
    { value: "DiscussionPostUpdated", label: t("types.DiscussionPostUpdated") },
    { value: "DiscussionComment", label: t("types.DiscussionComment") },
    { value: "DocumentReassigned", label: t("types.DocumentReassigned") },
    { value: "ReassignmentOverdue", label: t("types.ReassignmentOverdue") },
    { value: "MonitoringOverdue", label: t("types.MonitoringOverdue") },
    { value: "MonitoringDeviationsOpen", label: t("types.MonitoringDeviationsOpen") },
    { value: "MonitoringDueThisMonth", label: t("types.MonitoringDueThisMonth") },
    { value: "EquipmentCalibrationOverdue", label: t("types.EquipmentCalibrationOverdue") },
    { value: "EquipmentPmOverdue", label: t("types.EquipmentPmOverdue") }
  ];
}

// English labels, for code outside React.
export const NOTIFICATION_TYPE_OPTIONS = notificationTypeOptions(i18n.getFixedT("en", "notifications"));
