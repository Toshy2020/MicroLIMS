import { Box, Paper, Typography, Stack, useTheme } from "@mui/material";
import { SampleCard as SampleCardType } from "./types/workspaceTypes";
import { CategoryBadge } from "../../components/StatusBadge";
import { Ltr } from "../../components/Ltr";
import { useTranslation } from "../../i18n";
import { clickable } from "../../utils/clickable";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";

const STATUSES = ["Received", "InTesting", "UnderReview", "UnderApproval", "Approved", "Rejected", "RetestRequested"] as const;

// Read-only, grouped by Sample.status - clicking a card opens the same
// Sample Summary dialog the table's lifecycle badge opens. No
// drag-and-drop: dragging a card across columns would need to respect
// the workflow engine's state machine, which this view doesn't attempt.
export function SampleKanbanView({ samples, onCardClick }: { samples: SampleCardType[]; onCardClick: (sampleId: number) => void }) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  return (
    <Box sx={{ display: "flex", gap: 2, overflowX: "auto", pb: 1 }}>
      {STATUSES.map((status) => {
        const items = samples.filter((s) => s.status === status);
        return (
          <Box key={status} sx={{ minWidth: 260, flex: "0 0 260px" }}>
            <Stack
              direction="row"
              sx={{
                justifyContent: "space-between",
                alignItems: "center",
                mb: 1
              }}>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main }}>{t(`kanban.${status}`)}</Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}><Ltr>{items.length}</Ltr></Typography>
            </Stack>
            <Stack spacing={1} sx={{ maxHeight: 560, overflowY: "auto" }}>
              {items.map((s) => (
                <Paper
                  key={s.sampleId} onClick={() => onCardClick(s.sampleId)}
                  sx={{ p: 1.25, cursor: "pointer", "&:hover": { boxShadow: theme.palette.mode === "dark" ? "0 2px 6px rgba(0,0,0,0.4)" : "0 2px 6px rgba(0,0,0,0.12)" } }}
                  {...clickable(() => onCardClick(s.sampleId))}
                >
                  <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{s.displayName}</Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5 }}><Ltr>{s.referenceNumber}</Ltr></Typography>
                  <Stack
                    direction="row"
                    sx={{
                      justifyContent: "space-between",
                      alignItems: "center"
                    }}>
                    <CategoryBadge category={s.category} />
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}><Ltr>{formatDateTimeWithTimeZone(s.receivedAt)}</Ltr></Typography>
                  </Stack>
                </Paper>
              ))}
              {items.length === 0 && <Typography sx={{ fontSize: 12, color: "text.secondary", textAlign: "center", py: 2 }}>—</Typography>}
            </Stack>
          </Box>
        );
      })}
    </Box>
  );
}
