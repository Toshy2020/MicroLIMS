import { Box, Typography, Chip, Tooltip } from "@mui/material";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import { computeAuditDiff } from "../utils/auditDiffUtils";

interface Props {
  action: string;
  previousValue: string | null;
  newValue: string | null;
  compact?: boolean;
  maxCompactItems?: number;
  onViewAll?: () => void;
}

export function AuditDiffViewer({
  action,
  previousValue,
  newValue,
  compact = false,
  maxCompactItems = 2,
  onViewAll
}: Props) {
  const changes = computeAuditDiff(action, previousValue, newValue);

  if (action === "Create") {
    if (compact) {
      const displayProps = changes.slice(0, 2).map((c) => `${c.label}: ${c.newDisplay}`).join(" · ");
      return (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 0.25 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
            <Chip
              label="CREATED"
              size="small"
              sx={{
                fontSize: 10,
                fontWeight: 700,
                height: 18,
                bgcolor: "#ecfdf5",
                color: "#059669",
                border: "1px solid #a7f3d0"
              }}
            />
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              Initial record created
            </Typography>
          </Box>
          {displayProps && (
            <Typography sx={{ fontSize: 11, color: "text.primary", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", maxWidth: 300 }}>
              {displayProps}
            </Typography>
          )}
        </Box>
      );
    }

    return (
      <Box sx={{ display: "flex", flexDirection: "column", gap: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.5 }}>
          <Chip
            label="NEW RECORD CREATED"
            size="small"
            sx={{
              fontSize: 11,
              fontWeight: 700,
              bgcolor: "#ecfdf5",
              color: "#059669",
              border: "1px solid #a7f3d0"
            }}
          />
        </Box>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 1 }}>
          {changes.map((c) => (
            <Box key={c.key} sx={{ p: 1, bgcolor: "#f9fafb", borderRadius: 1, border: "1px solid #f3f4f6" }}>
              <Typography sx={{ fontSize: 11, color: "text.secondary", fontWeight: 600 }}>
                {c.label}
              </Typography>
              <Typography sx={{ fontSize: 12, fontWeight: 500, color: "text.primary" }}>
                {c.newDisplay}
              </Typography>
            </Box>
          ))}
        </Box>
      </Box>
    );
  }

  if (action === "Delete") {
    return (
      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
        <Chip
          label="DELETED"
          size="small"
          sx={{
            fontSize: 10,
            fontWeight: 700,
            height: 18,
            bgcolor: "#fef2f2",
            color: "#dc2626",
            border: "1px solid #fecaca"
          }}
        />
        <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
          Record permanently marked deleted
        </Typography>
      </Box>
    );
  }

  // UPDATE action
  if (changes.length === 0) {
    return (
      <Typography sx={{ fontSize: 11, color: "text.secondary", fontStyle: "italic" }}>
        No field value changes detected
      </Typography>
    );
  }

  if (compact) {
    const visibleChanges = changes.slice(0, maxCompactItems);
    const extraCount = changes.length - maxCompactItems;

    return (
      <Box sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}>
        {visibleChanges.map((c) => (
          <Box key={c.key} sx={{ display: "flex", alignItems: "center", gap: 0.75, fontSize: 11 }}>
            <Typography component="span" sx={{ fontSize: 11, fontWeight: 600, color: "text.secondary", minWidth: 80, flexShrink: 0 }}>
              {c.label}
            </Typography>
            <Box sx={{ display: "inline-flex", alignItems: "center", gap: 0.5, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
              <Typography
                component="span"
                sx={{
                  fontSize: 11,
                  color: "#991b1b",
                  bgcolor: "#fee2e2",
                  px: 0.5,
                  py: 0.1,
                  borderRadius: 0.5,
                  maxWidth: 120,
                  overflow: "hidden",
                  textOverflow: "ellipsis",
                  whiteSpace: "nowrap"
                }}
                title={c.oldDisplay}
              >
                {c.oldDisplay}
              </Typography>
              <ArrowForwardIcon sx={{ fontSize: 10, color: "text.secondary", flexShrink: 0 }} />
              <Typography
                component="span"
                sx={{
                  fontSize: 11,
                  color: "#065f46",
                  bgcolor: "#d1fae5",
                  px: 0.5,
                  py: 0.1,
                  borderRadius: 0.5,
                  fontWeight: 600,
                  maxWidth: 140,
                  overflow: "hidden",
                  textOverflow: "ellipsis",
                  whiteSpace: "nowrap"
                }}
                title={c.newDisplay}
              >
                {c.newDisplay}
              </Typography>
            </Box>
          </Box>
        ))}

        {extraCount > 0 && (
          <Box sx={{ mt: 0.25 }}>
            <Tooltip title="Click row to inspect all changed fields">
              <Typography
                component="span"
                onClick={(e) => {
                  e.stopPropagation();
                  onViewAll?.();
                }}
                sx={{
                  fontSize: 10,
                  fontWeight: 600,
                  color: "primary.main",
                  cursor: "pointer",
                  "&:hover": { textDecoration: "underline" }
                }}
              >
                +{extraCount} more changed {extraCount === 1 ? "field" : "fields"}
              </Typography>
            </Tooltip>
          </Box>
        )}
      </Box>
    );
  }

  // Full detailed diff table
  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 1 }}>
      {changes.map((c) => (
        <Box
          key={c.key}
          sx={{
            p: 1.5,
            border: "1px solid #e5e7eb",
            borderRadius: 1.5,
            bgcolor: "#ffffff"
          }}
        >
          <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", textTransform: "uppercase", mb: 0.5 }}>
            {c.label} <Typography component="span" sx={{ fontSize: 10, color: "text.disabled", fontFamily: "monospace" }}>({c.key})</Typography>
          </Typography>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap" }}>
            <Box sx={{ flex: 1, minWidth: 120, p: 1, bgcolor: "#fef2f2", border: "1px solid #fecaca", borderRadius: 1 }}>
              <Typography sx={{ fontSize: 10, color: "#991b1b", fontWeight: 700 }}>PREVIOUS VALUE</Typography>
              <Typography sx={{ fontSize: 12, color: "#7f1d1d", wordBreak: "break-word" }}>
                {c.oldDisplay}
              </Typography>
            </Box>
            <ArrowForwardIcon sx={{ color: "text.secondary", fontSize: 16 }} />
            <Box sx={{ flex: 1, minWidth: 120, p: 1, bgcolor: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: 1 }}>
              <Typography sx={{ fontSize: 10, color: "#166534", fontWeight: 700 }}>NEW VALUE</Typography>
              <Typography sx={{ fontSize: 12, color: "#14532d", fontWeight: 600, wordBreak: "break-word" }}>
                {c.newDisplay}
              </Typography>
            </Box>
          </Box>
        </Box>
      ))}
    </Box>
  );
}
