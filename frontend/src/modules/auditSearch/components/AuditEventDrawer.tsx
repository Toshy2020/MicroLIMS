import {
  Drawer,
  Box,
  Typography,
  IconButton,
  Chip,
  Divider,
  Button,
  Stack
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import PersonIcon from "@mui/icons-material/Person";
import HistoryIcon from "@mui/icons-material/History";
import EventNoteIcon from "@mui/icons-material/EventNote";
import TimelineIcon from "@mui/icons-material/Timeline";
import CodeIcon from "@mui/icons-material/Code";
import { formatLabDateTime } from "../../../utils/formatDate";
import { brandColors } from "../../../theme";
import type { AuditLogItem } from "../types/auditTypes";
import { ENTITY_DISPLAY_NAMES } from "../types/auditTypes";
import { AuditDiffViewer } from "./AuditDiffViewer";
import { AuditTraceabilityChain } from "./AuditTraceabilityChain";
import { AuditRawJsonViewer } from "./AuditRawJsonViewer";

interface Props {
  open: boolean;
  event: AuditLogItem | null;
  onClose: () => void;
  onViewRecordHistory: (entityName: string, entityId: string | number) => void;
}

const ACTION_COLORS: Record<string, "success" | "warning" | "error" | "default"> = {
  Create: "success",
  Update: "warning",
  Delete: "error"
};

export function AuditEventDrawer({
  open,
  event,
  onClose,
  onViewRecordHistory
}: Props) {
  if (!event) return null;

  const friendlyEntity = ENTITY_DISPLAY_NAMES[event.entityName] ?? event.entityName;

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      PaperProps={{
        sx: {
          width: { xs: "100%", sm: 540, md: 580 },
          p: 0,
          boxShadow: "-4px 0 20px rgba(0,0,0,0.08)"
        }
      }}
    >
      {/* Header */}
      <Box
        sx={{
          p: 2.5,
          bgcolor: "#faf5ff",
          borderBottom: "1px solid #f3e8ff",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start"
        }}
      >
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.5, flexWrap: "wrap" }}>
            <Typography variant="h6" sx={{ fontWeight: 700, color: brandColors.sectionTitle }}>
              Audit Event #{event.id}
            </Typography>
            <Chip
              label={event.action.toUpperCase()}
              size="small"
              color={ACTION_COLORS[event.action] ?? "default"}
              sx={{ fontWeight: 700, fontSize: 11 }}
            />
          </Box>
          <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
            {friendlyEntity} · ID: <span style={{ fontFamily: "monospace" }}>{event.entityId}</span>
          </Typography>
          <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.25 }}>
            Timestamp: {formatLabDateTime(event.timestamp)} UTC
          </Typography>
        </Box>

        <IconButton size="small" onClick={onClose} sx={{ color: "text.secondary" }}>
          <CloseIcon fontSize="small" />
        </IconButton>
      </Box>

      {/* Drawer Body */}
      <Box sx={{ p: 2.5, overflowY: "auto", display: "flex", flexDirection: "column", gap: 3 }}>
        {/* SECTION 1 — User Identity */}
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
            <PersonIcon sx={{ fontSize: 18, color: "primary.main" }} />
            <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary" }}>
              User Identity
            </Typography>
          </Box>
          <Box
            sx={{
              p: 2,
              bgcolor: "#f9fafb",
              borderRadius: 1.5,
              border: "1px solid #e5e7eb"
            }}
          >
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: "text.primary" }}>
              {event.userName}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "primary.main", fontWeight: 600 }}>
              {event.userRole ?? "User"}
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.5, fontFamily: "monospace" }}>
              User ID: #{event.userId} {event.userUsername ? `(${event.userUsername})` : ""}
            </Typography>
          </Box>
        </Box>

        {/* SECTION 2 — Changed Fields (Diff) */}
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
            <EventNoteIcon sx={{ fontSize: 18, color: "primary.main" }} />
            <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary" }}>
              Changed Fields (Inline Diff)
            </Typography>
          </Box>
          <AuditDiffViewer
            action={event.action}
            previousValue={event.previousValue}
            newValue={event.newValue}
            compact={false}
          />
        </Box>

        {/* SECTION 3 — Record Traceability */}
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
            <TimelineIcon sx={{ fontSize: 18, color: "primary.main" }} />
            <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary" }}>
              Record Traceability
            </Typography>
          </Box>
          <AuditTraceabilityChain
            auditLogId={event.id}
            entityName={event.entityName}
            entityId={event.entityId}
            onOpenEntityHistory={onViewRecordHistory}
          />
        </Box>

        {/* SECTION 4 — Raw Stored Audit Values */}
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
            <CodeIcon sx={{ fontSize: 18, color: "primary.main" }} />
            <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary" }}>
              Raw Storage
            </Typography>
          </Box>
          <AuditRawJsonViewer
            previousValue={event.previousValue}
            newValue={event.newValue}
          />
        </Box>

        {/* Action button */}
        <Box sx={{ pt: 1, pb: 2 }}>
          <Button
            fullWidth
            variant="outlined"
            startIcon={<HistoryIcon />}
            onClick={() => onViewRecordHistory(event.entityName, event.entityId)}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            View Complete History for {event.entityName} #{event.entityId}
          </Button>
        </Box>
      </Box>
    </Drawer>
  );
}
