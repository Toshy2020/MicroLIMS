import type { ReactNode } from "react";
import { Box, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import { useTranslation } from "../../../i18n";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import type { QualityRecordDetailDto } from "../../../services/qualityService";
import { PanelFrame } from "./PanelFrame";

interface DetailRow {
  key: string;
  label: string;
  value: ReactNode | null | undefined;
}

// The type-specific facts of a record, read-only. Entering them is done by the step forms of each type
// (the extension point in typeActions.ts); a fact the path has not reached yet reads "Not recorded yet".
export function TypeDetails({ record }: { record: QualityRecordDetailDto }) {
  const { t } = useTranslation("quality");

  const text = (value: string | null | undefined): ReactNode | null => (value && value.trim() ? value : null);
  const recordLink = (id: number | null, number: string | null): ReactNode | null =>
    id !== null && number ? (
      <RouterLink to={`/quality/${id}`} style={{ fontWeight: 600 }}>
        {number}
      </RouterLink>
    ) : null;

  let rows: DetailRow[] = [];
  if (record.deviation) {
    const d = record.deviation;
    rows = [
      { key: "occurredAt", label: t("details.deviation.occurredAt"), value: formatDateTimeWithTimeZone(d.occurredAtUtc) },
      { key: "location", label: t("details.deviation.location"), value: text(d.location) },
      { key: "immediateAction", label: t("details.deviation.immediateAction"), value: text(d.immediateAction) },
      { key: "impactAssessment", label: t("details.deviation.impactAssessment"), value: text(d.impactAssessment) },
      { key: "rootCause", label: t("details.deviation.rootCause"), value: text(d.rootCauseCategoryName) },
      { key: "rootCauseDescription", label: t("details.deviation.rootCauseDescription"), value: text(d.rootCauseDescription) },
      { key: "proposedActions", label: t("details.deviation.proposedActions"), value: text(d.proposedActions) },
      {
        key: "capaRequired",
        label: t("details.deviation.capaRequired"),
        value: d.capaRequired === null ? null : d.capaRequired ? t("details.yes") : t("details.no")
      },
      ...(d.rejectionReason ? [{ key: "rejectionReason", label: t("details.deviation.rejectionReason"), value: d.rejectionReason }] : [])
    ];
  } else if (record.capa) {
    const c = record.capa;
    rows = [
      { key: "source", label: t("details.capa.source"), value: recordLink(c.sourceRecordId, c.sourceRecordNumber) },
      { key: "effectivenessCriteria", label: t("details.capa.effectivenessCriteria"), value: text(c.effectivenessCriteria) },
      { key: "effectivenessMethod", label: t("details.capa.effectivenessMethod"), value: text(c.effectivenessMethod) },
      { key: "effectivenessPlannedDate", label: t("details.capa.effectivenessPlannedDate"), value: text(c.effectivenessPlannedDate) },
      {
        key: "effectivenessResult",
        label: t("details.capa.effectivenessResult"),
        value: c.effectivenessResult ? t(`details.effectiveness.${c.effectivenessResult}`) : null
      },
      { key: "effectivenessFindings", label: t("details.capa.effectivenessFindings"), value: text(c.effectivenessFindings) },
      ...(c.successorCapaId !== null
        ? [{ key: "successor", label: t("details.capa.successor"), value: recordLink(c.successorCapaId, c.successorCapaNumber) }]
        : [])
    ];
  } else if (record.changeControl) {
    const c = record.changeControl;
    rows = [
      { key: "reason", label: t("details.changeControl.reason"), value: text(c.reason) },
      { key: "impactSummary", label: t("details.changeControl.impactSummary"), value: text(c.impactSummary) },
      { key: "postChangeReview", label: t("details.changeControl.postChangeReview"), value: text(c.postChangeReview) },
      { key: "unchangedJustification", label: t("details.changeControl.unchangedJustification"), value: text(c.unchangedJustification) },
      ...(c.rejectionReason ? [{ key: "rejectionReason", label: t("details.changeControl.rejectionReason"), value: c.rejectionReason }] : [])
    ];
  } else if (record.complaint) {
    const c = record.complaint;
    rows = [
      { key: "source", label: t("details.complaint.source"), value: text(c.source) },
      { key: "receivedOn", label: t("details.complaint.receivedOn"), value: text(c.receivedOn) },
      { key: "externalReference", label: t("details.complaint.externalReference"), value: text(c.externalReference) },
      { key: "item", label: t("details.complaint.item"), value: text(c.itemName) },
      { key: "lot", label: t("details.complaint.lot"), value: text(c.lotText) },
      { key: "complaintText", label: t("details.complaint.complaintText"), value: text(c.complaintText) },
      { key: "whatToTest", label: t("details.complaint.whatToTest"), value: text(c.whatToTest) },
      { key: "findings", label: t("details.complaint.findings"), value: text(c.findings) },
      { key: "outcome", label: t("details.complaint.outcome"), value: c.outcome ? t(`details.outcome.${c.outcome}`) : null },
      { key: "conclusionSummary", label: t("details.complaint.conclusionSummary"), value: text(c.conclusionSummary) }
    ];
  }

  if (rows.length === 0) return null;

  return (
    <PanelFrame title={t("details.title")}>
      <Box component="dl" sx={{ m: 0, display: "grid", gridTemplateColumns: { xs: "1fr", md: "minmax(180px, 1fr) 3fr" }, gap: 1.25, columnGap: 3 }}>
        {rows.map((row) => (
          <Box key={row.key} sx={{ display: "contents" }}>
            <Typography component="dt" variant="body2" color="text.secondary">
              {row.label}
            </Typography>
            <Typography
              component="dd"
              variant="body2"
              sx={{ m: 0, whiteSpace: "pre-wrap", wordBreak: "break-word", fontVariantNumeric: "tabular-nums" }}
              color={row.value ? "text.primary" : "text.secondary"}
            >
              {row.value ?? t("details.notRecorded")}
            </Typography>
          </Box>
        ))}
      </Box>
    </PanelFrame>
  );
}
