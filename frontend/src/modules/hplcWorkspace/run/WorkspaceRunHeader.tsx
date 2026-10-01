import {
  Box,
  Typography,
  Button,
  Stack
} from "@mui/material";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { monospaceFontFamily } from "../../../theme/palette";
import type { HplcRunDto } from "../types";

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
              <Box component="span" sx={{ fontFamily: monospaceFontFamily, fontSize: "0.7em", color: "text.secondary" }}>
                {run.equipmentCode}
              </Box>
            </Typography>
            <HplcStatusBadge status={run.status} size="medium" />
          </Box>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            <strong>Run Code:</strong> <Box component="span" sx={{ fontFamily: monospaceFontFamily }}>{run.code}</Box> · <strong>Method:</strong> {run.hplcMethodAbbreviation} ·{" "}
            <strong>Column:</strong> <Box component="span" sx={{ fontFamily: monospaceFontFamily }}>{run.chromatographyColumnCode}</Box> · <strong>Analyst:</strong> {run.analystUserName ?? "Analyst"} ·{" "}
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
            >
              Abandon Run...
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
              Complete Run
            </Button>
          </Stack>
        )}
      </Box>
    </Box>
  );
}
