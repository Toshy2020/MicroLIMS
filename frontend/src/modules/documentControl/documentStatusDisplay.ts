import { DocumentMasterSummaryDto } from "./types/documentControlTypes";

/**
 * Status badge text for a document master row in the library / dashboard lists.
 *
 * ML-DC-FRS-1A-001 §5:104 requires a Status column on the library, and the
 * canonical revision lifecycle has eight states (ML-DC-FRS-1B-001 §4:376-389):
 * Draft, InReview, AwaitingApproval, FutureEffective, Effective, Superseded,
 * Obsolete, Cancelled.
 *
 * A voided master reports Void regardless of its revision history - the record
 * was registered in error and never held an effective revision
 * (ML-DC-FRS-1A-001 §5:117), so the master-level status is what matters.
 * Otherwise the badge is the real status of the master's current revision.
 *
 * The returned value is a raw status; StatusBadge maps it to the mandated
 * display string ("InReview" renders as "In Review") and to a tone, so callers
 * must not pre-format it here.
 */
export function documentMasterStatusLabel(doc: DocumentMasterSummaryDto): string {
  if (doc.recordStatus === "Void") return "Void";
  // Null means the master holds no revisions at all, which registration should
  // make impossible - it always creates Rev 01 as a Draft. Reported plainly
  // rather than guessed at, since naming the wrong lifecycle state is worse
  // than declining to name one.
  return doc.currentRevisionStatus ?? "No Revision";
}
