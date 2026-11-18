import React, { useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  Collapse,
  IconButton,
  Paper,
  Typography,
  useTheme
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { useAuth } from "../../../contexts/AuthContext";
import { i18n, useTranslation } from "../../../i18n";
import { PERMISSIONS } from "../../../routes/routes";
import { PreparationStepsSummary } from "../PreparationStepsSummary";
import { PreparationLotPicker } from "./PreparationLotPicker";
import { useGroupLotOptions } from "../hooks/useGroupedPreparation";
import {
  SamplePreparationService,
  type BatchConfirmPreparationResponse,
  type GroupedPreparation
} from "../services/SamplePreparationService";

interface Props {
  group: GroupedPreparation;
  onComplete: (result: BatchConfirmPreparationResponse) => void;
}

// One row per item preparation configuration. The steps are shown as they
// stand and signed for as-is - correcting them is a Section Head
// configuration change under Laboratory Configuration, not an analyst-side
// override, exactly as in the single-sample ConfirmPreparationForm.
export function GroupedPreparationRow({ group, onComplete }: Props) {
  const { t } = useTranslation("testPreparation");
  const theme = useTheme();
  // The server decides; QC Manager (approval-only) simply is not offered the action.
  const canPrepare = useAuth().permissions.includes(PERMISSIONS.SAMPLES_PREPARE);
  const [expanded, setExpanded] = useState(false);
  const [signing, setSigning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { lots, error: lotsError } = useGroupLotOptions(group.configurationId, expanded);
  const [diluentLotId, setDiluentLotId] = useState<number | null>(null);
  const [neutralizerLotId, setNeutralizerLotId] = useState<number | null>(null);

  const needsDiluentLot = Boolean(lots?.diluent.lotRequired);
  const needsNeutralizerLot = Boolean(lots?.neutralizer.lotRequired);
  const lotsComplete =
    lots != null &&
    (!needsDiluentLot || diluentLotId != null) &&
    (!needsNeutralizerLot || neutralizerLotId != null);

  const techniqueLabel = t(`techniques.${group.technique}`, { defaultValue: group.technique });
  const tEn = i18n.getFixedT("en", "testPreparation");

  // Errors propagate to SignatureDialog, which surfaces the server message
  // and keeps itself open with the password cleared.
  const confirm = async (password: string) => {
    setError(null);
    const result = await SamplePreparationService.confirmBatch({
      sampleIds: group.samples.map((s) => s.sampleId),
      configurationId: group.configurationId,
      password,
      diluentLotId: needsDiluentLot ? diluentLotId : null,
      neutralizerLotId: needsNeutralizerLot ? neutralizerLotId : null
    });
    setSigning(false);
    setExpanded(false);
    onComplete(result);
  };

  return (
    <Paper
      elevation={0}
      sx={{
        border: "1px solid",
        borderColor: expanded ? "primary.main" : theme.palette.divider,
        borderRadius: 1.5,
        transition: "all 0.15s ease",
        bgcolor: theme.palette.background.paper,
        overflow: "hidden"
      }}
    >
      {/* Collapsed single-line row */}
      <Box
        onClick={() => setExpanded((p) => !p)}
        sx={{
          minHeight: 46,
          px: 1.5,
          py: 0.75,
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          cursor: "pointer",
          gap: 1.25,
          "&:hover": { bgcolor: theme.palette.action.hover }
        }}
      >
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexShrink: 0, minWidth: 0 }}>
          <ScienceOutlinedIcon sx={{ fontSize: 18, color: "primary.main" }} />
          <Typography noWrap sx={{ fontWeight: 700, fontSize: "0.82rem" }}>
            {t("grouped.rowTitle", { itemName: group.itemName })}
          </Typography>

          <Chip
            size="small"
            label={t("grouped.sampleCountChip", { count: group.sampleCount })}
            sx={{
              height: 20,
              fontSize: "0.75rem",
              fontWeight: 700,
              bgcolor: theme.custom.status.purple.bg,
              color: theme.custom.status.purple.text,
              borderRadius: "4px"
            }}
          />

          <Chip
            size="small"
            label={group.approvalStatus === "Approved" ? t("status.approved") : t("status.pendingApproval")}
            color={group.approvalStatus === "Approved" ? "success" : "warning"}
            sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700, borderRadius: "4px" }}
          />
        </Box>

        <Box sx={{ flex: 1, minWidth: 0, textAlign: "center", px: 1 }}>
          <Typography noWrap sx={{ fontSize: "0.75rem", color: "text.secondary", fontWeight: 500 }}>
            {group.diluent
              ? t("grouped.summaryLineWithDiluent", { technique: techniqueLabel, amount: group.amount, diluent: group.diluent })
              : t("grouped.summaryLine", { technique: techniqueLabel, amount: group.amount })}
          </Typography>
        </Box>

        <IconButton aria-label={expanded ? t("grouped.collapseGroup") : t("grouped.expandGroup")} aria-expanded={Boolean(expanded)}
          size="small"
          onClick={(e) => {
            e.stopPropagation();
            setExpanded((p) => !p);
          }}
          sx={{ width: 26, height: 26, color: expanded ? "primary.main" : "text.secondary", flexShrink: 0 }}
        >
          {expanded ? <KeyboardArrowUpIcon fontSize="small" /> : <KeyboardArrowDownIcon fontSize="small" />}
        </IconButton>
      </Box>

      <Collapse in={expanded} timeout="auto" unmountOnExit>
        <Box
          onClick={(e) => e.stopPropagation()}
          sx={{
            p: 1.5,
            pt: 1,
            borderTop: "1px solid",
            borderColor: theme.palette.divider,
            bgcolor: theme.palette.mode === "dark" ? "rgba(255,255,255,0.02)" : "rgba(0,0,0,0.015)"
          }}
        >
          <Box sx={{ mb: 1.5 }}>
            <Typography sx={{ fontSize: "0.75rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 0.5 }}>
              <Trans
                i18nKey="grouped.samplesToPrepare"
                ns="testPreparation"
                values={{ count: group.sampleCount }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75 }}>
              {group.samples.map((s) => (
                <Chip
                  key={s.sampleId}
                  size="small"
                  label={<Ltr>{s.batchNumber ? `${s.sampleReference} · ${s.batchNumber}` : s.sampleReference}</Ltr>}
                  sx={{
                    height: 24,
                    fontSize: "0.75rem",
                    fontWeight: 600,
                    borderRadius: "6px",
                    bgcolor: theme.palette.background.paper,
                    border: "1px solid",
                    borderColor: theme.palette.divider
                  }}
                />
              ))}
            </Box>
          </Box>

          {group.approvalStatus === "PendingReview" && (
            <Alert severity="info" sx={{ mb: 1.5, py: 0.25, fontSize: "0.75rem" }}>
              {t("confirm.pendingApprovalInfo")}
            </Alert>
          )}

          {error && (
            <Alert severity="error" sx={{ mb: 1.5, py: 0.25, fontSize: "0.75rem" }}>
              {error}
            </Alert>
          )}

          <Typography sx={{ fontSize: "0.75rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 0.75 }}>
            {t("grouped.configuredSteps")}
          </Typography>

          <PreparationStepsSummary
            config={{
              amount: group.amount,
              technique: group.technique,
              filtrationVolume: group.filtrationVolume,
              washingVolume: group.washingVolume,
              diluent: group.diluent,
              neutralizer: group.neutralizer,
              diluentSource: group.diluentSource,
              neutralizerSource: group.neutralizerSource
            }}
          />

          {lotsError && <Alert severity="error" sx={{ mt: 1.5, py: 0.25, fontSize: "0.75rem" }}>{lotsError}</Alert>}
          {(lots?.needsMaterialMapping || group.needsMaterialMapping) && (
            <Alert severity="warning" sx={{ mt: 1.5, py: 0.25, fontSize: "0.75rem" }}>
              {t("grouped.needsMaterialMapping")}
            </Alert>
          )}
          {lots && !lots.needsMaterialMapping && (needsDiluentLot || needsNeutralizerLot) && (
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 1.5, mt: 1.5 }}>
              <Box sx={{ display: "grid", gap: 1, alignContent: "start" }}>
                <PreparationLotPicker label={t("fields.diluent")} group={lots.diluent} value={diluentLotId} onChange={setDiluentLotId} />
              </Box>
              <Box sx={{ display: "grid", gap: 1, alignContent: "start" }}>
                <PreparationLotPicker label={t("fields.neutralizer")} group={lots.neutralizer} value={neutralizerLotId} onChange={setNeutralizerLotId} />
              </Box>
            </Box>
          )}

          <Typography variant="caption" sx={{ display: "block", mt: 1.5, color: "text.secondary" }}>
            {t("grouped.auditNote")}
          </Typography>

          {canPrepare && (
          <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 1.5 }}>
            <Button
              variant="contained"
              size="small"
              onClick={() => setSigning(true)}
              disabled={!lotsComplete}
              sx={{ textTransform: "none", fontWeight: 700, fontSize: "0.75rem" }}
            >
              <Trans
                i18nKey="grouped.confirmButton"
                ns="testPreparation"
                count={group.sampleCount}
                values={{ count: group.sampleCount }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Button>
          </Box>
          )}
        </Box>
      </Collapse>

      <SignatureDialog
        open={signing}
        meaningStatement={tEn("grouped.signatureMeaning", {
          count: group.sampleCount,
          itemName: group.itemName,
          references: group.samples.map((s) => s.sampleReference).join(", ")
        })}
        meaningStatementTranslation={t("grouped.signatureMeaning", {
          count: group.sampleCount,
          itemName: group.itemName,
          references: group.samples.map((s) => s.sampleReference).join(t("common:listSeparator"))
        })}
        onCancel={() => setSigning(false)}
        onConfirm={confirm}
      />
    </Paper>
  );
}
