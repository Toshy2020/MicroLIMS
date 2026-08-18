import { Box, Typography, Stack, Chip, Divider } from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import AccessTimeOutlinedIcon from "@mui/icons-material/AccessTimeOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import { SharedTsbStateDto } from "../types/pathogenSessionTypes";
import { brandColors } from "../../../theme";

interface Props {
  sharedTsb: SharedTsbStateDto;
}

const formatDate = (d: string | null | undefined) =>
  d
    ? new Date(d).toLocaleString("en-GB", {
        day: "2-digit",
        month: "short",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit"
      })
    : "—";

export function SharedTsbStatusCard({ sharedTsb }: Props) {
  if (!sharedTsb.isStarted) {
    return (
      <Box
        sx={{
          p: 2.5,
          borderRadius: 2,
          border: "1px dashed #d1d5db",
          bgcolor: "#f9fafb",
          textAlign: "center"
        }}
      >
        <Typography sx={{ fontSize: 13, color: "#6b7280", fontWeight: 500 }}>
          Shared TSB Enrichment has not been started yet.
        </Typography>
      </Box>
    );
  }

  return (
    <Box
      sx={{
        p: 2.5,
        borderRadius: 2,
        border: "1px solid #e5e7eb",
        bgcolor: "#faf5ff",
        boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
      }}
    >
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
        <Stack direction="row" spacing={1.5} alignItems="center">
          <Box
            sx={{
              width: 36,
              height: 36,
              borderRadius: 1.5,
              bgcolor: brandColors.sectionTitle,
              color: "#ffffff",
              display: "flex",
              alignItems: "center",
              justifyContent: "center"
            }}
          >
            <ScienceOutlinedIcon sx={{ fontSize: 20 }} />
          </Box>
          <Box>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: "#1f2937" }}>
              Shared TSB Enrichment / Incubation
            </Typography>
            <Typography sx={{ fontSize: 12, color: "#6b7280" }}>
              Single shared laboratory procedure applied across all applicable pathogen tests
            </Typography>
          </Box>
        </Stack>

        <Chip
          icon={sharedTsb.isCompleted ? <CheckCircleOutlineIcon /> : <AccessTimeOutlinedIcon />}
          label={sharedTsb.isCompleted ? "Incubation Completed" : "Incubating in Progress"}
          size="small"
          sx={{
            fontWeight: 700,
            fontSize: 12,
            bgcolor: sharedTsb.isCompleted ? "#ecfdf5" : "#eff6ff",
            color: sharedTsb.isCompleted ? "#065f46" : "#1e40af",
            border: `1px solid ${sharedTsb.isCompleted ? "#a7f3d0" : "#bfdbfe"}`
          }}
        />
      </Stack>

      <Divider sx={{ my: 1.5, borderColor: "#e9d5ff" }} />

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(5, 1fr)" },
          gap: 2
        }}
      >
        <Box>
          <Typography sx={{ fontSize: 11, fontWeight: 600, color: "#7c3aed", textTransform: "uppercase" }}>
            TSB Media Lot
          </Typography>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "#1f2937" }}>
            {sharedTsb.mediaLotNumber ?? "—"}
          </Typography>
          <Stack direction="row" spacing={0.5} alignItems="center" sx={{ mt: 0.5 }}>
            <VerifiedUserOutlinedIcon sx={{ fontSize: 13, color: "#059669" }} />
            <Typography sx={{ fontSize: 11, color: "#059669", fontWeight: 600 }}>
              {sharedTsb.gptStatus ?? "GPT Conform"}
            </Typography>
          </Stack>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 11, fontWeight: 600, color: "#7c3aed", textTransform: "uppercase" }}>
            Incubator Equipment
          </Typography>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "#1f2937" }}>
            {sharedTsb.incubatorCode ?? "—"}
          </Typography>
          <Typography sx={{ fontSize: 12, color: "#4b5563" }}>
            {sharedTsb.temperature ?? "30–35 °C"}
          </Typography>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 11, fontWeight: 600, color: "#7c3aed", textTransform: "uppercase" }}>
            Incubation Start
          </Typography>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "#1f2937" }}>
            {formatDate(sharedTsb.actualStartUtc)}
          </Typography>
          <Typography sx={{ fontSize: 11, color: "#6b7280" }}>
            By {sharedTsb.startedByUserName ?? "Analyst"}
          </Typography>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 11, fontWeight: 600, color: "#7c3aed", textTransform: "uppercase" }}>
            Available From
          </Typography>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "#059669" }}>
            {formatDate(sharedTsb.minReadyAt ?? sharedTsb.actualStartUtc)}
          </Typography>
          <Typography sx={{ fontSize: 11, color: "#059669", fontWeight: 600 }}>
            Unlock Point ({sharedTsb.requiredDurationRange ?? "18–24 h"})
          </Typography>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 11, fontWeight: 600, color: "#7c3aed", textTransform: "uppercase" }}>
            Expected Window
          </Typography>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "#1f2937" }}>
            {formatDate(sharedTsb.expectedCompletionUtc)}
          </Typography>
          <Typography sx={{ fontSize: 11, color: "#6b7280" }}>
            Max Duration: {sharedTsb.incubationDurationHours ?? 24} h
          </Typography>
        </Box>
      </Box>

      <Box sx={{ mt: 2, p: 1.5, bgcolor: "#ffffff", borderRadius: 1.5, border: "1px solid #f3e8ff" }}>
        <Typography sx={{ fontSize: 12, color: "#4b5563" }}>
          <strong>Scope:</strong> Applies to <strong>{sharedTsb.applicableLocationCount}</strong> sampling locations and{" "}
          <strong>{sharedTsb.applicableTestCodes.length}</strong> applicable pathogen tests (
          {sharedTsb.applicableTestCodes.join(", ")})
        </Typography>
      </Box>
    </Box>
  );
}
