import { useEffect, useState } from "react";
import {
  Box,
  Typography,
  Chip,
  Divider,
  Button,
  Alert,
  CircularProgress,
  Stack
} from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { MaterialService } from "../services/MaterialService";
import { formatLabDate } from "../../../../utils/formatDate";
import type { MaterialItem, CoeEligibilityResult } from "../types/materialTypes";
import { COA_REQUIRED_TYPES } from "../types/materialTypes";
import { MaterialDocumentList } from "./MaterialDocumentList";
import { UploadMaterialDocumentDialog } from "./UploadMaterialDocumentDialog";
import { useAuth } from "../../../../contexts/AuthContext";

interface Props {
  open: boolean;
  material: MaterialItem | null;
  onClose: () => void;
}

function MetaRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Box sx={{ display: "flex", gap: 1, mb: 0.75, alignItems: "center" }}>
      <Typography component="span" sx={{ fontSize: 12, color: "text.secondary", minWidth: 110, flexShrink: 0 }}>
        {label}
      </Typography>
      <Box sx={{ fontSize: 13, fontWeight: 500, display: "flex", alignItems: "center" }}>
        {value}
      </Box>
    </Box>
  );
}

export function MaterialLotDetailsDialog({ open, material, onClose }: Props) {
  const { role } = useAuth();
  const [eligibility, setEligibility] = useState<CoeEligibilityResult | null>(null);
  const [eligibilityLoading, setEligibilityLoading] = useState(false);
  const [uploadOpen, setUploadOpen] = useState(false);
  const [refresh, setRefresh] = useState(0);

  const coaRequired = material ? COA_REQUIRED_TYPES.includes(material.materialType) : false;
  const isExpired = material?.status === "Expired";

  useEffect(() => {
    if (!open || !material || !coaRequired) {
      setEligibility(null);
      return;
    }
    setEligibilityLoading(true);
    MaterialService.getCOAEligibility(material.id)
      .then(setEligibility)
      .catch(() => setEligibility(null))
      .finally(() => setEligibilityLoading(false));
  }, [open, material?.id, coaRequired, refresh]);

  if (!material) return null;

  // Canonical ATCC: organism.atccNumber first, then material.atccNumber as fallback
  const canonicalAtcc = material.organism?.atccNumber ?? material.atccNumber;

  const handleUploadSuccess = () => {
    setUploadOpen(false);
    setRefresh((r) => r + 1);
  };

  return (
    <>
      <FloatingDialog
        open={open}
        title="Lot Details"
        onClose={onClose}
        actions={
          <Button
            id="lot-details-upload-btn"
            variant="contained"
            size="small"
            onClick={() => setUploadOpen(true)}
            sx={{ ml: 1 }}
          >
            Upload Document
          </Button>
        }
      >
        {/* ---- Lot Metadata ---- */}
        <Box sx={{ mb: 2 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 15, mb: 0.25 }}>
            {material.materialName}
          </Typography>
          {material.organism?.scientificName && (
            <Typography sx={{ fontSize: 12, color: "text.secondary", fontStyle: "italic" }}>
              {material.organism.scientificName}
              {canonicalAtcc && ` · ATCC ${canonicalAtcc}`}
            </Typography>
          )}
          {!material.organism?.scientificName && canonicalAtcc && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              ATCC {canonicalAtcc}
            </Typography>
          )}
        </Box>

        <Box sx={{ mb: 2 }}>
          <MetaRow label="Manufacturer" value={material.manufacturerName || "—"} />
          <MetaRow label="Batch / Lot" value={<span style={{ fontFamily: "monospace", fontWeight: 700 }}>{material.batchNumber}</span>} />
          <MetaRow label="Received" value={formatLabDate(material.receivingDate)} />
          <MetaRow label="Expiry" value={material.expiryDate ? formatLabDate(material.expiryDate) : "None"} />
          <MetaRow label="Quantity" value={`${material.quantityRemaining} / ${material.quantityReceived} ${material.unit}`} />
          <MetaRow
            label="Status"
            value={
              <Chip
                label={isExpired ? "Expired" : material.status === "Depleted" ? "Depleted" : "In Stock"}
                size="small"
                color={isExpired ? "error" : material.status === "Depleted" ? "default" : "success"}
                variant="outlined"
                sx={{ fontSize: 11 }}
              />
            }
          />
          {isExpired && (
            <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.5, fontStyle: "italic" }}>
              Historical — lot expired
            </Typography>
          )}
        </Box>

        {/* ---- COA Eligibility Banner ---- */}
        {coaRequired && (
          <>
            {eligibilityLoading ? (
              <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2 }}>
                <CircularProgress size={14} />
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Checking COA status…</Typography>
              </Box>
            ) : eligibility ? (
              <Alert
                id="lot-coa-eligibility-banner"
                severity={eligibility.hasCurrentCoa ? "success" : "warning"}
                icon={eligibility.hasCurrentCoa ? <CheckCircleOutlineIcon fontSize="small" /> : <InfoOutlinedIcon fontSize="small" />}
                sx={{ mb: 2, py: 0.5 }}
              >
                {eligibility.hasCurrentCoa ? (
                  <Typography sx={{ fontSize: 12 }}>
                    <strong>COA Requirement Satisfied</strong> — a current COA is on file.
                  </Typography>
                ) : (
                  <Stack spacing={0.25}>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>COA Required</Typography>
                    <Typography sx={{ fontSize: 12 }}>
                      This lot cannot be used until a current COA is available. Upload a COA to enable consumption.
                    </Typography>
                  </Stack>
                )}
              </Alert>
            ) : null}
          </>
        )}

        <Divider sx={{ mb: 2 }} />

        {/* ---- Document List ---- */}
        <Typography sx={{ fontWeight: 600, fontSize: 13, mb: 1.5 }}>Documents</Typography>
        <MaterialDocumentList
          materialId={material.id}
          isExpired={isExpired}
          refreshKey={refresh}
          onDocumentChanged={() => setRefresh((r) => r + 1)}
        />
      </FloatingDialog>

      {/* Upload sub-dialog */}
      <UploadMaterialDocumentDialog
        open={uploadOpen}
        materialId={material.id}
        onClose={() => setUploadOpen(false)}
        onSuccess={handleUploadSuccess}
      />
    </>
  );
}
