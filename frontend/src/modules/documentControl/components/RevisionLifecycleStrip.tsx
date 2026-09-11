import { Box, Typography, Tooltip } from "@mui/material";
import { statusTone } from "../../../theme/statusTokens";
import type { DocumentRevisionStatus } from "../types/documentControlTypes";

/**
 * Where the current revision sits in the controlled lifecycle.
 *
 * This is the first thing on the document page because it is the question every
 * reader of a GMP document has first: is this revision executable, or is it a
 * draft, retired, or waiting on somebody. Before it existed the page opened on
 * metadata and the lifecycle state was a single small chip among several.
 *
 * The lifecycle is a sequence, so a stepped device is honest here - but it is not
 * a straight line, and drawing it as one would misrepresent the process. The
 * approval path is the spine:
 *
 *     Draft --> In Review --> Awaiting Approval --> Effective
 *
 * Everything else is off-spine and is shown as the current position instead of
 * being faked as a step: Future Effective (approved, waiting for its date),
 * Superseded and Obsolete (past), Cancelled (abandoned before ever becoming
 * effective). ML-DC-FRS-1B-001 §4:376-389 defines the full set and its
 * transitions, including the ones that go backwards - Awaiting Approval can
 * return to Draft on a correction, which is precisely why the spine is drawn as
 * position-in-a-process rather than irreversible progress.
 */

const SPINE: { status: DocumentRevisionStatus; label: string }[] = [
  { status: "Draft", label: "Draft" },
  { status: "InReview", label: "In Review" },
  { status: "AwaitingApproval", label: "Awaiting Approval" },
  { status: "Effective", label: "Effective" }
];

// Off-spine states, with the plain-language reason each one is where it is.
const OFF_SPINE: Partial<Record<DocumentRevisionStatus, string>> = {
  FutureEffective: "Approved. Becomes effective automatically on its effective date.",
  Superseded: "Replaced by a later effective revision. Retained as a record.",
  Obsolete: "Withdrawn from use. No successor revision.",
  Cancelled: "Abandoned before approval. Retained as a record."
};

export function RevisionLifecycleStrip({
  status,
  revisionNumber
}: {
  status: DocumentRevisionStatus | null;
  revisionNumber?: string | null;
}) {
  if (!status) return null;

  const spineIndex = SPINE.findIndex((s) => s.status === status);
  const isOffSpine = spineIndex === -1;
  // A revision that reached Superseded or Obsolete did pass through the whole
  // approval path, so the spine reads as complete behind the off-spine marker.
  const completedThrough =
    status === "Superseded" || status === "Obsolete" || status === "FutureEffective"
      ? SPINE.length
      : spineIndex;

  return (
    <Box
      component="ol"
      aria-label="Revision lifecycle"
      sx={{
        listStyle: "none",
        display: "flex",
        flexWrap: "wrap",
        alignItems: "stretch",
        gap: 0.75,
        m: 0,
        p: 0
      }}
    >
      {SPINE.map((step, i) => {
        const isCurrent = i === spineIndex;
        const isDone = i < completedThrough;
        const tone = isCurrent ? statusTone(step.status) : null;

        return (
          <Box
            component="li"
            key={step.status}
            aria-current={isCurrent ? "step" : undefined}
            sx={{
              flex: "1 1 8rem",
              minWidth: "8rem",
              px: 1.5,
              py: 1,
              borderRadius: 1,
              // The current step is the only filled one. Past steps are quiet but
              // legible; future steps are outlined so the remaining path is still
              // readable without competing for attention.
              bgcolor: (t) =>
                isCurrent && tone
                  ? t.custom.status[tone].bg
                  : isDone
                    ? t.palette.action.hover
                    : "transparent",
              color: (t) =>
                isCurrent && tone ? t.custom.status[tone].text : t.palette.text.secondary,
              border: "1px solid",
              borderColor: (t) =>
                isCurrent && tone ? t.custom.status[tone].border : t.palette.divider,
              borderLeftWidth: isCurrent ? 4 : 1
            }}
          >
            <Typography
              variant="caption"
              sx={{ display: "block", fontWeight: isCurrent ? 700 : 600 }}
            >
              {step.label}
            </Typography>
            <Typography variant="caption" sx={{ display: "block", opacity: 0.85 }}>
              {isCurrent
                ? revisionNumber
                  ? `Revision ${revisionNumber} is here`
                  : "Current"
                : isDone
                  ? "Complete"
                  : "Not yet"}
            </Typography>
          </Box>
        );
      })}

      {isOffSpine && (
        <Tooltip title={OFF_SPINE[status] ?? ""}>
          <Box
            component="li"
            aria-current="step"
            sx={{
              flex: "1 1 12rem",
              minWidth: "12rem",
              px: 1.5,
              py: 1,
              borderRadius: 1,
              bgcolor: (t) => t.custom.status[statusTone(status)].bg,
              color: (t) => t.custom.status[statusTone(status)].text,
              border: "1px solid",
              borderColor: (t) => t.custom.status[statusTone(status)].border,
              borderLeftWidth: 4
            }}
          >
            <Typography variant="caption" sx={{ display: "block", fontWeight: 700 }}>
              {status === "FutureEffective" ? "Future Effective" : status}
            </Typography>
            <Typography variant="caption" sx={{ display: "block", opacity: 0.85 }}>
              {OFF_SPINE[status] ?? ""}
            </Typography>
          </Box>
        </Tooltip>
      )}
    </Box>
  );
}
