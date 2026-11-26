import { Stack, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import type { TestOrderSop } from "../types/sopCaptureTypes";

interface MethodDocument {
  documentCode: string;
  title: string;
  revisionNumber: string;
  effectiveDateUtc: string | null;
}

export function SopMethodLine({ document, timeZone = "UTC" }: { document: MethodDocument; timeZone?: string }) {
  const { t } = useTranslation("testingWorkspace");
  const { documentCode, title, revisionNumber, effectiveDateUtc } = document;
  const method = t("sopCapture.method", { code: documentCode, revision: revisionNumber });
  return (
    <Typography component="p" variant="body2">
      <bdi>{method}</bdi>
      {title && <> · <bdi>{title}</bdi></>}
      {effectiveDateUtc && <> · {t("sopCapture.effective", { date: formatDateTimeWithTimeZone(effectiveDateUtc, { timeZone }) })}</>}
    </Typography>
  );
}

interface SopRevisionNoticeProps extends Pick<TestOrderSop, "newerSopRevision"> {
  timeZone?: string;
  // When one notice stands for several tests (the certificate), the tests it applies to.
  testNames?: string[];
}

export function SopRevisionNotice({ newerSopRevision, timeZone = "UTC", testNames }: SopRevisionNoticeProps) {
  const { t } = useTranslation("testingWorkspace");
  if (!newerSopRevision) return null;
  const values = {
    revision: newerSopRevision.revisionNumber,
    date: formatDateTimeWithTimeZone(newerSopRevision.effectiveDateUtc, { timeZone })
  };
  return (
    <Typography component="p" variant="body2" role="status">
      {testNames && testNames.length > 0
        ? t("sopCapture.newerForTests", { ...values, tests: testNames.join(", ") })
        : t("sopCapture.newer", values)}
    </Typography>
  );
}

export function SopMethodDetails({ sopCapture, newerSopRevision, timeZone }: TestOrderSop & { timeZone?: string }) {
  const { t } = useTranslation("testingWorkspace");
  if (!sopCapture) return <Typography variant="body2">{t("sopCapture.notRecorded")}</Typography>;
  if (sopCapture.status !== "Captured") {
    return <Typography variant="body2">{t("sopCapture.unresolved", { reason: t(`sopCapture.reasons.${sopCapture.status}`) })}</Typography>;
  }
  return (
    <Stack spacing={0.5}>
      <SopMethodLine timeZone={timeZone} document={{
        documentCode: sopCapture.documentCode ?? "",
        title: sopCapture.title ?? "",
        revisionNumber: sopCapture.revisionNumber ?? "",
        effectiveDateUtc: sopCapture.effectiveDateUtc
      }} />
      <SopRevisionNotice newerSopRevision={newerSopRevision} timeZone={timeZone} />
    </Stack>
  );
}
