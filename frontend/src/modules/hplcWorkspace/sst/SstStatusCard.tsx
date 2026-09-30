import {
  Paper,
  Box,
  Typography,
  Alert,
  AlertTitle,
  Stack,
  useTheme
} from "@mui/material";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import ErrorOutlinedIcon from "@mui/icons-material/ErrorOutlined";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import type { HplcSstStatus } from "../types";

export interface SstStatusCardProps {
  code: string;
  status: HplcSstStatus;
  failureReasons?: string | null;
  confirmedByUserName?: string | null;
  confirmedAt?: string | null;
}

export function SstStatusCard({
  code,
  status,
  failureReasons,
  confirmedByUserName,
  confirmedAt
}: SstStatusCardProps) {
  const theme = useTheme();
  const isPassed = status === "Passed";
  const isFailed = status === "Failed";
  const isPending = status === "Pending";

  return (
    <Paper
      elevation={0}
      sx={{
        p: 2.5,
        borderRadius: 2,
        border: `1px solid ${
          isPassed
            ? theme.palette.success.main
            : isFailed
            ? theme.palette.error.main
            : theme.palette.divider
        }`,
        backgroundColor: theme.palette.background.paper
      }}
    >
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
        <Box>
          <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 700, textTransform: "uppercase" }}>
            System Suitability Record
          </Typography>
          <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16 }}>
            {code}
          </Typography>
        </Box>
        <HplcStatusBadge status={status} size="medium" />
      </Box>

      {isPassed && (
        <Alert
          severity="success"
          icon={<CheckCircleOutlinedIcon fontSize="inherit" />}
          sx={{ borderRadius: 1.5 }}
        >
          <AlertTitle sx={{ fontWeight: 700 }}>System Suitability Passed</AlertTitle>
          All acceptance criteria have been verified and confirmed. Sample assignment is unlocked for this run.
          {confirmedByUserName && (
            <Typography variant="caption" sx={{ display: "block", mt: 0.5, fontWeight: 600 }}>
              Confirmed by {confirmedByUserName}
              {confirmedAt ? ` on ${new Date(confirmedAt).toLocaleString()}` : ""}
            </Typography>
          )}
        </Alert>
      )}

      {isPending && (
        <Alert
          severity="warning"
          icon={<LockOutlinedIcon fontSize="inherit" />}
          sx={{ borderRadius: 1.5 }}
        >
          <AlertTitle sx={{ fontWeight: 700 }}>System Suitability Pending</AlertTitle>
          Sample assignment is locked until system suitability passes.
          <Typography variant="body2" sx={{ fontSize: 13, mt: 0.5 }}>
            Record standard weights and injection responses, transcribe CDS report criteria, upload the standard report, and confirm with electronic signature.
          </Typography>
        </Alert>
      )}

      {isFailed && (
        <Stack spacing={1}>
          <Alert
            severity="error"
            icon={<ErrorOutlinedIcon fontSize="inherit" />}
            sx={{ borderRadius: 1.5 }}
          >
            <AlertTitle sx={{ fontWeight: 700 }}>System Suitability Failed</AlertTitle>
            Sample assignment is locked until system suitability passes.
            {failureReasons && (
              <Box sx={{ mt: 1, p: 1, borderRadius: 1, backgroundColor: "rgba(0,0,0,0.04)" }}>
                <Typography variant="caption" sx={{ fontWeight: 700, display: "block" }}>
                  Failure Details:
                </Typography>
                <Typography variant="body2" sx={{ fontSize: 13, whiteSpace: "pre-wrap" }}>
                  {failureReasons}
                </Typography>
              </Box>
            )}
          </Alert>
          <Typography variant="caption" sx={{ color: "text.secondary", px: 0.5 }}>
            Under lab SOP, a failed SST cannot be re-opened for sample testing. Abandon this run with an explanatory reason and initiate a fresh run.
          </Typography>
        </Stack>
      )}
    </Paper>
  );
}
