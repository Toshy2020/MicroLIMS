import { useEffect, useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  Chip,
  Divider,
  CircularProgress,
  Stack,
  IconButton
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import HistoryIcon from "@mui/icons-material/History";
import { formatLabDateTime } from "../utils/formatDate";
import { brandColors } from "../theme";
import { AuditSearchService } from "../modules/auditSearch/services/AuditSearchService";
import type { AuditLogItem } from "../modules/auditSearch/types/auditTypes";
import { ENTITY_DISPLAY_NAMES } from "../modules/auditSearch/types/auditTypes";
import { AuditDiffViewer } from "../modules/auditSearch/components/AuditDiffViewer";
import { AuditRawJsonViewer } from "../modules/auditSearch/components/AuditRawJsonViewer";

interface AuditHistoryDialogProps {
  open: boolean;
  onClose: () => void;
  entityName: string;
  entityId: number | string | null;
}

const ACTION_COLORS: Record<string, "success" | "warning" | "error" | "default"> = {
  Create: "success",
  Update: "warning",
  Delete: "error"
};

export function AuditHistoryDialog({
  open,
  onClose,
  entityName,
  entityId
}: AuditHistoryDialogProps) {
  const [entries, setEntries] = useState<AuditLogItem[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [expandedRawId, setExpandedRawId] = useState<number | null>(null);

  useEffect(() => {
    if (open && entityId != null) {
      setLoading(true);
      setEntries(null);
      AuditSearchService.getForEntity(entityName, entityId)
        .then((res) => setEntries(res))
        .catch(() => setEntries([]))
        .finally(() => setLoading(false));
    }
  }, [open, entityName, entityId]);

  const friendlyEntity = ENTITY_DISPLAY_NAMES[entityName] ?? entityName;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          pb: 1.5,
          bgcolor: "#faf5ff"
        }}
      >
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <HistoryIcon sx={{ color: brandColors.sectionTitle }} />
          <Typography variant="h6" sx={{ fontWeight: 700, color: brandColors.sectionTitle }}>
            Audit History · {friendlyEntity} #{entityId}
          </Typography>
        </Box>
        <IconButton size="small" onClick={onClose}>
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>

      <DialogContent dividers sx={{ p: 3, bgcolor: "#fcfcfd" }}>
        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
            <CircularProgress size={24} />
          </Box>
        ) : !entries || entries.length === 0 ? (
          <Typography color="text.secondary" sx={{ textAlign: "center", py: 3, fontStyle: "italic" }}>
            No audit log entries recorded for this entity.
          </Typography>
        ) : (
          <Stack divider={<Divider sx={{ my: 2 }} />} spacing={2}>
            {entries.map((entry) => (
              <Box
                key={entry.id}
                sx={{
                  p: 2,
                  bgcolor: "#ffffff",
                  borderRadius: 1.5,
                  border: "1px solid #e5e7eb",
                  boxShadow: "0 1px 2px rgba(0,0,0,0.02)"
                }}
              >
                {/* Event header */}
                <Box
                  sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    mb: 1.5,
                    flexWrap: "wrap",
                    gap: 1
                  }}
                >
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    <Chip
                      label={entry.action.toUpperCase()}
                      size="small"
                      color={ACTION_COLORS[entry.action] ?? "default"}
                      sx={{ fontWeight: 700, fontSize: 10, height: 20 }}
                    />
                    <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary" }}>
                      {entry.userName}
                    </Typography>
                    <Typography sx={{ fontSize: 11, color: "text.secondary", fontFamily: "monospace" }}>
                      (User #{entry.userId})
                    </Typography>
                  </Box>

                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                    {formatLabDateTime(entry.timestamp)} UTC
                  </Typography>
                </Box>

                {/* Field-level diff */}
                <Box sx={{ mb: 1.5 }}>
                  <AuditDiffViewer
                    action={entry.action}
                    previousValue={entry.previousValue}
                    newValue={entry.newValue}
                    compact={false}
                  />
                </Box>

                {/* Collapsible raw data */}
                <Box sx={{ mt: 1 }}>
                  <AuditRawJsonViewer
                    previousValue={entry.previousValue}
                    newValue={entry.newValue}
                  />
                </Box>
              </Box>
            ))}
          </Stack>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 1.5 }}>
        <Button onClick={onClose} color="inherit">
          Close
        </Button>
      </DialogActions>
    </Dialog>
  );
}
