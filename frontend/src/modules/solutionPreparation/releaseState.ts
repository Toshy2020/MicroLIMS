import { formatLabDateTime } from "../../utils/formatDate";
import type { ApprovalGateStatus, SolutionPreparationResponse } from "./types";

// Short label for a release gate value, as shown in lists and pickers.
export function approvalStatusLabel(status: ApprovalGateStatus | null | undefined): string | null {
  switch (status) {
    case "PendingReview":
      return "Awaiting release";
    case "Approved":
      return "Released";
    case "Rejected":
      return "Rejected";
    default:
      return null;
  }
}

// Full sentence for the preparation record. Null when the recipe needs no release.
export function releaseStateText(
  p: Pick<SolutionPreparationResponse, "approvalStatus" | "releasedByUserName" | "releasedAt" | "releaseComment">
): string | null {
  const when = p.releasedAt ? ` on ${formatLabDateTime(p.releasedAt)}` : "";
  switch (p.approvalStatus) {
    case "PendingReview":
      return "Awaiting release";
    case "Approved":
      return `Released by ${p.releasedByUserName ?? "—"}${when}`;
    case "Rejected":
      return `Rejected by ${p.releasedByUserName ?? "—"}${when}${p.releaseComment ? `: ${p.releaseComment}` : ""}`;
    default:
      return null;
  }
}
