import { useState } from "react";
import {
  Box,
  Typography,
  Button,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Snackbar,
  Alert,
  useTheme
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";

interface Props {
  previousValue: string | null;
  newValue: string | null;
}

function formatJsonString(raw: string | null): string {
  if (!raw) return "null";
  try {
    const parsed = JSON.parse(raw);
    return JSON.stringify(parsed, null, 2);
  } catch {
    return raw;
  }
}

export function AuditRawJsonViewer({ previousValue, newValue }: Props) {
  const theme = useTheme();
  const [copied, setCopied] = useState(false);

  const prevFormatted = formatJsonString(previousValue);
  const newFormatted = formatJsonString(newValue);

  const copyBoth = () => {
    const payload = JSON.stringify(
      {
        previousValue: previousValue ? JSON.parse(previousValue) : null,
        newValue: newValue ? JSON.parse(newValue) : null
      },
      null,
      2
    );
    navigator.clipboard.writeText(payload);
    setCopied(true);
  };

  return (
    <Accordion defaultExpanded={false} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1.5, "&:before": { display: "none" } }}>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", width: "100%", pr: 1 }}>
          <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary" }}>
            Raw Stored Audit Values
          </Typography>
        </Box>
      </AccordionSummary>

      <AccordionDetails sx={{ pt: 0 }}>
        <Box sx={{ display: "flex", justifyContent: "flex-end", mb: 1 }}>
          <Button
            size="small"
            variant="outlined"
            startIcon={<ContentCopyIcon sx={{ fontSize: 12 }} />}
            onClick={copyBoth}
            sx={{ fontSize: 11, textTransform: "none" }}
          >
            Copy Raw JSON
          </Button>
        </Box>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 1.5 }}>
          {/* Previous Value */}
          <Box>
            <Typography sx={{ fontSize: 11, fontWeight: 700, color: theme.custom.status.detected.text, mb: 0.5 }}>
              Previous Value
            </Typography>
            <Box
              component="pre"
              sx={{
                p: 1.5,
                bgcolor: theme.custom.status.detected.bg,
                border: "1px solid",
                borderColor: theme.custom.status.detected.border,
                borderRadius: 1,
                fontSize: 11,
                fontFamily: "monospace",
                overflowX: "auto",
                maxHeight: 250,
                color: theme.custom.status.detected.text
              }}
            >
              {prevFormatted}
            </Box>
          </Box>

          {/* New Value */}
          <Box>
            <Typography sx={{ fontSize: 11, fontWeight: 700, color: theme.custom.status.notDetected.text, mb: 0.5 }}>
              Current Value
            </Typography>
            <Box
              component="pre"
              sx={{
                p: 1.5,
                bgcolor: theme.custom.status.notDetected.bg,
                border: "1px solid",
                borderColor: theme.custom.status.notDetected.border,
                borderRadius: 1,
                fontSize: 11,
                fontFamily: "monospace",
                overflowX: "auto",
                maxHeight: 250,
                color: theme.custom.status.notDetected.text
              }}
            >
              {newFormatted}
            </Box>
          </Box>
        </Box>

        <Snackbar
          open={copied}
          autoHideDuration={2000}
          onClose={() => setCopied(false)}
          message="Raw JSON copied to clipboard"
        />
      </AccordionDetails>
    </Accordion>
  );
}
