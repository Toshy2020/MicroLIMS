import {
  Box,
  Typography,
  Button,
  Stack
} from "@mui/material";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import type { HplcRunDto } from "../types";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

export interface WorkspaceRunHeaderProps {
  run: HplcRunDto;
  canOperate: boolean;
  completing: boolean;
  onAbandon: () => void;
  onComplete: () => void;
}

export function WorkspaceRunHeader({
  run,
  canOperate,
  completing,
  onAbandon,
  onComplete
}: WorkspaceRunHeaderProps) {
  const { t } = useTranslation("hplcWorkspace");
  const allSamplesFinished =
    run.samples.length > 0 &&
    run.samples.every((s) => s.submitted || s.status === "Removed");

  return (
    <Box sx={{ mb: 2 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 2 }}>
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 0.5 }}>
            <Typography variant="h5" sx={{ fontWeight: 700 }}>
              {run.equipmentName}{" "}
              <Box component="span" sx={{ fontFamily: monospaceFontFamily, fontSize: "0.75em", color: "text.secondary" }}>
                <Ltr>{run.equipmentCode}</Ltr>
              </Box>
            </Typography>
            <HplcStatusBadge status={run.status} size="medium" />
          </Box>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            <strong>{t("run.header.runCode")}</strong> <Box component="span" sx={{ fontFamily: monospaceFontFamily }}><Ltr>{run.code}</Ltr></Box> · <strong>{t("run.header.method")}</strong> <Ltr>{run.hplcMethodAbbreviation}</Ltr> ·{" "}
            <strong>{t("run.header.column")}</strong> <Box component="span" sx={{ fontFamily: monospaceFontFamily }}><Ltr>{run.chromatographyColumnCode}</Ltr></Box> · <strong>{t("run.header.analyst")}</strong> {run.analystUserName ?? t("run.header.analystFallback")} ·{" "}
            <strong>{t("run.header.started")}</strong> <Ltr>{formatDateTimeWithTimeZone(run.startedAt)}</Ltr>
          </Typography>
        </Box>

        {run.status === "Open" && canOperate && (
          <Stack direction="row" spacing={1.5}>
            <Button
              variant="outlined"
              color="error"
              size="small"
              startIcon={<CancelOutlinedIcon />}
              onClick={onAbandon}
            >
              {t("run.header.abandonRun")}
            </Button>
            <Button
              variant="contained"
              color="primary"
              size="small"
              startIcon={<CheckCircleOutlinedIcon />}
              onClick={onComplete}
              disabled={completing || !allSamplesFinished}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {t("run.header.completeRun")}
            </Button>
          </Stack>
        )}
      </Box>
    </Box>
  );
}
