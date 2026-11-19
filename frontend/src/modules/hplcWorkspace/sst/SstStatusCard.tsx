import { Box, Stack, Typography } from "@mui/material";
import { VerdictBanner } from "../../../components/lab";
import type { Verdict } from "../../../components/lab";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import type { HplcSstStatus } from "../types";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

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
  const { t } = useTranslation("hplcWorkspace");
  const isPassed = status === "Passed";
  const isFailed = status === "Failed";

  const detail = isPassed
    ? `${t("sst.statusCard.detailPassed")}${
        confirmedByUserName
          ? t("sst.statusCard.detailConfirmedBy", {
              user: confirmedByUserName,
              at: confirmedAt ? t("sst.statusCard.detailConfirmedAt", { date: formatDateTimeWithTimeZone(confirmedAt) }) : ""
            })
          : ""
      }`
    : isFailed
    ? t("sst.statusCard.detailFailed")
    : t("sst.statusCard.detailPending");

  return (
    <Stack spacing={1.5}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Box>
          <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 700, textTransform: "uppercase" }}>
            {t("sst.statusCard.title")}
          </Typography>
          <Typography sx={{ fontWeight: 700, fontSize: 16, fontFamily: monospaceFontFamily }}>
            <Ltr>{code}</Ltr>
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
                {t("sst.statusCard.failureDetails")}
              </Typography>
              <Typography variant="body2" sx={{ fontSize: 13, whiteSpace: "pre-wrap" }}>
                {failureReasons}
              </Typography>
            </Box>
          )}
          <Typography variant="caption" sx={{ color: "text.secondary", px: 0.5 }}>
            {t("sst.statusCard.sopNote")}
          </Typography>
        </>
      )}
    </Stack>
  );
}
