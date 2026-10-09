import { Box, Typography, Divider, Alert, Table, TableHead, TableRow, TableCell, TableBody, Paper, useTheme, TableContainer } from "@mui/material";
import { StatusBadge } from "../../../components/StatusBadge";
import { brandColors, tableHeadSx } from "../../../theme";
import { SampleSummary, SampleLocationDetail, IncubationDetail } from "../types/sampleSummaryTypes";
import { formatDate } from "./summaryFormatting";

// Reusable display field component
export function SummaryField({
  label,
  value,
  secondaryValue,
  highlight = false
}: {
  label: string;
  value: string | number | null | undefined;
  secondaryValue?: string | null | undefined;
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
  const theme = useTheme();
  return (
    <Paper sx={{ p: 2.5, border: "1px solid", borderColor: "divider", borderRadius: 2, bgcolor: "background.paper" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 15, color: theme.palette.primary.main, mb: 2 }}>
        Sample Identity
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
        <SummaryField label="Reference Number" value={s.referenceNumber} highlight />
        <SummaryField label="Category" value={s.category} />
        <SummaryField label="Received At" value={formatDate(s.receivedAt)} />
        <SummaryField label="Exp Date" value={formatDate(s.expDate)} />

        <SummaryField label="Item / Point / Room / Machine" value={s.displayName} highlight />
        <SummaryField label="Production Stage" value={s.productionStage} />
        <SummaryField label="Sampled By" value={s.sampledBy} />
        <SummaryField label="Batch Number" value={s.batchNumber} />

        <SummaryField label="Control Number" value={s.controlNumber} />
        <SummaryField label="Received By" value={s.receivedByName} />
        <SummaryField label="Sample Quantity" value={s.sampleQuantity} />
        <SummaryField label="Mfg Date" value={formatDate(s.mfgDate)} />

        <SummaryField label="Cause of Testing" value={s.causeOfTesting} />
        <SummaryField label="Assigned Analyst" value={s.assignedAnalystName ?? "Unassigned"} highlight={Boolean(s.assignedAnalystName)} />
        {s.category === "AfterCleaning" && (
          <>
            <SummaryField label="Previous Product" value={s.previousProductName || "—"} highlight />
            <SummaryField label="Previous Product Batch" value={s.previousProductBatchNumber || s.batchNumber || "—"} highlight />
          </>
        )}
        {s.waterSamplingPointCode && (
          <SummaryField label="Sampling Point" value={`${s.waterSamplingPointCode}, ${s.waterSamplingPointLocation}`} />
        )}
        {s.storageCondition && (
          <SummaryField
            label="Storage Condition"
            value={s.storageCondition === "Refrigerator" ? `Refrigerator (${s.storageTimeHours ?? "?"}h)` : s.storageCondition}
          />
        )}
      </Box>
    </Paper>
  );
}

// Sample Preparation Card (if applicable)
export function SamplePreparationCard({ preparation: p }: { preparation: NonNullable<SampleSummary["preparation"]> }) {
  const theme = useTheme();
  return (
    <Paper sx={{ p: 2.5, border: "1px solid", borderColor: "divider", borderRadius: 2, bgcolor: "background.paper" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 15, color: theme.palette.primary.main, mb: 2 }}>
        Sample Preparation
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
        <SummaryField label="Amount" value={`${p.amount}`} />
        <SummaryField label="Technique" value={p.technique} />
        {p.technique === "Filtration" && (
          <>
            <SummaryField label="Filtration Volume" value={p.filtrationVolume ? `${p.filtrationVolume} mL` : "—"} />
            <SummaryField label="Washing Volume" value={p.washingVolume ? `${p.washingVolume} mL` : "—"} />
          </>
        )}
        <SummaryField label="Diluent" value={p.diluent || "—"} />
        <SummaryField label="Neutralizer" value={p.neutralizer || "—"} />
        <SummaryField label="Prepared By" value={p.preparedByName} />
        <SummaryField label="Prepared At" value={formatDate(p.preparedAt)} />
      </Box>
    </Paper>
  );
}

// Incubation Stage Card (Stage 1, Stage 2, or Standard Single Stage)
export function IncubationStageBlock({
  inc,
  totalStages,
  stageIndex
}: {
  inc: IncubationDetail;
  totalStages: number;
  stageIndex: number;
}) {
  const isMultiStage = totalStages > 1 || inc.stageNumber > 1 || !!inc.transferredAt || !!inc.transferredByName;
  const isStage1 = inc.stageNumber === 1;
  const isStage2 = inc.stageNumber === 2;

  const stageTitle = isMultiStage
    ? isStage1
      ? `Stage 1 Incubation (${inc.stepName})`
      : isStage2
      ? `Stage 2 Incubation (${inc.stepName})`
      : `Stage ${inc.stageNumber} Incubation (${inc.stepName})`
    : `Incubation (${inc.stepName})`;

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
            {inc.stageNumber}
          </Box>
        )}
        <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
          {stageTitle}
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
          label="Media Lot"
          value={inc.mediaLotNumber ? `${inc.mediaLotNumber} (${inc.mediaMaterialName ?? "—"})` : "—"}
        />
        <SummaryField label="Incubator" value={inc.incubatorName} />
        <SummaryField label="Temperature" value={inc.temperature} />
        <SummaryField label="Duration" value={inc.duration} />
        <SummaryField
          label="Started At"
          value={formatDate(inc.startedAt)}
          secondaryValue={inc.startedByName ? `By ${inc.startedByName}` : undefined}
        />
        <SummaryField label="Started By" value={inc.startedByName} />
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
              label="Transferred At"
              value={formatDate(inc.transferredAt ?? inc.completedAt)}
            />
            <SummaryField
              label="Transferred By"
              value={inc.transferredByName ?? inc.completedByName ?? "—"}
            />
            <SummaryField
              label="Outcome"
              value={inc.outcome ?? "Transferred to stage 2 incubation."}
            />
          </>
        ) : (
          <>
            <SummaryField
              label="Completed At"
              value={formatDate(inc.completedAt)}
            />
            <SummaryField
              label="Completed By"
              value={inc.completedByName ?? "—"}
            />
            <SummaryField
              label="Outcome"
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
              <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Location</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Limits (Alert / Action / Spec)</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>{unit}</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Reported Result</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "center" }}>Status</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Entered By</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Entered At</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {locations.map((l, idx) => (
              <TableRow key={idx} hover>
                <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                  {l.locationName}{l.gradeClassification ? ` (${l.gradeClassification})` : ""}
                </TableCell>
                <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                  {l.alertLimit || "—"} / {l.actionLimit || "—"} / {l.specLimit || "—"}
                </TableCell>
                <TableCell sx={{ fontSize: 12, textAlign: "right", fontWeight: 700 }}>
                  {l.cfuResult ?? "—"}
                </TableCell>
                <TableCell sx={{ fontSize: 12 }}>
                  {l.reportedResult ?? "—"}
                </TableCell>
                <TableCell sx={{ textAlign: "center" }}>
                  {l.status ? <StatusBadge status={l.status} /> : "—"}
                </TableCell>
                <TableCell sx={{ fontSize: 12 }}>
                  {l.enteredByName || "—"}
                </TableCell>
                <TableCell sx={{ fontSize: 12 }}>
                  {formatDate(l.enteredAt)}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <Box sx={{ mt: 1.25, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          <strong>{conformCount}/{locations.length}</strong> locations within specification
        </Typography>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Worst status:</Typography>
          <StatusBadge status={worstStatus} />
        </Box>
      </Box>
    </Box>
  );
}
