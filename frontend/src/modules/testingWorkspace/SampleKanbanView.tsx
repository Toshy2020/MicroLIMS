import { Box, Paper, Typography, Stack } from "@mui/material";
import { SampleCard as SampleCardType } from "./types/workspaceTypes";
import { CategoryBadge } from "../../components/StatusBadge";
import { brandColors } from "../../theme";

const COLUMNS: { status: string; label: string }[] = [
  { status: "Received", label: "Received" },
  { status: "InTesting", label: "Under Testing" },
  { status: "UnderReview", label: "Under Review" },
  { status: "UnderApproval", label: "Under Approval" },
  { status: "Approved", label: "Approved" },
  { status: "Rejected", label: "Rejected" },
  { status: "RetestRequested", label: "Retest Requested" }
];

const formatDate = (d: string) => new Date(d).toLocaleDateString();

// Read-only, grouped by Sample.status - clicking a card opens the same
// Sample Summary dialog the table's lifecycle badge opens. No
// drag-and-drop: dragging a card across columns would need to respect
// the workflow engine's state machine, which this view doesn't attempt.
export function SampleKanbanView({ samples, onCardClick }: { samples: SampleCardType[]; onCardClick: (sampleId: number) => void }) {
  return (
    <Box sx={{ display: "flex", gap: 2, overflowX: "auto", pb: 1 }}>
      {COLUMNS.map((col) => {
        const items = samples.filter((s) => s.status === col.status);
        return (
          <Box key={col.status} sx={{ minWidth: 260, flex: "0 0 260px" }}>
            <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.sectionTitle }}>{col.label}</Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{items.length}</Typography>
            </Stack>
            <Stack spacing={1} sx={{ maxHeight: 560, overflowY: "auto" }}>
              {items.map((s) => (
                <Paper
                  key={s.sampleId} onClick={() => onCardClick(s.sampleId)}
                  sx={{ p: 1.25, cursor: "pointer", "&:hover": { boxShadow: "0 2px 6px rgba(0,0,0,0.12)" } }}
                >
                  <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{s.displayName}</Typography>
                  <Typography sx={{ fontSize: 11, color: "text.secondary", mb: 0.5 }}>{s.referenceNumber}</Typography>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <CategoryBadge category={s.category} />
                    <Typography sx={{ fontSize: 10, color: "text.secondary" }}>{formatDate(s.receivedAt)}</Typography>
                  </Stack>
                </Paper>
              ))}
              {items.length === 0 && <Typography sx={{ fontSize: 12, color: "#c0c0c0", textAlign: "center", py: 2 }}>—</Typography>}
            </Stack>
          </Box>
        );
      })}
    </Box>
  );
}
