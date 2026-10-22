import { STATUS_LABEL, STATUS_SYMBOL, STATUS_TOKEN } from "../statusDisplay";
import type { OccurrenceStatus } from "../types";
import { StatusPill } from "./StatusPill";

export function OccurrenceStatusChip({ status, late }: { status: OccurrenceStatus; late?: boolean }) {
  return (
    <StatusPill tone={STATUS_TOKEN[status]} symbol={STATUS_SYMBOL[status]}>
      {late ? `${STATUS_LABEL[status]} (late)` : STATUS_LABEL[status]}
    </StatusPill>
  );
}
