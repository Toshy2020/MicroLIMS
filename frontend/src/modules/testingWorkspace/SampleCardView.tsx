import { Grid, Paper, Box, Typography, Stack, Tooltip, useTheme } from "@mui/material";
import { SampleCard as SampleCardType } from "./types/workspaceTypes";
import { CategoryBadge } from "../../components/StatusBadge";
import { SampleLifecycleBadge } from "./SampleLifecycleBadge";
import { brandColors } from "../../theme";
import { isInteractiveElement } from "../../utils/isInteractiveElement";
import { dueLabel } from "../receiving/components/SampleRegisterTable";
import { Trans } from "react-i18next";
import { Ltr } from "../../components/Ltr";
import { useTranslation } from "../../i18n";
import { assignedAnalystsLabel } from "../receiving/utils/assignedAnalysts";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";

interface Props {
  samples: SampleCardType[];
  selectedSampleId?: number | null;
  onSelectSample: (sample: SampleCardType) => void;
  onNeedsPreparationClick: (sample: SampleCardType) => void;
  onLifecycleBadgeClick: (sampleId: number) => void;
}

export function SampleCardView({
  samples,
  selectedSampleId,
  onSelectSample,
  onNeedsPreparationClick,
  onLifecycleBadgeClick
}: Props) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();

  return (
    <Grid container spacing={2}>
      {samples.map((sample) => {
        const needsPreparation = sample.preparationStatus === "NeedsPreparation";
        const isSelected = selectedSampleId === sample.sampleId;
        const due = dueLabel(sample.dueAt, sample.isOverdue);
        const dueColor = sample.isOverdue ? theme.custom.status.detected.text : theme.palette.text.secondary;

        return (
          <Grid
            key={sample.sampleId}
            size={{
              xs: 12,
              sm: 6,
              md: 4
            }}>
            <Paper
              tabIndex={0}
              role="button"
              onClick={(e) => {
                if (isInteractiveElement(e.target, e.currentTarget)) {
                  return;
                }
                onSelectSample(sample);
              }}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                  if (isInteractiveElement(e.target, e.currentTarget)) {
                    return;
                  }
                  e.preventDefault();
                  onSelectSample(sample);
                }
              }}
              sx={{
                p: 2,
                height: "100%",
                cursor: "pointer",
                // Overdue wins even over selection, as in the register table.
                borderInlineStartWidth: 4,
                borderInlineStartStyle: "solid",
                borderInlineStartColor: sample.isOverdue
                  ? theme.custom.status.detected.text
                  : isSelected
                  ? brandColors.sectionTitle
                  : needsPreparation
                  ? theme.custom.status.inconclusive.text
                  : "transparent",
                bgcolor: isSelected ? theme.custom.status.purple.bg : "background.paper",
                transition: "all 0.15s ease",
                "&:hover": {
                  boxShadow: theme.palette.mode === "dark" ? "0 4px 12px rgba(0,0,0,0.4)" : "0 4px 12px rgba(0,0,0,0.08)",
                  transform: "translateY(-1px)"
                }
              }}
            >
              <Stack
                direction="row"
                sx={{
                  justifyContent: "space-between",
                  alignItems: "flex-start",
                  mb: 1
                }}>
                <Box>
                  <Typography sx={{ fontWeight: isSelected ? 700 : 600, fontSize: 14, color: isSelected ? brandColors.pageTitle : "text.primary" }}>
                    {sample.displayName}
                  </Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                    <Ltr>{sample.referenceNumber}</Ltr>
                  </Typography>
                </Box>
                <CategoryBadge category={sample.category} />
              </Stack>

              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
                {sample.causeOfTesting} · <Ltr>{formatDateTimeWithTimeZone(sample.receivedAt)}</Ltr>
              </Typography>

              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: due ? 1 : 1.5 }}>
                {sample.batchNumber ? (
                  <Trans i18nKey="cardView.batchLine" ns="testingWorkspace" values={{ batch: sample.batchNumber }} components={{ ltr: <Ltr>{null}</Ltr> }} />
                ) : (
                  <Trans i18nKey="cardView.controlLine" ns="testingWorkspace" values={{ control: sample.controlNumber }} components={{ ltr: <Ltr>{null}</Ltr> }} />
                )}
                {" · "}
                <Trans i18nKey="cardView.testsCount" ns="testingWorkspace" values={{ count: sample.assignedTests.length }} components={{ ltr: <Ltr>{null}</Ltr> }} />
              </Typography>

              {due && sample.dueAt && (
                <Tooltip
                  title={
                    <Trans
                      i18nKey="cardView.dueTooltip"
                      ns="testingWorkspace"
                      values={{ date: formatDateTimeWithTimeZone(sample.dueAt) }}
                      components={{ ltr: <Ltr>{null}</Ltr> }}
                    />
                  }
                >
                  <Typography
                    sx={{
                      display: "inline-flex", alignItems: "center", gap: 0.75, mb: 1.5,
                      fontSize: 12, fontWeight: sample.isOverdue ? 700 : 500, color: dueColor,
                      "&::before": { content: '""', width: 6, height: 6, borderRadius: "50%", bgcolor: dueColor }
                    }}
                  >
                    {due}
                  </Typography>
                </Tooltip>
              )}

              <Stack
                direction="row"
                sx={{
                  justifyContent: "space-between",
                  alignItems: "center"
                }}>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  {(() => {
                    const label = assignedAnalystsLabel(sample.assignedTests);
                    return label ? <Ltr>{label}</Ltr> : t("cardView.unassigned");
                  })()}
                </Typography>
                <SampleLifecycleBadge
                  status={sample.status}
                  onClick={() => onLifecycleBadgeClick(sample.sampleId)}
                />
              </Stack>
            </Paper>
          </Grid>
        );
      })}
      {samples.length === 0 && (
        <Grid size={12}>
          <Typography sx={{ color: "text.secondary", fontSize: 13, p: 2 }}>
            {t("cardView.emptyFiltered")}
          </Typography>
        </Grid>
      )}
    </Grid>
  );
}
