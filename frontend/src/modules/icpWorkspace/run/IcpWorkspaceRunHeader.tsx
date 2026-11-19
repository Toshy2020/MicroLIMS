import {
  Box,
  Typography,
  Button,
  Stack
} from "@mui/material";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import { IcpStatusBadge } from "../components/IcpStatusBadge";
import { IcpElementStatusStrip } from "../components/IcpElementStatusStrip";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import type { IcpRunDto } from "../types";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

export interface IcpWorkspaceRunHeaderProps {
  run: IcpRunDto;
  canOperate: boolean;
  completing: boolean;
  onAbandon: () => void;
  onComplete: () => void;
}

export function IcpWorkspaceRunHeader({
  run,
  canOperate,
  completing,
  onAbandon,
  onComplete
}: IcpWorkspaceRunHeaderProps) {
  const { t } = useTranslation("icpWorkspace");
  return (
    <Box sx={{ mb: 2 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 2, mb: 1.5 }}>
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 0.5, flexWrap: "wrap" }}>
            <Typography variant="h5" sx={{ fontWeight: 700 }}>
              {run.equipmentName}{" "}
              <Box component="span" sx={{ fontFamily: monospaceFontFamily, fontSize: "0.75em", color: "text.secondary" }}>
                <Ltr>{run.equipmentCode}</Ltr>
              </Box>
            </Typography>
            <IcpStatusBadge status={run.status} size="medium" />
            <IcpStatusBadge status={run.calibration?.status ?? "Pending"} label={t("run.header.calibrationBadge", { status: run.calibration?.status ?? "Pending" })} size="medium" />
          </Box>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            <strong>{t("run.header.runCode")}</strong> <Box component="span" sx={{ fontFamily: monospaceFontFamily }}><Ltr>{run.code}</Ltr></Box> ·{" "}
            <strong>{t("run.header.analyst")}</strong> {run.analystUserName ?? t("run.header.analystFallback")} ·{" "}
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
              sx={{ textTransform: "none" }}
            >
              {t("run.header.abandonRun")}
            </Button>
            <Button
              variant="contained"
              color="primary"
              size="small"
              startIcon={<CheckCircleOutlinedIcon />}
              onClick={onComplete}
              disabled={completing}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {completing ? t("run.header.completing") : t("run.header.completeRun")}
            </Button>
          </Stack>
        )}
      </Box>

      {/* Element Status Strip */}
      <IcpElementStatusStrip elements={run.calibration?.elements} />
    </Box>
  );
}
