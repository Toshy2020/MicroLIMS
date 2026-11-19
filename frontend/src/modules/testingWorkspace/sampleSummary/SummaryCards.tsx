import type { ReactNode } from "react";
import { Box, Typography, Divider, Table, TableHead, TableRow, TableCell, TableBody, Paper, useTheme, TableContainer } from "@mui/material";
import { Trans } from "react-i18next";
import { withLot } from "../../testPreparation/preparationMaterial";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { brandColors, tableHeadSx } from "../../../theme";
import { SampleSummary, SampleLocationDetail, IncubationDetail } from "../types/sampleSummaryTypes";
import { formatCalendarDate, formatDate } from "./summaryFormatting";
import { formatRetentionDisplay } from "../utils/moistureRetentionHelpers";

// Reusable display field component
export function SummaryField({
  label,
  value,
  secondaryValue,
  highlight = false
}: {
  label: string;
  value: ReactNode;
  secondaryValue?: ReactNode;
  highlight?: boolean;
}) {
  return (
    <Box sx={{ minWidth: 0 }}>
      <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", textTransform: "uppercase", letterSpacing: 0.3 }} noWrap>
        {label}
      </Typography>
      <Typography
        sx={{
          fontSize: 13,
          fontWeight: highlight ? 700 : 600,
          color: highlight ? brandColors.sectionTitle : "text.primary",
          lineHeight: 1.3,
          wordBreak: "break-word"
        }}
      >
        {value != null && value !== "" ? value : "—"}
      </Typography>
      {secondaryValue && (
        <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.25 }}>
          {secondaryValue}
        </Typography>
      )}
    </Box>
  );
}

// 1. Sample Identity Section (4-column responsive grid)
export function SampleIdentityCard({ summary: s }: { summary: SampleSummary }) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  const retentionDisplay = formatRetentionDisplay(s.retentionQuantity, s.retentionQuantityUnit);
  const analystsValue = s.assignedAnalystNames?.length ? (
    <Ltr>{s.assignedAnalystNames.join(", ")}</Ltr>
  ) : s.assignedAnalystName ? (
    <Ltr>{s.assignedAnalystName}</Ltr>
  ) : (
    t("sampleSummary.identity.unassigned")
  );
  const prevBatch = s.previousProductBatchNumber || s.batchNumber;

  return (
    <Paper sx={{ p: 2.5, border: "1px solid", borderColor: "divider", borderRadius: 2, bgcolor: "background.paper" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 15, color: theme.palette.primary.main, mb: 2 }}>
        {t("sampleSummary.identity.title")}
      </Typography>
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "repeat(4, 1fr)"
          },
          gap: 2
        }}
      >
        <SummaryField label={t("sampleSummary.identity.referenceNumber")} value={<Ltr>{s.referenceNumber}</Ltr>} highlight />
        <SummaryField
          label={t("sampleSummary.identity.category")}
          value={t(`sampleCategoryFull.${s.category}`, { ns: "common", defaultValue: s.category })}
        />
        <SummaryField label={t("sampleSummary.identity.receivedAt")} value={<Ltr>{formatDate(s.receivedAt)}</Ltr>} />
        <SummaryField label={t("sampleSummary.identity.expDate")} value={<Ltr>{formatCalendarDate(s.expDate)}</Ltr>} />

        <SummaryField label={t("sampleSummary.identity.displayName")} value={s.displayName} highlight />
        <SummaryField label={t("sampleSummary.identity.productionStage")} value={s.productionStage} />
        <SummaryField label={t("sampleSummary.identity.sampledBy")} value={s.sampledBy ? <Ltr>{s.sampledBy}</Ltr> : null} />
        <SummaryField label={t("sampleSummary.identity.batchNumber")} value={s.batchNumber ? <Ltr>{s.batchNumber}</Ltr> : null} />
        {s.grade && <SummaryField label={t("sampleSummary.identity.grade")} value={<Ltr>{s.grade}</Ltr>} />}

        <SummaryField label={t("sampleSummary.identity.controlNumber")} value={s.controlNumber ? <Ltr>{s.controlNumber}</Ltr> : null} />
        <SummaryField label={t("sampleSummary.identity.receivedBy")} value={s.receivedByName ? <Ltr>{s.receivedByName}</Ltr> : null} />
        <SummaryField label={t("sampleSummary.identity.sampleQuantity")} value={s.sampleQuantity ? <Ltr>{s.sampleQuantity}</Ltr> : null} />
        {s.retentionQuantity != null && (
          <SummaryField
            label={t("sampleSummary.identity.retention")}
            value={retentionDisplay ? <Ltr>{retentionDisplay}</Ltr> : ""}
          />
        )}
        <SummaryField label={t("sampleSummary.identity.mfgDate")} value={<Ltr>{formatCalendarDate(s.mfgDate)}</Ltr>} />

        <SummaryField label={t("sampleSummary.identity.causeOfTesting")} value={s.causeOfTesting} />
        <SummaryField
          label={
            (s.assignedAnalystNames?.length ?? 0) > 1
              ? t("sampleSummary.identity.assignedAnalysts")
              : t("sampleSummary.identity.assignedAnalyst")
          }
          value={analystsValue}
          highlight={Boolean(s.assignedAnalystNames?.length || s.assignedAnalystName)}
        />
        {s.category === "AfterCleaning" && (
          <>
            <SummaryField label={t("sampleSummary.identity.previousProduct")} value={s.previousProductName || "—"} highlight />
            <SummaryField
              label={t("sampleSummary.identity.previousProductBatch")}
              value={prevBatch ? <Ltr>{prevBatch}</Ltr> : "—"}
              highlight
            />
          </>
        )}
        {s.waterSamplingPointCode && (
          <SummaryField
            label={t("sampleSummary.identity.samplingPoint")}
            value={
              <>
                <Ltr>{s.waterSamplingPointCode}</Ltr>
                {s.waterSamplingPointLocation ? `, ${s.waterSamplingPointLocation}` : ""}
              </>
            }
          />
        )}
        {s.storageCondition && (
          <SummaryField
            label={t("sampleSummary.identity.storageCondition")}
            value={
              s.storageCondition === "Refrigerator" ? (
                <Trans
                  i18nKey="sampleSummary.identity.refrigeratorHours"
                  ns="testingWorkspace"
                  values={{ hours: s.storageTimeHours ?? "?" }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              ) : (
                s.storageCondition
              )
            }
          />
        )}
      </Box>
    </Paper>
  );
}

// Sample Preparation Card (if applicable)
export function SamplePreparationCard({ preparation: p }: { preparation: NonNullable<SampleSummary["preparation"]> }) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  return (
    <Paper sx={{ p: 2.5, border: "1px solid", borderColor: "divider", borderRadius: 2, bgcolor: "background.paper" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 15, color: theme.palette.primary.main, mb: 2 }}>
        {t("sampleSummary.preparation.title")}
      </Typography>
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "repeat(4, 1fr)"
          },
          gap: 2
        }}
      >
        <SummaryField label={t("sampleSummary.preparation.amount")} value={<Ltr>{`${p.amount}`}</Ltr>} />
        <SummaryField label={t("sampleSummary.preparation.technique")} value={p.technique} />
        {p.technique === "Filtration" && (
          <>
            <SummaryField
              label={t("sampleSummary.preparation.filtrationVolume")}
              value={p.filtrationVolume ? <Ltr>{`${p.filtrationVolume} mL`}</Ltr> : "—"}
            />
            <SummaryField
              label={t("sampleSummary.preparation.washingVolume")}
              value={p.washingVolume ? <Ltr>{`${p.washingVolume} mL`}</Ltr> : "—"}
            />
          </>
        )}
        <SummaryField label={t("sampleSummary.preparation.diluent")} value={withLot(p.diluent || "—", p.diluentLotCode)} />
        <SummaryField label={t("sampleSummary.preparation.neutralizer")} value={withLot(p.neutralizer || "—", p.neutralizerLotCode)} />
        <SummaryField label={t("sampleSummary.preparation.preparedBy")} value={p.preparedByName ? <Ltr>{p.preparedByName}</Ltr> : null} />
        <SummaryField label={t("sampleSummary.preparation.preparedAt")} value={<Ltr>{formatDate(p.preparedAt)}</Ltr>} />
      </Box>
    </Paper>
  );
}

// Incubation Stage Card (Stage 1, Stage 2, or Standard Single Stage)
export function IncubationStageBlock({
  inc,
  totalStages
}: {
  inc: IncubationDetail;
  totalStages: number;
  stageIndex: number;
}) {
  const { t } = useTranslation("testingWorkspace");
  const isMultiStage = totalStages > 1 || inc.stageNumber > 1 || !!inc.transferredAt || !!inc.transferredByName;
  const isStage1 = inc.stageNumber === 1;

  const transferredBy = inc.transferredByName ?? inc.completedByName;

  return (
    <Box
      sx={{
        p: 2,
        borderRadius: 1.5,
        border: "1px solid",
        borderColor: "divider",
        bgcolor: "background.default"
      }}
    >
      <Box sx={{ display: "flex", alignItems: "center", gap: 1.25, mb: 1.5 }}>
        {isMultiStage && (
          <Box
            sx={{
              width: 22,
              height: 22,
              borderRadius: "50%",
              bgcolor: brandColors.sectionTitle,
              color: "common.white",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 12,
              fontWeight: 700,
              flexShrink: 0
            }}
          >
            <Ltr>{inc.stageNumber}</Ltr>
          </Box>
        )}
        <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
          {isMultiStage ? (
            <Trans
              i18nKey="sampleSummary.incubation.stageTitle"
              ns="testingWorkspace"
              values={{ stage: inc.stageNumber, stepName: inc.stepName }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          ) : (
            t("sampleSummary.incubation.singleTitle", { stepName: inc.stepName })
          )}
        </Typography>
      </Box>

      {/* Primary Incubation Parameters */}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "repeat(4, 1fr)"
          },
          gap: 1.5,
          mb: 1.5
        }}
      >
        <SummaryField
          label={t("sampleSummary.incubation.mediaLot")}
          value={
            inc.mediaLotNumber ? (
              <>
                <Ltr>{inc.mediaLotNumber}</Ltr> ({inc.mediaMaterialName ?? "—"})
              </>
            ) : (
              "—"
            )
          }
        />
        <SummaryField label={t("sampleSummary.incubation.incubator")} value={inc.incubatorName} />
        <SummaryField label={t("sampleSummary.incubation.temperature")} value={inc.temperature ? <Ltr>{inc.temperature}</Ltr> : null} />
        <SummaryField label={t("sampleSummary.incubation.duration")} value={inc.duration ? <Ltr>{inc.duration}</Ltr> : null} />
        <SummaryField
          label={t("sampleSummary.incubation.startedAt")}
          value={<Ltr>{formatDate(inc.startedAt)}</Ltr>}
          secondaryValue={
            inc.startedByName ? (
              <Trans
                i18nKey="sampleSummary.incubation.byUser"
                ns="testingWorkspace"
                values={{ name: inc.startedByName }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            ) : undefined
          }
        />
        <SummaryField label={t("sampleSummary.incubation.startedBy")} value={inc.startedByName ? <Ltr>{inc.startedByName}</Ltr> : null} />
      </Box>

      <Divider sx={{ my: 1.25, borderStyle: "dashed" }} />

      {/* Stage Transition / Completion Details */}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "repeat(3, 1fr)"
          },
          gap: 1.5
        }}
      >
        {isStage1 && isMultiStage ? (
          <>
            <SummaryField
              label={t("sampleSummary.incubation.transferredAt")}
              value={<Ltr>{formatDate(inc.transferredAt ?? inc.completedAt)}</Ltr>}
            />
            <SummaryField
              label={t("sampleSummary.incubation.transferredBy")}
              value={transferredBy ? <Ltr>{transferredBy}</Ltr> : "—"}
            />
            <SummaryField
              label={t("sampleSummary.incubation.outcome")}
              value={inc.outcome ?? t("sampleSummary.incubation.transferredToStage2")}
            />
          </>
        ) : (
          <>
            <SummaryField
              label={t("sampleSummary.incubation.completedAt")}
              value={<Ltr>{formatDate(inc.completedAt)}</Ltr>}
            />
            <SummaryField
              label={t("sampleSummary.incubation.completedBy")}
              value={inc.completedByName ? <Ltr>{inc.completedByName}</Ltr> : "—"}
            />
            <SummaryField
              label={t("sampleSummary.incubation.outcome")}
              value={inc.outcome ?? "—"}
            />
          </>
        )}
      </Box>
    </Box>
  );
}

// Location-based Results Table (EM / After Cleaning)
export function LocationResultsTable({ locations }: { locations: SampleLocationDetail[] }) {
  const { t } = useTranslation("testingWorkspace");
  const conformCount = locations.filter((l) => l.status === "WithinLimits" || l.status === "Absent").length;
  const severityOrder = ["WithinLimits", "Absent", "LimitsNotConfigured", "AlertLimitExceeded", "ActionLimitExceeded", "OutOfSpecification", "Detected"];
  const worstStatus = locations.reduce<string>((worst, l) => {
    if (!l.status) return worst;
    return severityOrder.indexOf(l.status) > severityOrder.indexOf(worst) ? l.status : worst;
  }, "WithinLimits");
  // Unit comes from the location data (set at result-entry time) - EM/
  // After Cleaning/Water mix CFU/plate/4 hours, CFU/25 cm2, and CFU/mL
  // depending on sampling method, never a single assumed "CFU".
  const unit = locations.find((l) => l.unit)?.unit ?? "CFU";

  return (
    <Box>
      <TableContainer>
        <Table size="small" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
          <TableHead sx={tableHeadSx}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("sampleSummary.locations.location")}</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("sampleSummary.locations.limits")}</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "end" }}><Ltr>{unit}</Ltr></TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("sampleSummary.locations.reportedResult")}</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "center" }}>{t("sampleSummary.locations.status")}</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("sampleSummary.locations.enteredBy")}</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("sampleSummary.locations.enteredAt")}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {locations.map((l, idx) => (
              <TableRow key={idx} hover>
                <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                  {l.locationName}{l.gradeClassification ? ` (${l.gradeClassification})` : ""}
                </TableCell>
                <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                  <Ltr>{`${l.alertLimit || "—"} / ${l.actionLimit || "—"} / ${l.specLimit || "—"}`}</Ltr>
                </TableCell>
                <TableCell sx={{ fontSize: 12, textAlign: "end", fontWeight: 700 }}>
                  {l.cfuResult != null ? <Ltr>{l.cfuResult}</Ltr> : "—"}
                </TableCell>
                <TableCell sx={{ fontSize: 12 }}>
                  {l.reportedResult != null ? <Ltr>{l.reportedResult}</Ltr> : "—"}
                </TableCell>
                <TableCell sx={{ textAlign: "center" }}>
                  {l.status ? <StatusBadge status={l.status} /> : "—"}
                </TableCell>
                <TableCell sx={{ fontSize: 12 }}>
                  {l.enteredByName ? <Ltr>{l.enteredByName}</Ltr> : "—"}
                </TableCell>
                <TableCell sx={{ fontSize: 12 }}>
                  <Ltr>{formatDate(l.enteredAt)}</Ltr>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <Box sx={{ mt: 1.25, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          <Trans
            i18nKey="sampleSummary.locations.withinSpec"
            ns="testingWorkspace"
            values={{ conform: conformCount, total: locations.length }}
            components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
          />
        </Typography>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("sampleSummary.locations.worstStatus")}</Typography>
          <StatusBadge status={worstStatus} />
        </Box>
      </Box>
    </Box>
  );
}
