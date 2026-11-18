import type { TFunction } from "i18next";
import { i18n } from "../../i18n";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import type { ApprovalGateStatus, SolutionPreparationResponse } from "./types";

// Short label for a release gate value, as shown in lists and pickers.
export function approvalStatusLabel(
  status: ApprovalGateStatus | null | undefined,
  t: TFunction<"solutionPreparation"> = i18n.getFixedT("en", "solutionPreparation")
): string | null {
  switch (status) {
    case "PendingReview":
      return t("approval.PendingReview");
    case "Approved":
      return t("approval.Approved");
    case "Rejected":
      return t("approval.Rejected");
    default:
      return null;
  }
}

// Full sentence for the preparation record. Null when the recipe needs no release.
export function releaseStateText(
  p: Pick<SolutionPreparationResponse, "approvalStatus" | "releasedByUserName" | "releasedAt" | "releaseComment">,
  t: TFunction<"solutionPreparation"> = i18n.getFixedT("en", "solutionPreparation")
): string | null {
  const user = p.releasedByUserName ?? "—";
  const date = p.releasedAt ? formatDateTimeWithTimeZone(p.releasedAt) : null;
  switch (p.approvalStatus) {
    case "PendingReview":
      return t("approval.PendingReview");
    case "Approved":
      return date ? t("approval.releasedByOn", { user, date }) : t("approval.releasedBy", { user });
    case "Rejected": {
      const base = date ? t("approval.rejectedByOn", { user, date }) : t("approval.rejectedBy", { user });
      return p.releaseComment ? t("approval.rejectedWithComment", { base, comment: p.releaseComment }) : base;
    }
    default:
      return null;
  }
}
