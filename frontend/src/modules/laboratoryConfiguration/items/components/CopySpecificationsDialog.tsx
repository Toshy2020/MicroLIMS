import React, { useEffect, useMemo, useRef, useState } from "react";
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Checkbox,
  CircularProgress,
  FormControlLabel,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import { FormDialog } from "../../../../components/lab/FormDialog";
import { LoadErrorAlert } from "../../../../components/LoadErrorAlert";
import { ToneBadge } from "../../../../components/StatusBadge";
import { Item, ItemService } from "../services/ItemService";
import {
  SpecificationService,
  type CopySpecificationsPreviewRow,
  type CopySpecificationsPreviewStatus,
  type SpecificationDto
} from "../../specifications/services/SpecificationService";
import type { TestDefinitionSummary } from "../specWizard/specHelpers";
import { limitSummary, specQualifiers } from "./copySpecifications";

interface CopySpecificationsDialogProps {
  // The item the specifications are copied TO.
  item: Item;
  testDefinitionByCode: Record<string, TestDefinitionSummary>;
  onClose: () => void;
  // Called after specifications were created so the list can refresh.
  onSpecsCreated: () => void;
  // Called once copying completes, before the dialog closes.
  onCompleted: (createdCount: number) => void;
}

const errorMessage = (err: unknown, fallback: string): string => {
  const e = err as { response?: { data?: { message?: string } }; message?: string } | null;
  return e?.response?.data?.message || e?.message || fallback;
};

const statusBadge = (status: CopySpecificationsPreviewStatus) => {
  switch (status) {
    case "Ok":
      return <ToneBadge tone="notDetected" label="Ok" />;
    case "AlreadyPresent":
      return <ToneBadge tone="inconclusive" label="Already present" />;
    case "Invalid":
      return <ToneBadge tone="detected" label="Invalid" />;
  }
};

export const CopySpecificationsDialog: React.FC<CopySpecificationsDialogProps> = ({
  item,
  testDefinitionByCode,
  onClose,
  onSpecsCreated,
  onCompleted
}) => {
  const [items, setItems] = useState<Item[] | null>(null);
  const [itemsError, setItemsError] = useState<string | null>(null);
  const [itemsAttempt, setItemsAttempt] = useState(0);
  const [source, setSource] = useState<Item | null>(null);

  const [previewRows, setPreviewRows] = useState<CopySpecificationsPreviewRow[] | null>(null);
  const [sourceSpecsById, setSourceSpecsById] = useState<ReadonlyMap<number, SpecificationDto>>(new Map());
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [previewAttempt, setPreviewAttempt] = useState(0);

  const [tickedIds, setTickedIds] = useState<ReadonlySet<number>>(new Set());
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    setItemsError(null);
    setItems(null);
    ItemService.getAll()
      .then((all) => {
        if (!cancelled) setItems(all);
      })
      .catch((err: unknown) => {
        if (!cancelled) setItemsError(errorMessage(err, "The items could not be loaded."));
      });
    return () => {
      cancelled = true;
    };
  }, [itemsAttempt]);

  useEffect(() => {
    if (!source) return;
    let cancelled = false;
    setPreviewLoading(true);
    setPreviewError(null);
    setPreviewRows(null);
    setSourceSpecsById(new Map());
    setTickedIds(new Set());
    setSaveError(null);

    Promise.all([
      SpecificationService.previewCopy({
        sourceItemId: source.id,
        targetItemId: item.id,
        specificationIds: []
      }),
      SpecificationService.getForItem(source.id).catch(() => [] as SpecificationDto[])
    ])
      .then(([rows, specs]) => {
        if (cancelled) return;
        const specMap = new Map<number, SpecificationDto>();
        for (const s of specs) {
          if (s.id != null) specMap.set(s.id, s);
        }
        setSourceSpecsById(specMap);
        setPreviewRows(rows);
        setTickedIds(new Set(rows.filter((r) => r.status === "Ok").map((r) => r.sourceSpecificationId)));
      })
      .catch((err: unknown) => {
        if (!cancelled) setPreviewError(errorMessage(err, "The preview could not be loaded."));
      })
      .finally(() => {
        if (!cancelled) setPreviewLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [source, item.id, previewAttempt]);

  const pickSource = (picked: Item | null) => {
    setSource(picked);
    setPreviewRows(null);
    setPreviewError(null);
    setSourceSpecsById(new Map());
    setTickedIds(new Set());
    setSaveError(null);
  };

  const toggle = (specId: number) =>
    setTickedIds((prev) => {
      const next = new Set(prev);
      if (next.has(specId)) next.delete(specId);
      else next.add(specId);
      return next;
    });

  const orderedTickedIds = useMemo(
    () => (previewRows ?? []).filter((r) => tickedIds.has(r.sourceSpecificationId)).map((r) => r.sourceSpecificationId),
    [previewRows, tickedIds]
  );

  const hasTickedInvalid = useMemo(
    () => (previewRows ?? []).some((r) => tickedIds.has(r.sourceSpecificationId) && r.status === "Invalid"),
    [previewRows, tickedIds]
  );

  const rowLabel = (row: CopySpecificationsPreviewRow) => {
    const def = testDefinitionByCode[row.testCode];
    const testName = item.assignedTests.find((t) => t.testCode === row.testCode)?.displayName || def?.displayName || row.testCode;
    return row.parameterName && row.parameterName !== testName ? `${testName} – ${row.parameterName}` : testName;
  };

  const handlePreviewSelection = async () => {
    if (!source || orderedTickedIds.length === 0 || previewLoading || saving) return;
    setPreviewLoading(true);
    setPreviewError(null);
    setSaveError(null);
    try {
      const updated = await SpecificationService.previewCopy({
        sourceItemId: source.id,
        targetItemId: item.id,
        specificationIds: orderedTickedIds
      });
      if (!mountedRef.current) return;
      const byId = new Map(updated.map((r) => [r.sourceSpecificationId, r] as const));
      setPreviewRows((prev) => (prev ? prev.map((row) => byId.get(row.sourceSpecificationId) ?? row) : updated));
    } catch (err: unknown) {
      if (mountedRef.current) setPreviewError(errorMessage(err, "The preview could not be loaded."));
    } finally {
      if (mountedRef.current) setPreviewLoading(false);
    }
  };

  const handleSave = async () => {
    if (!source || orderedTickedIds.length === 0 || hasTickedInvalid || saving) return;
    setSaving(true);
    setSaveError(null);
    try {
      const createdCount = await SpecificationService.copySpecifications({
        sourceItemId: source.id,
        targetItemId: item.id,
        specificationIds: orderedTickedIds
      });
      if (!mountedRef.current) return;
      onSpecsCreated();
      onCompleted(createdCount);
      onClose();
    } catch (err: unknown) {
      if (mountedRef.current) {
        setSaveError(errorMessage(err, "The specifications could not be copied."));
      }
    } finally {
      if (mountedRef.current) setSaving(false);
    }
  };

  const handleClose = () => {
    if (saving) return;
    onClose();
  };

  return (
    <FormDialog
      open
      title={`Copy specifications to ${item.name} (${item.code})`}
      onClose={handleClose}
      onSubmit={handleSave}
      submitLabel="Save"
      submitting={saving}
      submitDisabled={orderedTickedIds.length === 0 || hasTickedInvalid || previewLoading || previewRows === null}
      error={saveError}
      maxWidth="md"
    >
      <Typography variant="body2" color="text.secondary">
        Limits are copied exactly as they are on the source item. Parameters already present on this item are skipped,
        and invalid rows must be unticked before saving. Each created specification is recorded in the audit trail.
      </Typography>

      {itemsError ? (
        <LoadErrorAlert message={`Items could not be loaded: ${itemsError}`} onRetry={() => setItemsAttempt((n) => n + 1)} />
      ) : (
        <Autocomplete
          options={(items ?? []).filter((i) => i.id !== item.id)}
          value={source}
          loading={items == null}
          disabled={saving}
          onChange={(_, value) => pickSource(value)}
          getOptionLabel={(o) => `${o.name} (${o.code})`}
          isOptionEqualToValue={(a, b) => a.id === b.id}
          renderInput={(params) => <TextField {...params} size="small" label="Copy from item" />}
        />
      )}

      {source && previewError && (
        <LoadErrorAlert
          message={`The preview for ${source.name} could not be loaded: ${previewError}`}
          onRetry={() => setPreviewAttempt((n) => n + 1)}
        />
      )}

      {source && previewLoading && (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }} role="status" aria-live="polite">
          <CircularProgress size={16} />
          <Typography variant="body2">Loading preview</Typography>
        </Stack>
      )}

      {source && !previewError && previewRows && (
        <Box>
          {previewRows.length === 0 ? (
            <Alert severity="info">Nothing on {source.name} can be copied to {item.name}.</Alert>
          ) : (
            <Stack spacing={1}>
              <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
                <Typography variant="caption" color="text.secondary">
                  {orderedTickedIds.length} of {previewRows.length} selected
                </Typography>
                <Button
                  type="button"
                  size="small"
                  variant="outlined"
                  onClick={handlePreviewSelection}
                  disabled={orderedTickedIds.length === 0 || previewLoading || saving}
                  sx={{ textTransform: "none" }}
                >
                  Preview
                </Button>
              </Stack>

              {hasTickedInvalid && (
                <Alert severity="warning" role="status">
                  One or more ticked specifications are invalid for {item.name}. Untick invalid rows before saving.
                </Alert>
              )}

              <Stack spacing={0.75} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1, p: 1 }}>
                {previewRows.map((row) => {
                  const spec = sourceSpecsById.get(row.sourceSpecificationId);
                  const qualifiers = spec ? specQualifiers(spec) : [];
                  const isAlreadyPresent = row.status === "AlreadyPresent";
                  const label = rowLabel(row);
                  return (
                    <Box key={row.sourceSpecificationId} sx={{ opacity: isAlreadyPresent ? 0.75 : 1 }}>
                      <FormControlLabel
                        sx={{ alignItems: "flex-start", m: 0, width: "100%" }}
                        control={
                          <Checkbox
                            size="small"
                            checked={tickedIds.has(row.sourceSpecificationId)}
                            disabled={saving || isAlreadyPresent}
                            onChange={() => toggle(row.sourceSpecificationId)}
                            slotProps={{ input: { "aria-label": `Copy ${label}` } }}
                          />
                        }
                        label={
                          <Box sx={{ width: "100%" }}>
                            <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
                              <Typography sx={{ fontWeight: 600, fontSize: 13 }}>
                                {label}{" "}
                                <Typography component="span" sx={{ fontSize: 12, color: "text.secondary" }}>
                                  {row.testCode}
                                </Typography>
                              </Typography>
                              {statusBadge(row.status)}
                            </Stack>
                            {spec && <Typography sx={{ fontSize: 13 }}>{limitSummary(spec)}</Typography>}
                            {qualifiers.length > 0 && (
                              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                                {qualifiers.join(" · ")}
                              </Typography>
                            )}
                            {row.message && (
                              <Typography
                                sx={{
                                  fontSize: 12,
                                  color: row.status === "Invalid" ? "error.main" : "text.secondary"
                                }}
                              >
                                {row.message}
                              </Typography>
                            )}
                          </Box>
                        }
                      />
                    </Box>
                  );
                })}
              </Stack>
            </Stack>
          )}
        </Box>
      )}
    </FormDialog>
  );
};
