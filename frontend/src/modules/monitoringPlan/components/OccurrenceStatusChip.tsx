import { Chip } from "@mui/material";
import { STATUS_LABEL, STATUS_TONE } from "../statusDisplay";
import type { OccurrenceStatus } from "../types";

export function OccurrenceStatusChip({ status, late }: { status: OccurrenceStatus; late?: boolean }) {
  const tone = STATUS_TONE[status];
  return (
    <Chip
      size="small"
      label={late ? `${STATUS_LABEL[status]} (late)` : STATUS_LABEL[status]}
      color={tone === "default" ? undefined : tone}
      variant={tone === "default" ? "outlined" : "filled"}
    />
  );
}
