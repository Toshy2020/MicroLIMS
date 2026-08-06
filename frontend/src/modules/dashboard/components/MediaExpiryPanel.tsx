import { Paper, Box, Typography, Stack } from "@mui/material";
import { useNavigate } from "react-router-dom";
import HourglassBottomOutlinedIcon from "@mui/icons-material/HourglassBottomOutlined";
import { StatusBadge } from "../../../components/StatusBadge";
import { SectionTitle } from "../../../components/SectionTitle";
import { LoadingSpinner } from "../../../components/LoadingSpinner";
import { useMediaExpiry } from "../hooks/useMediaExpiry";

// No remaining-quantity (plates/tubes/units) column - that field doesn't
// exist on Media yet; deferred until a schema change is decided on.
export function MediaExpiryPanel() {
  const { data: lots, loading } = useMediaExpiry();
  const navigate = useNavigate();

  return (
    <Paper sx={{ p: 2.5 }}>
      <SectionTitle tabs={[{ label: "View inventory", onClick: () => navigate("/laboratory-configuration/media") }]}>Media Expiry</SectionTitle>
      {loading || !lots ? <LoadingSpinner /> : (
        <Stack spacing={1.25}>
          {lots.map((lot) => (
            <Box key={lot.mediaId} sx={{ display: "flex", alignItems: "center", gap: 1.25, p: 1, border: "1px solid #f0e6f2", borderRadius: 1.5 }}>
              <HourglassBottomOutlinedIcon fontSize="small" sx={{ color: lot.daysRemaining <= 3 ? "#dc2626" : "#ea580c" }} />
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{lot.mediaTypeName} lot {lot.lotNumber}</Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  Expires in {lot.daysRemaining} day{lot.daysRemaining === 1 ? "" : "s"}
                </Typography>
              </Box>
              <StatusBadge status={lot.evaluationStatus} />
            </Box>
          ))}
          {lots.length === 0 && <Typography sx={{ fontSize: 13, color: "text.secondary" }}>No lots expiring soon.</Typography>}
        </Stack>
      )}
    </Paper>
  );
}
