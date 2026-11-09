import { Box, Chip, Tooltip, Typography, useTheme } from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorOutlinedIcon from "@mui/icons-material/ErrorOutlined";
import type { IcpCalibrationElementDto } from "../types";

export interface IcpElementStatusStripProps {
  // The elements the analyst calibrated on this run, with the server's r verdict.
  elements?: IcpCalibrationElementDto[];
}

export function IcpElementStatusStrip({ elements = [] }: IcpElementStatusStripProps) {
  const theme = useTheme();

  if (elements.length === 0) {
    return null;
  }

  return (
    <Box
      sx={{
        display: "flex",
        alignItems: "center",
        flexWrap: "wrap",
        gap: 1,
        p: 1.25,
        borderRadius: 1.5,
        backgroundColor: theme.palette.background.paper,
        border: `1px solid ${theme.palette.divider}`
      }}
    >
      <Typography variant="caption" sx={{ fontWeight: 700, color: "text.secondary", mr: 0.5, textTransform: "uppercase", letterSpacing: 0.5 }}>
        Calibrated elements:
      </Typography>

      <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
        {elements.map((el) => {
          if (el.passed) {
            return (
              <Chip
                key={el.id}
                size="small"
                icon={<CheckCircleIcon sx={{ fontSize: "16px !important" }} />}
                label={`${el.symbol}: r ${el.correlationR ?? "-"}`}
                color="success"
                variant="outlined"
                sx={{
                  fontWeight: 600,
                  fontSize: 12,
                  borderColor: theme.palette.success.light,
                  backgroundColor: theme.palette.mode === "dark" ? "rgba(46, 125, 50, 0.15)" : "#e8f5e9"
                }}
              />
            );
          }

          const failureText = el.failureReasons || "Failed calibration";
          return (
            <Tooltip key={el.id} title={failureText} arrow placement="top">
              <Chip
                size="small"
                icon={<ErrorOutlinedIcon sx={{ fontSize: "16px !important" }} />}
                label={`${el.symbol}: ${failureText}`}
                color="error"
                variant="outlined"
                sx={{
                  fontWeight: 600,
                  fontSize: 12,
                  maxWidth: { xs: 260, sm: 380 },
                  borderColor: theme.palette.error.light,
                  backgroundColor: theme.palette.mode === "dark" ? "rgba(211, 47, 47, 0.15)" : "#ffebee",
                  "& .MuiChip-label": {
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap"
                  }
                }}
              />
            </Tooltip>
          );
        })}
      </Box>
    </Box>
  );
}
