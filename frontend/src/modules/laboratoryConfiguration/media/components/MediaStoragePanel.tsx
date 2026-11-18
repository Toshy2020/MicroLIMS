import { useCallback, useEffect, useState } from "react";
import type { TFunction } from "i18next";
import { Trans } from "react-i18next";
import {
  Alert, Box, Button, FormControl, InputLabel, MenuItem, Select, Stack, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, TextField, Typography
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { useTranslation } from "../../../../i18n";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import {
  EMPTY_STORAGE_LOCATION,
  StorageLocationPicker,
  StorageLocationValue,
  isStorageLocationValid,
  storageLocationPayload
} from "../../../../components/StorageLocationPicker";
import { tableHeadSx } from "../../../../theme";
import { MediaPreparationService } from "../services/MediaPreparationService";

function getForms(t: TFunction<"labConfig">) {
  return [
    { value: "Plates", label: t("media.storagePanel.forms.plates") },
    { value: "AgarBottles", label: t("media.storagePanel.forms.agarBottles") },
    { value: "BrothBottles", label: t("media.storagePanel.forms.brothBottles") },
    { value: "Tubes", label: t("media.storagePanel.forms.tubes") }
  ];
}
const formLabel = (t: TFunction<"labConfig">, v: string) => getForms(t).find((f) => f.value === v)?.label ?? v;

interface Entry {
  id: number;
  form: string;
  quantity: number;
  equipmentCode?: string | null;
  equipmentName?: string | null;
  equipmentLocation?: string | null;
  locationText?: string | null;
}

// equipmentName arrives as "Type - Manufacturer" (sometimes with stray spaces).
const placeOf = (e: Entry) =>
  e.equipmentCode
    ? [(e.equipmentName ?? "").replace(/\s+-\s+/g, " ").replace(/\s+/g, " ").trim(), e.equipmentCode, e.equipmentLocation].filter(Boolean).join(" · ")
    : e.locationText ?? "—";

// Where a released lot physically is. One lot can be held in several forms
// and places at once (plates in a refrigerator, bottles in the media store).
export function MediaStoragePanel({ lot }: { lot: any }) {
  const { t } = useTranslation("labConfig");
  const [entries, setEntries] = useState<Entry[]>([]);
  const [loadError, setLoadError] = useState(false);
  const [adding, setAdding] = useState(false);
  const [moving, setMoving] = useState<Entry | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const load = useCallback(() => {
    setLoadError(false);
    MediaPreparationService.getStorage(lot.id).then(setEntries).catch(() => setLoadError(true));
  }, [lot.id]);

  useEffect(load, [load]);

  const canStore = lot.isReleasedForUse && lot.status === "Active";
  const done = (text: string) => {
    setAdding(false);
    setMoving(null);
    setMessage(text);
    load();
  };

  return (
    <Stack spacing={2} sx={{ pt: 1 }}>
      {message && <Alert severity="success" onClose={() => setMessage(null)}>{message}</Alert>}
      {loadError && <Alert severity="error">{t("media.storagePanel.loadError")}</Alert>}

      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Typography sx={{ fontSize: 12, fontWeight: 700, color: "primary.main" }}>
          {t("media.storagePanel.title")}
        </Typography>
        {canStore && (
          <Button size="small" variant="outlined" startIcon={<AddIcon />} onClick={() => setAdding(true)}>
            {t("media.storagePanel.addStorage")}
          </Button>
        )}
      </Box>

      {!canStore && <Alert severity="info">{t("media.storagePanel.notStorable")}</Alert>}

      {entries.length === 0 && canStore && !loadError ? (
        <Alert severity="info">{t("media.storagePanel.empty")}</Alert>
      ) : (
        entries.length > 0 && (
          <TableContainer>
            <Table size="small" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
              <TableHead>
                <TableRow sx={tableHeadSx}>
                  <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>{t("media.storagePanel.columns.form")}</TableCell>
                  <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>{t("media.storagePanel.columns.quantity")}</TableCell>
                  <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>{t("media.storagePanel.columns.location")}</TableCell>
                  <TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {entries.map((e) => (
                  <TableRow key={e.id}>
                    <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>{formLabel(t, e.form)}</TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{e.quantity}</TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{placeOf(e)}</TableCell>
                    <TableCell align="right">
                      {canStore && (
                        <Button size="small" onClick={() => setMoving(e)}>
                          {t("media.storagePanel.move")}
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )
      )}

      {adding && (
        <StorageDialog
          lot={lot}
          onClose={() => setAdding(false)}
          onDone={() => done(t("media.storagePanel.storageAdded"))}
        />
      )}
      {moving && (
        <StorageDialog
          lot={lot}
          entry={moving}
          onClose={() => setMoving(null)}
          onDone={() => done(t("media.storagePanel.storageMoved"))}
        />
      )}
    </Stack>
  );
}

// Add a new entry, or (with `entry`) move some/all of an existing one.
function StorageDialog({ lot, entry, onClose, onDone }: { lot: any; entry?: Entry; onClose: () => void; onDone: () => void }) {
  const { t } = useTranslation("labConfig");
  const forms = getForms(t);
  const [form, setForm] = useState(entry?.form ?? "Plates");
  const [quantity, setQuantity] = useState(entry ? String(entry.quantity) : "");
  const [location, setLocation] = useState<StorageLocationValue>(EMPTY_STORAGE_LOCATION);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const qty = Number(quantity);
  const qtyOk = Number.isInteger(qty) && qty > 0 && (!entry || qty <= entry.quantity);
  const valid = qtyOk && isStorageLocationValid(location) && (!entry || reason.trim().length >= 5);

  const save = async () => {
    setSubmitting(true);
    setError(null);
    try {
      const where = storageLocationPayload(location);
      if (entry) await MediaPreparationService.moveStorage(lot.id, entry.id, { quantity: qty, ...where, reason: reason.trim() });
      else await MediaPreparationService.addStorage(lot.id, { form, quantity: qty, ...where });
      onDone();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? t("media.storagePanel.saveError"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FloatingDialog
      open
      maxWidth="sm"
      onClose={() => { if (!submitting) onClose(); }}
      title={entry ? t("media.storagePanel.moveTitle") : t("media.storagePanel.addTitle")}
      actions={
        <>
          <Button onClick={onClose} disabled={submitting}>
            {t("media.storagePanel.cancel")}
          </Button>
          <Button variant="contained" onClick={save} disabled={!valid || submitting}>
            {submitting
              ? t("media.storagePanel.saving")
              : entry
              ? t("media.storagePanel.move")
              : t("media.storagePanel.add")}
          </Button>
        </>
      }
    >
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        <Typography sx={{ fontSize: 13 }}>
          <Trans
            i18nKey="media.storagePanel.lotPrefix"
            ns="labConfig"
            values={{ lotNumber: lot.lotNumber }}
            components={{ bold: <b /> }}
          />
          {entry &&
            t("media.storagePanel.movingFrom", {
              form: formLabel(t, entry.form),
              place: placeOf(entry),
              quantity: entry.quantity
            })}
        </Typography>
        {!entry && (
          <FormControl size="small" fullWidth>
            <InputLabel id="storage-form">{t("media.storagePanel.storedAsLabel")}</InputLabel>
            <Select
              labelId="storage-form"
              label={t("media.storagePanel.storedAsLabel")}
              value={form}
              onChange={(e) => setForm(e.target.value)}
            >
              {forms.map((f) => <MenuItem key={f.value} value={f.value}>{f.label}</MenuItem>)}
            </Select>
          </FormControl>
        )}
        <TextField
          size="small"
          label={entry ? t("media.storagePanel.quantityToMoveLabel") : t("media.storagePanel.quantityLabel")}
          type="number"
          value={quantity}
          onChange={(e) => setQuantity(e.target.value)}
          error={quantity !== "" && !qtyOk}
          helperText={entry ? t("media.storagePanel.moveHelper", { max: entry.quantity }) : undefined}
          slotProps={{ htmlInput: { min: 1, step: 1 } }}
        />
        <StorageLocationPicker value={location} onChange={setLocation} disabled={submitting} />
        {entry && (
          <TextField
            size="small"
            fullWidth
            multiline
            rows={2}
            label={t("media.storagePanel.reasonLabel")}
            value={reason}
            onChange={(e) => setReason(e.target.value.slice(0, 500))}
            error={reason.length > 0 && reason.trim().length < 5}
            helperText={t("media.storagePanel.reasonHelper")}
          />
        )}
      </Stack>
    </FloatingDialog>
  );
}
