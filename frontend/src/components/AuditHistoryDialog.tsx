import { useEffect, useState } from "react";
import {
  Alert,
  Button,
  Box,
  Typography,
  Chip,
  Stack,
  useTheme
} from "@mui/material";
import HistoryIcon from "@mui/icons-material/History";
import { formatLabDateTime } from "../utils/formatDate";
import { FloatingDialog } from "./FloatingDialog";
import { LoadingSpinner } from "./LoadingSpinner";
import { EmptyState } from "./lab/EmptyState";
import { getErrorMessage } from "../utils/errorMessage";
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
  const theme = useTheme();
  const [entries, setEntries] = useState<AuditLogItem[] | null>(null);
  const [loading, setLoading] = useState(false);
  // A failed load used to be stored as an empty list, so the dialog stated
  // "No audit log entries recorded" for a record whose trail simply could
  // not be fetched - a false statement about a GMP record.
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (open && entityId != null) {
      setLoading(true);
      setEntries(null);
      setError(null);
      AuditSearchService.getForEntity(entityName, entityId)
        .then((res) => setEntries(res))
        .catch((err) => setError(getErrorMessage(err, "The audit history could not be loaded.")))
        .finally(() => setLoading(false));
    }
  }, [open, entityName, entityId, reloadKey]);

  const friendlyEntity = ENTITY_DISPLAY_NAMES[entityName] ?? entityName;

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      titleSx={{ pb: 1.5, bgcolor: theme.custom.status.purple.bg }}
      title={
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <HistoryIcon sx={{ color: theme.palette.primary.main }} />
          <Typography variant="h6" sx={{ fontWeight: 700, color: theme.palette.primary.main }}>
            Audit History · {friendlyEntity} #{entityId}
          </Typography>
        </Box>
      }
      actions={
        <Button onClick={onClose} color="inherit">
          Close
        </Button>
      }
    >
      {loading ? (
        <LoadingSpinner label="Loading audit history…" showLabel />
      ) : error ? (
        <Alert
          severity="error"
          action={<Button color="inherit" size="small" onClick={() => setReloadKey((k) => k + 1)}>Retry</Button>}
        >
          {error}
        </Alert>
      ) : !entries || entries.length === 0 ? (
        <EmptyState
          icon={<HistoryIcon />}
          title="No audit entries recorded"
          description="No changes have been recorded for this record yet."
        />
      ) : (
        // Newest first as the server returns them. Each entry answers who,
        // what (action + field changes) and when, in that order.
        <Stack component="ol" spacing={1.5} sx={{ listStyle: "none", m: 0, p: 0 }}>
          {entries.map((entry) => (
            <Box
              component="li"
              key={entry.id}
              sx={{
                p: 2,
                bgcolor: "background.paper",
                borderRadius: 1.5,
                border: "1px solid",
                borderColor: "divider"
              }}
            >
              {/* Event header: action, who, when */}
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
                <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
                  <Chip
                    label={entry.action}
                    size="small"
                    color={ACTION_COLORS[entry.action] ?? "default"}
                    variant="outlined"
                    sx={{ height: 22 }}
                  />
                  <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                    {entry.userName}
                  </Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary", fontFamily: "monospace" }}>
                    User #{entry.userId}
                  </Typography>
                </Box>

                <Typography component="time" dateTime={entry.timestamp} sx={{ fontSize: 12.5, color: "text.secondary", fontVariantNumeric: "tabular-nums" }}>
                  {formatLabDateTime(entry.timestamp)} UTC
                </Typography>
              </Box>

              {/* Field-level diff */}
              <Box sx={{ mb: 1.5 }}>
                <AuditDiffViewer
                  action={entry.action}
                  previousValue={entry.previousValue}
                  newValue={entry.newValue}
                  entityName={entityName}
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
    </FloatingDialog>
  );
}
