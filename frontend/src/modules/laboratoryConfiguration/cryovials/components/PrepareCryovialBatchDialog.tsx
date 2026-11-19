import { useState, useEffect } from "react";
import { useTranslation } from "../../../../i18n";
import {
  Button,
  TextField,
  Typography,
  Box,
  Alert,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  IconButton,
  Divider,
  Stack,
  Paper,
  Checkbox,
  FormControlLabel,
  useTheme,
  TableContainer
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import ScienceIcon from "@mui/icons-material/Science";
import AddIcon from "@mui/icons-material/Add";
import { PanelRow, PrepareCryovialPayload } from "../types/cryovialTypes";
import { CryovialService } from "../services/CryovialService";
import { MaterialService } from "../../../inventory/materials/services/MaterialService";
import { EquipmentInventoryService } from "../../../inventory/equipment/services/EquipmentInventoryService";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { useLoadFailures } from "../../../../hooks/useLoadFailures";
import { LoadFailuresAlert } from "../../../../components/LoadErrorAlert";
import { tableHeadSx } from "../../../../theme";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { DateField } from "../../../../components/DateField";

export interface CreatedCryovialRef {
  id: number;
  code: string;
  organismName?: string;
}

interface PrepareCryovialBatchDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: (message: string, created?: CreatedCryovialRef) => void;
}

const emptyRow = (): PanelRow => ({
  mediaId: "",
  incubatorEquipmentId: "",
  incubationStart: "",
  incubationEnd: "",
  observationText: ""
});

export function PrepareCryovialBatchDialog({
  open,
  onClose,
  onSuccess
}: PrepareCryovialBatchDialogProps) {
  const { t } = useTranslation("labConfig");
  const theme = useTheme();
  const [materials, setMaterials] = useState<any[]>([]);
  const [releasedMedia, setReleasedMedia] = useState<any[]>([]);
  const [incubators, setIncubators] = useState<any[]>([]);
  const [equipmentList, setEquipmentList] = useState<any[]>([]);
  const [equipmentLoading, setEquipmentLoading] = useState(false);
  const [form, setForm] = useState<Record<string, any>>({});
  const [panel, setPanel] = useState<PanelRow[]>([emptyRow()]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const { failed: failedLists, fail, reset: resetFailures } = useLoadFailures();

  useEffect(() => {
    if (open) {
      setError(null);
      setForm({});
      setPanel([emptyRow()]);
      resetFailures();
      // These three had no catch at all: a failure was an unhandled
      // rejection and left the pickers silently empty.
      MaterialService.getAll("LyophilizedMicroorganism").then(setMaterials).catch(fail("lyophilized microorganism stock", () => setMaterials([])));
      masterDataOptions.getReleasedMedia().then(setReleasedMedia).catch(fail("released media", () => setReleasedMedia([])));
      masterDataOptions.getEquipment("Incubator").then(setIncubators).catch(fail("incubators", () => setIncubators([])));
      setEquipmentLoading(true);
      EquipmentInventoryService.getAll()
        .then((data: any[]) => setEquipmentList(data || []))
        .catch(fail("storage equipment", () => setEquipmentList([])))
        .finally(() => setEquipmentLoading(false));
    }
  }, [open, fail, resetFailures]);

  const usableMaterials = materials.filter((m) => m.status === "InStock");
  const selectedMaterial = usableMaterials.find((m) => m.id === form.materialId);

  // Eligible storage equipment: Deep Freezer or Freezer in service
  const eligibleFreezers = equipmentList.filter((e) => {
    const type = (e.instrumentType || "").trim().toLowerCase();
    const isFreezer = type === "deep freezer" || type === "freezer" || type.includes("freezer");
    const isInService = e.status === "InService" || e.status === 0 || e.status === "0";
    return isFreezer && isInService;
  });

  const selectedStorageEquipment = eligibleFreezers.find(
    (e) => e.id === Number(form.storageEquipmentId)
  );

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));
  const updateRow = (i: number, k: keyof PanelRow, v: string) =>
    setPanel((p) => p.map((r, idx) => (idx === i ? { ...r, [k]: v } : r)));
  const addRow = () => setPanel((p) => [...p, emptyRow()]);
  const removeRow = (i: number) =>
    setPanel((p) => (p.length > 1 ? p.filter((_, idx) => idx !== i) : p));

  const validate = (): string | null => {
    if (!form.materialId) return t("cryovials.prepare.errors.material");
    if (!form.numberOfVialsPrepared || Number(form.numberOfVialsPrepared) <= 0)
      return t("cryovials.prepare.errors.vials");
    if (form.discsUsed === undefined || form.discsUsed === "" || Number(form.discsUsed) < 0)
      return t("cryovials.prepare.errors.discs");
    if (!form.expiryDate) return t("cryovials.prepare.errors.expiry");
    if (!form.storageEquipmentId) return t("cryovials.prepare.errors.storage");
    if (!form.physicalCheckConfirmed) return t("cryovials.prepare.errors.physical");
    if (panel.length === 0) return t("cryovials.prepare.errors.panelRequired");
    for (let i = 0; i < panel.length; i++) {
      const r = panel[i];
      if (!r.mediaId) return t("cryovials.prepare.errors.rowMedia", { row: i + 1 });
      if (!r.incubatorEquipmentId) return t("cryovials.prepare.errors.rowIncubator", { row: i + 1 });
      if (!r.incubationStart) return t("cryovials.prepare.errors.rowStart", { row: i + 1 });
      if (!r.incubationEnd) return t("cryovials.prepare.errors.rowEnd", { row: i + 1 });
    }
    return null;
  };

  const handleSave = async () => {
    const valError = validate();
    if (valError) {
      setError(valError);
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const storageConditionValue = selectedStorageEquipment
        ? `${selectedStorageEquipment.instrumentType}, ${selectedStorageEquipment.manufacturerName} (${selectedStorageEquipment.code})`
        : "";

      const payload: PrepareCryovialPayload = {
        materialId: Number(form.materialId),
        numberOfVialsPrepared: Number(form.numberOfVialsPrepared),
        expiryDate: form.expiryDate,
        storageCondition: storageConditionValue,
        physicalCheckConfirmed: Boolean(form.physicalCheckConfirmed),
        physicalCheckText: form.physicalCheckText || "",
        discsUsed: Number(form.discsUsed),
        panel: panel.map((r) => ({
          mediaId: Number(r.mediaId),
          incubatorEquipmentId: Number(r.incubatorEquipmentId),
          incubationStart: r.incubationStart,
          incubationEnd: r.incubationEnd,
          observationText: r.observationText || ""
        }))
      };

      const result = await CryovialService.prepare(payload);
      const code = result?.code ? ` (${result.code})` : "";
      const created: CreatedCryovialRef | undefined =
        result && typeof result.id === "number" && typeof result.code === "string" && result.code.trim().length > 0
          ? {
              id: result.id,
              code: result.code,
              organismName: result.organismNameSnapshot || result.organism?.scientificName
            }
          : undefined;
      onSuccess(t("cryovials.prepare.success", { code }), created);
      onClose();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? t("cryovials.prepare.errors.prepareFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      onClose={() => { if (!submitting) onClose(); }}
      maxWidth="md"
      paperSx={{ borderRadius: 2, p: 0.5 }}
      titleSx={{ pb: 1 }}
      title={
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Box
            sx={{
              width: 34,
              height: 34,
              borderRadius: 1.5,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              bgcolor: theme.custom.status.purple.bg,
              color: theme.palette.primary.main
            }}
          >
            <ScienceIcon fontSize="small" />
          </Box>
          <Typography sx={{ fontSize: 18, fontWeight: 700, color: "text.primary" }}>
            {t("cryovials.prepare.title")}
          </Typography>
        </Box>
      }
      actions={
        <>
          <Button onClick={onClose} disabled={submitting} sx={{ color: "text.secondary" }}>
            {t("cryovials.prepare.cancel")}
          </Button>
          <Button
            variant="contained"
            onClick={handleSave}
            disabled={submitting || eligibleFreezers.length === 0}
            color="primary"
          >
            {submitting ? t("cryovials.prepare.saving") : t("cryovials.prepare.save")}
          </Button>
        </>
      }
    >
      <Stack spacing={3}>
        <LoadFailuresAlert failed={failedLists} />
        {error && <Alert severity="error">{error}</Alert>}

        {/* SECTION 1: Source / Batch Information */}
        <Box>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5, textTransform: "uppercase", letterSpacing: "0.5px" }}>
            {t("cryovials.prepare.section1")}
          </Typography>

          <Box sx={{ mb: 2 }}>
            <FormControl fullWidth size="small">
              <InputLabel id="source-material-label">{t("cryovials.prepare.sourceLabel")}</InputLabel>
              <Select
                labelId="source-material-label"
                label={t("cryovials.prepare.sourceLabel")}
                value={form.materialId ?? ""}
                onChange={(e) => setField("materialId", e.target.value)}
              >
                <MenuItem value="">
                  <em>{t("cryovials.prepare.selectSource")}</em>
                </MenuItem>
                {usableMaterials.map((m) => (
                  <MenuItem key={m.id} value={m.id}>
                    {t("cryovials.prepare.sourceOption", { name: m.materialName, batch: m.batchNumber, remaining: m.quantityRemaining, unit: m.unit })}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            {selectedMaterial && (
              <Paper
                variant="outlined"
                sx={{
                  mt: 1,
                  p: 1.5,
                  bgcolor: "background.default",
                  borderColor: "divider",
                  borderRadius: 1
                }}
              >
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {t("cryovials.prepare.organismLabel")}{" "}
                  <span style={{ color: theme.palette.primary.main, fontWeight: 700 }}>
                    {selectedMaterial.organism?.scientificName ?? t("cryovials.prepare.organismNotSet")}
                  </span>
                  {selectedMaterial.organism?.atccNumber ? ` (ATCC ${selectedMaterial.organism.atccNumber})` : ""}
                  {selectedMaterial.referenceOrganismCode
                    ? t("cryovials.prepare.strain", {
                        code: selectedMaterial.referenceOrganismCode,
                        collection: selectedMaterial.cultureCollection,
                        number: selectedMaterial.strainNumber ? ` ${selectedMaterial.strainNumber}` : ""
                      })
                    : ""}
                  {" · "}
                  {t("cryovials.prepare.manufacturer", { name: selectedMaterial.manufacturerName || "—" })}
                </Typography>

                <Box sx={{ mt: 1.25, pt: 1, borderTop: "1px dashed", borderColor: "divider" }}>
                  <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase", letterSpacing: "0.3px", mb: 0.5 }}>
                    {t("cryovials.prepare.referenceHeading")}
                  </Typography>
                  {selectedMaterial.organism?.description ? (
                    <Typography sx={{ fontSize: 12, color: "text.primary", whiteSpace: "pre-wrap", bgcolor: "action.hover", p: 1, borderRadius: 0.5, border: "1px solid", borderColor: "divider" }}>
                      {selectedMaterial.organism.description}
                    </Typography>
                  ) : (
                    <Typography sx={{ fontSize: 12, color: "warning.main", fontStyle: "italic" }}>
                      {t("cryovials.prepare.noReference")}
                    </Typography>
                  )}
                </Box>
              </Paper>
            )}
          </Box>

          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "1fr",
                sm: "repeat(2, 1fr)",
                md: "repeat(3, 1fr)"
              },
              gap: 2
            }}
          >
            <TextField
              size="small"
              label={t("cryovials.prepare.vialsLabel")}
              type="number"
              placeholder={t("cryovials.prepare.vialsPlaceholder")}
              value={form.numberOfVialsPrepared ?? ""}
              onChange={(e) => setField("numberOfVialsPrepared", e.target.value)}
              required
              slotProps={{
                htmlInput: { min: 1 }
              }}
            />
            <TextField
              size="small"
              label={t("cryovials.prepare.discsLabel")}
              type="number"
              placeholder={t("cryovials.prepare.discsPlaceholder")}
              value={form.discsUsed ?? ""}
              onChange={(e) => setField("discsUsed", e.target.value)}
              required
              slotProps={{
                htmlInput: { min: 0 }
              }}
            />
            <DateField
              label={t("cryovials.prepare.expiryLabel")}
              value={form.expiryDate ?? ""}
              onChange={(v) => setField("expiryDate", v)}
              required
            />
            <FormControl size="small" required sx={{ gridColumn: { sm: "span 2", md: "span 3" } }}>
              <InputLabel id="storage-equipment-label">{t("cryovials.prepare.storageLabel")}</InputLabel>
              <Select
                labelId="storage-equipment-label"
                label={t("cryovials.prepare.storageLabel")}
                value={form.storageEquipmentId ?? ""}
                onChange={(e) => setField("storageEquipmentId", e.target.value)}
                disabled={equipmentLoading || eligibleFreezers.length === 0}
              >
                <MenuItem value="">
                  <em>{equipmentLoading ? t("cryovials.prepare.loadingEquipment") : t("cryovials.prepare.selectFreezer")}</em>
                </MenuItem>
                {eligibleFreezers.map((eq) => (
                  <MenuItem key={eq.id} value={eq.id}>
                    {eq.instrumentType}, {eq.manufacturerName} ({eq.code})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <Box sx={{ gridColumn: { xs: "1fr", sm: "span 2", md: "span 3" }, mt: 0.5 }}>
              <FormControlLabel
                control={
                  <Checkbox
                    checked={Boolean(form.physicalCheckConfirmed)}
                    onChange={(e) => setField("physicalCheckConfirmed", e.target.checked)}
                    color="primary"
                    size="small"
                  />
                }
                label={
                  <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                    {t("cryovials.prepare.physicalConfirm")}
                  </Typography>
                }
              />
            </Box>

            <TextField
              fullWidth
              size="small"
              label={t("cryovials.prepare.physicalNotes")}
              placeholder={t("cryovials.prepare.physicalPlaceholder")}
              value={form.physicalCheckText ?? ""}
              onChange={(e) => setField("physicalCheckText", e.target.value)}
              sx={{ gridColumn: { xs: "1fr", sm: "span 2", md: "span 3" } }}
            />
          </Box>

          {eligibleFreezers.length === 0 && !equipmentLoading && !failedLists.includes("storage equipment") && (
            <Alert severity="warning" sx={{ mt: 1.5 }}>
              {t("cryovials.prepare.noFreezer")}
            </Alert>
          )}

          {selectedStorageEquipment && (
            <Paper
              variant="outlined"
              sx={{
                mt: 1.5,
                p: 1.5,
                bgcolor: "background.default",
                borderColor: "divider",
                borderRadius: 1
              }}
            >
              <Box
                sx={{
                  display: "grid",
                  gridTemplateColumns: {
                    xs: "1fr",
                    sm: "repeat(2, 1fr)",
                    md: "repeat(4, 1fr)"
                  },
                  gap: 1.5
                }}
              >
                <Box>
                  <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                    {t("cryovials.prepare.storage.heading")}
                  </Typography>
                  <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main }}>
                    {selectedStorageEquipment.instrumentType}, {selectedStorageEquipment.manufacturerName || t("cryovials.prepare.storage.asset")}
                  </Typography>
                </Box>
                <Box>
                  <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                    {t("cryovials.prepare.storage.code")}
                  </Typography>
                  <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                    {selectedStorageEquipment.code}
                  </Typography>
                </Box>
                <Box>
                  <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                    {t("cryovials.prepare.storage.location")}
                  </Typography>
                  <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                    {selectedStorageEquipment.location || "—"}
                  </Typography>
                </Box>
                <Box>
                  <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                    {t("cryovials.prepare.storage.status")}
                  </Typography>
                  <Typography sx={{ fontSize: 13, fontWeight: 600, color: "success.main" }}>
                    {t("cryovials.prepare.storage.inService")}
                  </Typography>
                </Box>
              </Box>
            </Paper>
          )}
        </Box>

        <Divider />

        {/* SECTION 2: Identity Confirmation Panel */}
        <Box>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, textTransform: "uppercase", letterSpacing: "0.5px" }}>
              {t("cryovials.prepare.section2")}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              {t("cryovials.prepare.mandatory")}
            </Typography>
          </Box>

          <TableContainer>
            <Table size="small" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1, overflow: "hidden" }}>
              <TableHead sx={tableHeadSx}>
                <TableRow>
                  <TableCell sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>{t("cryovials.prepare.columns.media")}</TableCell>
                  <TableCell sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>{t("cryovials.prepare.columns.incubator")}</TableCell>
                  <TableCell sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>{t("cryovials.prepare.columns.start")}</TableCell>
                  <TableCell sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>{t("cryovials.prepare.columns.end")}</TableCell>
                  <TableCell sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>{t("cryovials.prepare.columns.observation")}</TableCell>
                  <TableCell sx={{ width: 40 }}></TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {panel.map((row, i) => (
                  <TableRow key={i}>
                    <TableCell sx={{ py: 1 }}>
                      <Select
                        size="small"
                        fullWidth
                        displayEmpty
                        value={row.mediaId}
                        onChange={(e) => updateRow(i, "mediaId", e.target.value)}
                        inputProps={{ "aria-label": t("cryovials.prepare.mediaAria") }}
                      >
                        <MenuItem value="">
                          <em>{t("cryovials.prepare.selectMedia")}</em>
                        </MenuItem>
                        {releasedMedia.map((m) => (
                          <MenuItem key={m.id} value={m.id}>
                            {m.lotNumber} ({m.material?.materialName || m.mediaType?.class || t("cryovials.prepare.mediaFallback")})
                          </MenuItem>
                        ))}
                      </Select>
                    </TableCell>
                    <TableCell sx={{ py: 1 }}>
                      <Select
                        size="small"
                        fullWidth
                        displayEmpty
                        value={row.incubatorEquipmentId}
                        onChange={(e) => updateRow(i, "incubatorEquipmentId", e.target.value)}
                        inputProps={{ "aria-label": t("cryovials.prepare.incubatorAria") }}
                      >
                        <MenuItem value="">
                          <em>{t("cryovials.prepare.selectIncubator")}</em>
                        </MenuItem>
                        {incubators.map((i2) => (
                          <MenuItem key={i2.id} value={i2.id}>
                            {i2.name} ({i2.code})
                          </MenuItem>
                        ))}
                      </Select>
                    </TableCell>
                    <TableCell sx={{ py: 1 }}>
                      <DateField
                        value={row.incubationStart}
                        onChange={(v) => updateRow(i, "incubationStart", v)}
                        fullWidth
                        ariaLabel={t("cryovials.prepare.startAria", { row: i + 1 })}
                      />
                    </TableCell>
                    <TableCell sx={{ py: 1 }}>
                      <DateField
                        value={row.incubationEnd}
                        onChange={(v) => updateRow(i, "incubationEnd", v)}
                        fullWidth
                        ariaLabel={t("cryovials.prepare.endAria", { row: i + 1 })}
                      />
                    </TableCell>
                    <TableCell sx={{ py: 1 }}>
                      <TextField
                        size="small"
                        placeholder={t("cryovials.prepare.observationPlaceholder")}
                        value={row.observationText}
                        onChange={(e) => updateRow(i, "observationText", e.target.value)}
                        fullWidth
                        slotProps={{ htmlInput: { "aria-label": t("cryovials.prepare.observationAria", { row: i + 1 }) } }}
                      />
                    </TableCell>
                    <TableCell sx={{ py: 1 }}>
                      <IconButton aria-label={t("cryovials.prepare.removeRow", { row: i + 1 })}
                        size="small"
                        onClick={() => removeRow(i)}
                        disabled={panel.length <= 1}
                        sx={{ color: panel.length <= 1 ? "text.disabled" : "error.main" }}
                      >
                        <CloseIcon fontSize="small" />
                      </IconButton>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>

          <Button
            size="small"
            onClick={addRow}
            startIcon={<AddIcon fontSize="small" />}
            sx={{ mt: 1.5, color: theme.palette.primary.main }}
          >
            {t("cryovials.prepare.addRow")}
          </Button>
        </Box>
      </Stack>
    </FloatingDialog>
  );
}
