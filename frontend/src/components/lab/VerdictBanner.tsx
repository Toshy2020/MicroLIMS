import { Box, Typography, useTheme } from "@mui/material";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import HourglassEmptyIcon from "@mui/icons-material/HourglassEmpty";
import HelpOutlineIcon from "@mui/icons-material/HelpOutlined";
import type { ReactNode } from "react";
import type { StatusTone } from "../../theme/statusTokens";

export type Verdict = "Pass" | "Fail" | "Pending" | "Inconclusive";

const VERDICT_STYLE: Record<Verdict, { tone: StatusTone; icon: ReactNode }> = {
  Pass: { tone: "notDetected", icon: <CheckCircleOutlineIcon /> },
  Fail: { tone: "detected", icon: <CancelOutlinedIcon /> },
  Pending: { tone: "pending", icon: <HourglassEmptyIcon /> },
  Inconclusive: { tone: "inconclusive", icon: <HelpOutlineIcon /> }
};

// Display only - the server stays the calculator of record for the verdict.
export function VerdictBanner({ verdict, detail }: { verdict: Verdict; detail?: string }) {
  const theme = useTheme();
  const { tone, icon } = VERDICT_STYLE[verdict];
  const t = theme.custom.status[tone];
  return (
    <Box
      role="status"
      sx={{
        display: "flex", alignItems: "center", gap: 1.5, px: 2, py: 1.25, borderRadius: 1.5,
        bgcolor: t.bg, color: t.text, border: `1px solid ${t.border}`
      }}
    >
      <Box sx={{ display: "flex" }}>{icon}</Box>
      <Box>
        <Typography sx={{ fontWeight: 700, color: "inherit" }}>{verdict}</Typography>
        {detail && <Typography variant="body2" sx={{ color: "inherit" }}>{detail}</Typography>}
      </Box>
    </Box>
  );
}
