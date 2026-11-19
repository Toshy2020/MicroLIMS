import { Box, Chip, Tooltip, Typography, useTheme } from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorOutlinedIcon from "@mui/icons-material/ErrorOutlined";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import type { IcpCalibrationElementDto } from "../types";

export interface IcpElementStatusStripProps {
  // The elements the analyst calibrated on this run, with the server's r verdict.
  elements?: IcpCalibrationElementDto[];
}

export function IcpElementStatusStrip({ elements = [] }: IcpElementStatusStripProps) {
  const { t } = useTranslation("icpWorkspace");
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
        {t("elementStrip.title")}
      </Typography>

      <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
        {elements.map((el) => {
          if (el.passed) {
            return (
              <Chip
                key={el.id}
                size="small"
                icon={<CheckCircleIcon sx={{ fontSize: "16px !important" }} />}
                label={<Ltr>{`${el.symbol}: r ${el.correlationR ?? "-"}`}</Ltr>}
                color="success"
                variant="outlined"
                sx={{
                  fontWeight: 600,
                  fontSize: 12,
                  borderColor: theme.custom.status.notDetected.border,
                  backgroundColor: theme.custom.status.notDetected.bg
                }}
              />
            );
          }

          const failureText = el.failureReasons || t("elementStrip.failedFallback");
          return (
            <Tooltip key={el.id} title={failureText} arrow placement="top">
              <Chip
                size="small"
                icon={<ErrorOutlinedIcon sx={{ fontSize: "16px !important" }} />}
                label={<><Ltr>{el.symbol}</Ltr>: {failureText}</>}
                color="error"
                variant="outlined"
                sx={{
                  fontWeight: 600,
                  fontSize: 12,
                  maxWidth: { xs: 260, sm: 380 },
                  borderColor: theme.custom.status.detected.border,
                  backgroundColor: theme.custom.status.detected.bg,
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
