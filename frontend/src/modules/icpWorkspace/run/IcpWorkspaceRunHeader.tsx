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
import { monospaceFontFamily } from "../../../theme/palette";
import type { IcpRunDto } from "../types";

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
  return (
    <Box sx={{ mb: 2 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 2, mb: 1.5 }}>
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 0.5, flexWrap: "wrap" }}>
            <Typography variant="h5" sx={{ fontWeight: 700 }}>
              {run.equipmentName}{" "}
              <Box component="span" sx={{ fontFamily: monospaceFontFamily, fontSize: "0.75em", color: "text.secondary" }}>
                {run.equipmentCode}
              </Box>
            </Typography>
            <IcpStatusBadge status={run.status} size="medium" />
            <IcpStatusBadge status={run.calibration?.status ?? "Pending"} label={`Calibration: ${run.calibration?.status ?? "Pending"}`} size="medium" />
          </Box>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            <strong>Run Code:</strong> <Box component="span" sx={{ fontFamily: monospaceFontFamily }}>{run.code}</Box> ·{" "}
            <strong>Method:</strong> {run.icpMethodAbbreviation} ({run.method.mode === "MineralAssay" ? "Mineral Assay" : "Elemental Impurities"}) ·{" "}
            <strong>Analyst:</strong> {run.analystUserName ?? "Analyst"} ·{" "}
            <strong>Started:</strong> {new Date(run.startedAt).toLocaleString()}
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
              Abandon Run...
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
              {completing ? "Completing..." : "Complete Run"}
            </Button>
          </Stack>
        )}
      </Box>

      {/* Element Status Strip */}
      <IcpElementStatusStrip elementStates={run.elementStates} />
    </Box>
  );
}
