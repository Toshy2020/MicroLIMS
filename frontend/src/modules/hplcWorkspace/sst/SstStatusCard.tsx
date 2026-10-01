import { Box, Stack, Typography } from "@mui/material";
import { VerdictBanner } from "../../../components/lab";
import type { Verdict } from "../../../components/lab";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { monospaceFontFamily } from "../../../theme/palette";
import type { HplcSstStatus } from "../types";

export interface SstStatusCardProps {
  code: string;
  status: HplcSstStatus;
  failureReasons?: string | null;
  confirmedByUserName?: string | null;
  confirmedAt?: string | null;
}

// The verdict is the server's SST status; nothing is recalculated here.
const VERDICT: Record<HplcSstStatus, Verdict> = {
  Passed: "Pass",
  Failed: "Fail",
  Pending: "Pending"
};

export function SstStatusCard({
  code,
  status,
  failureReasons,
  confirmedByUserName,
  confirmedAt
}: SstStatusCardProps) {
  const isPassed = status === "Passed";
  const isFailed = status === "Failed";

  const detail = isPassed
    ? `All acceptance criteria have been verified and confirmed. Sample assignment is unlocked for this run.${
        confirmedByUserName
          ? ` Confirmed by ${confirmedByUserName}${confirmedAt ? ` on ${new Date(confirmedAt).toLocaleString()}` : ""}.`
          : ""
      }`
    : isFailed
    ? "Sample assignment is locked until system suitability passes."
    : "Sample assignment is locked until system suitability passes. Record standard weights and injection responses, transcribe CDS report criteria, upload the standard report, and confirm with electronic signature.";

  return (
    <Stack spacing={1.5}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Box>
          <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 700, textTransform: "uppercase" }}>
            System Suitability Record
          </Typography>
          <Typography sx={{ fontWeight: 700, fontSize: 16, fontFamily: monospaceFontFamily }}>
            {code}
          </Typography>
        </Box>
        <HplcStatusBadge status={status} size="medium" />
      </Box>

      <VerdictBanner verdict={VERDICT[status]} detail={detail} />

      {isFailed && (
        <>
          {failureReasons && (
            <Box sx={{ p: 1, borderRadius: 1, bgcolor: "action.hover" }}>
              <Typography variant="caption" sx={{ fontWeight: 700, display: "block" }}>
                Failure Details:
              </Typography>
              <Typography variant="body2" sx={{ fontSize: 13, whiteSpace: "pre-wrap" }}>
                {failureReasons}
              </Typography>
            </Box>
          )}
          <Typography variant="caption" sx={{ color: "text.secondary", px: 0.5 }}>
            Under lab SOP, a failed SST cannot be re-opened for sample testing. Abandon this run with an explanatory reason and initiate a fresh run.
          </Typography>
        </>
      )}
    </Stack>
  );
}
