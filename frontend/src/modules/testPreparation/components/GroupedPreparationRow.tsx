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
import { SignatureDialog } from "../../../components/SignatureDialog";
import { PreparationStepsSummary } from "../PreparationStepsSummary";
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
  const theme = useTheme();
  const [expanded, setExpanded] = useState(false);
  const [signing, setSigning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const sampleWord = group.sampleCount === 1 ? "Sample" : "Samples";

  // Errors propagate to SignatureDialog, which surfaces the server message
  // and keeps itself open with the password cleared.
  const confirm = async (password: string) => {
    setError(null);
    const result = await SamplePreparationService.confirmBatch({
      sampleIds: group.samples.map((s) => s.sampleId),
      configurationId: group.configurationId,
      password
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
            Test Preparation · {group.itemName}
          </Typography>

          <Chip
            size="small"
            label={`× ${group.sampleCount} ${sampleWord}`}
            sx={{
              height: 20,
              fontSize: "0.68rem",
              fontWeight: 700,
              bgcolor: theme.palette.mode === "dark" ? "rgba(99, 102, 241, 0.15)" : "#EEF2FF",
              color: theme.palette.mode === "dark" ? "#A5B4FC" : "#4338CA",
              borderRadius: "4px"
            }}
          />

          <Chip
            size="small"
            label={group.approvalStatus === "Approved" ? "Approved" : "Pending Approval"}
            color={group.approvalStatus === "Approved" ? "success" : "warning"}
            sx={{ height: 20, fontSize: "0.68rem", fontWeight: 700, borderRadius: "4px" }}
          />
        </Box>

        <Box sx={{ flex: 1, minWidth: 0, textAlign: "center", px: 1 }}>
          <Typography noWrap sx={{ fontSize: "0.74rem", color: "text.secondary", fontWeight: 500 }}>
            {group.technique === "PourPlate" ? "Pour Plate" : group.technique} · Amount {group.amount}
            {group.diluent ? ` · ${group.diluent}` : ""}
          </Typography>
        </Box>

        <IconButton
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
            <Typography sx={{ fontSize: "0.68rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 0.5 }}>
              Samples to prepare ({group.sampleCount}):
            </Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75 }}>
              {group.samples.map((s) => (
                <Chip
                  key={s.sampleId}
                  size="small"
                  label={s.batchNumber ? `${s.sampleReference} · ${s.batchNumber}` : s.sampleReference}
                  sx={{
                    height: 24,
                    fontSize: "0.72rem",
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
            <Alert severity="info" sx={{ mb: 1.5, py: 0.25, fontSize: "0.74rem" }}>
              This item's preparation configuration is still awaiting Section Head approval. It is in effect and
              can be confirmed now - approval is a separate review and does not hold up testing.
            </Alert>
          )}

          {error && (
            <Alert severity="error" sx={{ mb: 1.5, py: 0.25, fontSize: "0.74rem" }}>
              {error}
            </Alert>
          )}

          <Typography sx={{ fontSize: "0.68rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 0.75 }}>
            Configured Preparation Steps
          </Typography>

          <PreparationStepsSummary
            config={{
              amount: group.amount,
              technique: group.technique,
              filtrationVolume: group.filtrationVolume,
              washingVolume: group.washingVolume,
              diluent: group.diluent,
              neutralizer: group.neutralizer
            }}
          />

          <Typography variant="caption" sx={{ display: "block", mt: 1.5, color: "text.secondary" }}>
            Confirming records these exact values against every sample listed above, each with its own signature.
            Later changes to the item's configuration will not alter those records.
          </Typography>

          <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 1.5 }}>
            <Button
              variant="contained"
              size="small"
              onClick={() => setSigning(true)}
              sx={{ textTransform: "none", fontWeight: 700, fontSize: "0.75rem" }}
            >
              Confirm &amp; Start Testing ({group.sampleCount} {sampleWord})
            </Button>
          </Box>
        </Box>
      </Collapse>

      <SignatureDialog
        open={signing}
        meaningStatement={
          `I confirm the preparation steps shown above are the steps performed for all ${group.sampleCount} ` +
          `${sampleWord.toLowerCase()} of ${group.itemName} listed in this group ` +
          `(${group.samples.map((s) => s.sampleReference).join(", ")}).`
        }
        onCancel={() => setSigning(false)}
        onConfirm={confirm}
      />
    </Paper>
  );
}
