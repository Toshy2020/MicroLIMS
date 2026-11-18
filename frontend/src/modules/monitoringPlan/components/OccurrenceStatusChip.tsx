import { useTranslation } from "../../../i18n";
import { STATUS_KEY, STATUS_SYMBOL, STATUS_TOKEN } from "../statusDisplay";
import type { OccurrenceStatus } from "../types";
import { StatusPill } from "./StatusPill";

export function OccurrenceStatusChip({ status, late }: { status: OccurrenceStatus; late?: boolean }) {
  const { t } = useTranslation("monitoring");
  const label = t(STATUS_KEY[status]);
  return (
    <StatusPill tone={STATUS_TOKEN[status]} symbol={STATUS_SYMBOL[status]}>
      {late ? t("occurrence.statusLate", { status: label }) : label}
    </StatusPill>
  );
}
