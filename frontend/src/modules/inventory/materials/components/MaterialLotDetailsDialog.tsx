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
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { MaterialService } from "../services/MaterialService";
import { formatLabCalendarDate } from "../../../../utils/formatDate";
import type { MaterialItem, CoeEligibilityResult } from "../types/materialTypes";
import { COA_REQUIRED_TYPES } from "../types/materialTypes";
import { MaterialDocumentList } from "./MaterialDocumentList";
import { UploadMaterialDocumentDialog } from "./UploadMaterialDocumentDialog";
import { LotPreparationsList } from "./LotPreparationsList";
import { PreparationWhereUsedPanel } from "../../../testPreparation/components/PreparationWhereUsedPanel";
import { PrintLabelButton } from "../../../../components/labels/PrintLabelButton";
import { useTrackRecent } from "../../../navigation/useTrackRecent";

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
  const { t } = useTranslation("inventory");
  useTrackRecent("material", open ? material?.id : null);
  const [eligibility, setEligibility] = useState<CoeEligibilityResult | null>(null);
  const [eligibilityLoading, setEligibilityLoading] = useState(false);
  // A failed check used to show no banner at all - indistinguishable from a
  // lot that needs no COA.
  const [eligibilityFailed, setEligibilityFailed] = useState(false);
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
    setEligibilityFailed(false);
    MaterialService.getCOAEligibility(material.id)
      .then(setEligibility)
      .catch(() => {
        setEligibility(null);
        setEligibilityFailed(true);
      })
      .finally(() => setEligibilityLoading(false));
  }, [open, material, coaRequired, refresh]);

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
        title={t("materials.lot.title")}
        onClose={onClose}
        actions={
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <PrintLabelButton
              target={{
                entityType: "MaterialStockLot",
                entityId: material.id,
                code: material.stockId ?? "",
                displayName: material.materialName || material.stockId || "Stock Lot"
              }}
            />
            <Button
              id="lot-details-upload-btn"
              variant="contained"
              size="small"
              onClick={() => setUploadOpen(true)}
            >
              {t("materials.lot.upload")}
            </Button>
          </Stack>
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
          <MetaRow label={t("materials.lot.manufacturer")} value={material.manufacturerName || "—"} />
          <MetaRow label={t("materials.lot.stockId")} value={<span style={{ fontFamily: "monospace", fontWeight: 700 }}>{material.stockId ?? "—"}</span>} />
          <MetaRow label={t("materials.lot.batchLot")} value={<span style={{ fontFamily: "monospace", fontWeight: 700 }}>{material.batchNumber}</span>} />
          <MetaRow label={t("materials.lot.received")} value={formatLabCalendarDate(material.receivingDate)} />
          <MetaRow label={t("materials.lot.expiry")} value={material.expiryDate ? formatLabCalendarDate(material.expiryDate) : t("materials.lot.none")} />
          {material.materialType === "ReferenceStandard" && (
            <MetaRow label={t("materials.lot.purity")} value={material.purity != null ? `${material.purity}%` : "—"} />
          )}
          <MetaRow label={t("materials.lot.quantity")} value={`${material.quantityRemaining} / ${material.quantityReceived} ${material.unit}`} />
          <MetaRow
            label={t("materials.lot.status")}
            value={
              <Chip
                label={isExpired ? t("status.Expired") : material.status === "Depleted" ? t("status.Depleted") : t("status.InStock")}
                size="small"
                color={isExpired ? "error" : material.status === "Depleted" ? "default" : "success"}
                variant="outlined"
                sx={{ fontSize: 12 }}
              />
            }
          />
          {isExpired && (
            <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5, fontStyle: "italic" }}>
              {t("materials.lot.historical")}
            </Typography>
          )}
        </Box>

        {/* ---- COA Eligibility Banner ---- */}
        {coaRequired && (
          <>
            {eligibilityLoading ? (
              <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2 }}>
                <CircularProgress size={14} />
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("materials.lot.checkingCoa")}</Typography>
              </Box>
            ) : eligibilityFailed ? (
              <Alert severity="error" sx={{ mb: 2 }}>
                {t("materials.lot.coaFailed")}
              </Alert>
            ) : eligibility ? (
              <Alert
                id="lot-coa-eligibility-banner"
                severity={eligibility.hasCurrentCoa ? "success" : "warning"}
                icon={eligibility.hasCurrentCoa ? <CheckCircleOutlineIcon fontSize="small" /> : <InfoOutlinedIcon fontSize="small" />}
                sx={{ mb: 2, py: 0.5 }}
              >
                {eligibility.hasCurrentCoa ? (
                  <Typography sx={{ fontSize: 12 }}>
                    <Trans i18nKey="materials.lot.coaSatisfied" ns="inventory" components={{ b: <strong /> }} />
                  </Typography>
                ) : (
                  <Stack spacing={0.25}>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{t("materials.lot.coaRequired")}</Typography>
                    <Typography sx={{ fontSize: 12 }}>
                      {t("materials.lot.coaCannotUse")}
                    </Typography>
                  </Stack>
                )}
              </Alert>
            ) : null}
          </>
        )}

        <Divider sx={{ mb: 2 }} />

        {/* ---- Document List ---- */}
        <Typography sx={{ fontWeight: 600, fontSize: 13, mb: 1.5 }}>{t("materials.lot.documents")}</Typography>
        <MaterialDocumentList
          materialId={material.id}
          isExpired={isExpired}
          refreshKey={refresh}
          onDocumentChanged={() => setRefresh((r) => r + 1)}
        />

        <Divider sx={{ my: 2 }} />

        {/* ---- Consumed by Preparations ---- */}
        <LotPreparationsList materialId={material.id} onNavigate={onClose} />

        <Divider sx={{ my: 2 }} />

        {/* ---- Used in test preparation (diluent / neutralizer) ---- */}
        <PreparationWhereUsedPanel lot={{ materialId: material.id }} />
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
