import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Alert,
  Autocomplete,
  Box,
  Checkbox,
  CircularProgress,
  FormControlLabel,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import { FormDialog } from "../../../../components/lab/FormDialog";
import { LoadErrorAlert } from "../../../../components/LoadErrorAlert";
import { useMyLabs } from "../../../../hooks/useMyLabs";
import { useLaboratorySections } from "../../../../hooks/useLaboratorySections";
import { Item, ItemService } from "../services/ItemService";
import { SpecificationDto, SpecificationService } from "../../specifications/services/SpecificationService";
import type { TestDefinitionSummary } from "./SpecificationParameterDialog";
import {
  CopyCandidate,
  assignDisplayOrders,
  isCannotCopy,
  limitSummary,
  ownLabTestCodes,
  selectCopyCandidates,
  specQualifiers,
  toCreatePayload
} from "./copySpecifications";

interface CopySpecificationsDialogProps {
  // The item the specifications are copied TO.
  item: Item;
  testDefinitionByCode: Record<string, TestDefinitionSummary>;
  onClose: () => void;
  // Called after any specification was created (also after a partial run) so the list can refresh.
  onSpecsCreated: () => void;
  // Called once every selected row was created, before the dialog closes.
  onCompleted: (createdCount: number) => void;
}

interface SourceRows {
  sourceId: number;
  sourceSpecs: SpecificationDto[];
  // Read fresh from the server when the source is picked: a test that already has limits is never offered.
  targetSpecs: SpecificationDto[];
}

const errorMessage = (err: unknown, fallback: string): string => {
  const e = err as { response?: { data?: { message?: string } }; message?: string } | null;
  return e?.response?.data?.message || e?.message || fallback;
};

const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

export const CopySpecificationsDialog: React.FC<CopySpecificationsDialogProps> = ({
  item,
  testDefinitionByCode,
  onClose,
  onSpecsCreated,
  onCompleted
}) => {
  const { codes: myLabCodes, loading: labsLoading } = useMyLabs();
  const { sections, loading: sectionsLoading } = useLaboratorySections();
  const myLabSectionIds = useMemo(
    () => new Set(sections.filter((s) => myLabCodes.includes(s.sectionCode)).map((s) => s.sectionId)),
    [sections, myLabCodes]
  );

  // Source item picker
  const [items, setItems] = useState<Item[] | null>(null);
  const [itemsError, setItemsError] = useState<string | null>(null);
  const [itemsAttempt, setItemsAttempt] = useState(0);
  const [source, setSource] = useState<Item | null>(null);

  // The source's rows (and the target's current rows)
  const [rows, setRows] = useState<SourceRows | null>(null);
  const [rowsError, setRowsError] = useState<string | null>(null);
  const [rowsAttempt, setRowsAttempt] = useState(0);

  // Review list state. Rows are ticked unless listed here.
  const [unticked, setUnticked] = useState<ReadonlySet<string>>(new Set());
  const [done, setDone] = useState<ReadonlySet<string>>(new Set());
  const [failure, setFailure] = useState<{ key: string; message: string } | null>(null);
  const [progress, setProgress] = useState<{ current: number; total: number } | null>(null);
  const running = progress != null;
  const runningRef = useRef(false);
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
    setRows(null);
    setRowsError(null);
    Promise.all([SpecificationService.getForItem(source.id), SpecificationService.getForItem(item.id)])
      .then(([sourceSpecs, targetSpecs]) => {
        if (!cancelled) setRows({ sourceId: source.id, sourceSpecs, targetSpecs });
      })
      .catch((err: unknown) => {
        if (!cancelled) setRowsError(errorMessage(err, "The specifications could not be loaded."));
      });
    return () => {
      cancelled = true;
    };
  }, [source, item.id, rowsAttempt]);

  const pickSource = (picked: Item | null) => {
    setSource(picked);
    setRows(null);
    setRowsError(null);
    setUnticked(new Set());
    setDone(new Set());
    setFailure(null);
  };

  const definitionsLoaded = Object.keys(testDefinitionByCode).length > 0;
  const labsReady = !labsLoading && !sectionsLoading;

  const selection = useMemo(() => {
    if (!source || !rows || rows.sourceId !== source.id || !definitionsLoaded || !labsReady) return null;
    return selectCopyCandidates({
      sourceItem: source,
      targetItem: item,
      sourceSpecs: rows.sourceSpecs,
      targetSpecs: rows.targetSpecs,
      ownLabTestCodes: ownLabTestCodes(item.assignedTests ?? [], testDefinitionByCode, myLabSectionIds, item.category)
    });
  }, [source, rows, item, testDefinitionByCode, myLabSectionIds, definitionsLoaded, labsReady]);

  const candidates = selection?.candidates ?? [];
  const isSelected = useCallback(
    (c: CopyCandidate) => c.cannotCopy == null && !unticked.has(c.key),
    [unticked]
  );
  const selected = candidates.filter(isSelected);
  const pending = selected.filter((c) => !done.has(c.key));

  const toggle = (key: string) =>
    setUnticked((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });

  const handleClose = () => {
    if (runningRef.current) return;
    onClose();
  };

  const rowLabel = (c: CopyCandidate) => {
    const def = testDefinitionByCode[c.spec.testCode];
    const testName = item.assignedTests.find((t) => t.testCode === c.spec.testCode)?.displayName || def?.displayName || c.spec.testCode;
    return c.spec.parameterName && c.spec.parameterName !== testName ? `${testName} – ${c.spec.parameterName}` : testName;
  };

  const handleCopy = async () => {
    if (!rows || runningRef.current || pending.length === 0) return;
    runningRef.current = true;
    setFailure(null);
    // Orders are worked out over every selected row, those already created included,
    // so a retry continues the sequence the first attempt started.
    const orders = assignDisplayOrders(selected.map((c) => c.spec), rows.targetSpecs);
    const orderByKey = new Map(selected.map((c, i) => [c.key, orders[i]] as const));
    const doneBefore = done.size;
    const createdNow: string[] = [];
    let failed = false;
    try {
      for (let i = 0; i < pending.length; i++) {
        const candidate = pending[i];
        if (!mountedRef.current) break;
        setProgress({ current: i + 1, total: pending.length });
        const payload = toCreatePayload(candidate.spec, item.id, orderByKey.get(candidate.key) ?? 0);
        if (isCannotCopy(payload)) {
          failed = true;
          setFailure({ key: candidate.key, message: payload.cannotCopy });
          break;
        }
        try {
          await SpecificationService.create(payload);
        } catch (err: unknown) {
          failed = true;
          if (mountedRef.current) {
            setFailure({ key: candidate.key, message: errorMessage(err, "The specification could not be created.") });
          }
          break;
        }
        createdNow.push(candidate.key);
        if (mountedRef.current) setDone((prev) => new Set(prev).add(candidate.key));
      }
    } finally {
      runningRef.current = false;
      if (mountedRef.current) setProgress(null);
    }
    if (createdNow.length > 0) onSpecsCreated();
    if (!failed && createdNow.length === pending.length) {
      onCompleted(doneBefore + createdNow.length);
      onClose();
    }
  };

  const failedCandidate = failure ? candidates.find((c) => c.key === failure.key) : undefined;
  const formError = failure && failedCandidate
    ? `Copying stopped at "${rowLabel(failedCandidate)}" (${failedCandidate.spec.testCode}): ${failure.message}${done.size > 0 ? ` ${plural(done.size, "parameter was", "parameters were")} already created and will not be sent again.` : ""}`
    : null;

  const skipped = selection?.skipped;
  const skippedLines = skipped
    ? [
        skipped.notAssigned > 0 && `${plural(skipped.notAssigned, "parameter is", "parameters are")} not assigned to this item`,
        skipped.alreadyHasLimits > 0 && `${plural(skipped.alreadyHasLimits, "parameter", "parameters")} already ${skipped.alreadyHasLimits === 1 ? "has" : "have"} limits here`,
        skipped.otherLab > 0 && `${plural(skipped.otherLab, "parameter belongs", "parameters belong")} to another laboratory`
      ].filter((l): l is string => Boolean(l))
    : [];

  const submitLabel =
    running && progress
      ? `Copying ${progress.current} of ${progress.total}`
      : `Copy ${plural(pending.length, "parameter", "parameters")}`;

  return (
    <FormDialog
      open
      title={`Copy specifications to ${item.name} (${item.code})`}
      onClose={handleClose}
      onSubmit={handleCopy}
      submitLabel={submitLabel}
      submitting={running}
      submitDisabled={pending.length === 0}
      error={formError}
      maxWidth="md"
    >
      <Typography variant="body2" color="text.secondary">
        Limits are copied exactly as they are on the other item. Tests that already have limits here are never changed.
        Each parameter is saved as a normal specification and recorded in the audit trail.
      </Typography>

      {itemsError ? (
        <LoadErrorAlert message={`Items could not be loaded: ${itemsError}`} onRetry={() => setItemsAttempt((n) => n + 1)} />
      ) : (
        <Autocomplete
          options={(items ?? []).filter((i) => i.id !== item.id)}
          value={source}
          loading={items == null}
          disabled={running}
          onChange={(_, value) => pickSource(value)}
          getOptionLabel={(o) => `${o.name} (${o.code})`}
          isOptionEqualToValue={(a, b) => a.id === b.id}
          renderInput={(params) => <TextField {...params} size="small" label="Copy from item" />}
        />
      )}

      {source && rowsError && (
        <LoadErrorAlert
          message={`The specifications of ${source.name} could not be loaded: ${rowsError}`}
          onRetry={() => setRowsAttempt((n) => n + 1)}
        />
      )}
      {source && !rowsError && !selection && (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          <CircularProgress size={16} />
          <Typography variant="body2">Loading specifications</Typography>
        </Stack>
      )}
      {source && !definitionsLoaded && (
        <Alert severity="warning">
          The test definitions are not loaded, so the laboratory of each test cannot be checked. Close this dialog and reopen it once the page has loaded.
        </Alert>
      )}

      {selection?.categoryMismatch && (
        <Alert severity="info">
          {source?.name} is a different kind of item from {item.name}. Grades and production stages differ between
          categories, so nothing is offered to copy; add the parameters one by one instead.
        </Alert>
      )}

      {selection && !selection.categoryMismatch && (
        <Box>
          {candidates.length === 0 ? (
            <Alert severity="info">Nothing on {source?.name} can be copied to {item.name}.</Alert>
          ) : (
            <Stack spacing={0.5} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1, p: 1 }}>
              {candidates.map((c) => {
                const isDone = done.has(c.key);
                const isFailed = failure?.key === c.key;
                const qualifiers = specQualifiers(c.spec);
                return (
                  <Box key={c.key} sx={{ opacity: c.cannotCopy ? 0.7 : 1 }}>
                    <FormControlLabel
                      sx={{ alignItems: "flex-start", m: 0 }}
                      control={
                        <Checkbox
                          size="small"
                          checked={isSelected(c)}
                          disabled={running || isDone || c.cannotCopy != null}
                          onChange={() => toggle(c.key)}
                          slotProps={{ input: { "aria-label": `Copy ${rowLabel(c)}` } }}
                        />
                      }
                      label={
                        <Box>
                          <Typography sx={{ fontWeight: 600, fontSize: 13 }}>
                            {rowLabel(c)} <Typography component="span" sx={{ fontSize: 12, color: "text.secondary" }}>{c.spec.testCode}</Typography>
                          </Typography>
                          <Typography sx={{ fontSize: 13 }}>{limitSummary(c.spec)}</Typography>
                          {qualifiers.length > 0 && (
                            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{qualifiers.join(" · ")}</Typography>
                          )}
                          {c.cannotCopy && (
                            <Typography sx={{ fontSize: 12, color: "warning.main" }}>Cannot be copied: {c.cannotCopy}</Typography>
                          )}
                          {isDone && <Typography sx={{ fontSize: 12, color: "success.main" }}>Copied</Typography>}
                          {isFailed && <Typography sx={{ fontSize: 12, color: "error.main" }}>Failed: {failure?.message}</Typography>}
                        </Box>
                      }
                    />
                  </Box>
                );
              })}
            </Stack>
          )}
          {skippedLines.length > 0 && (
            <Box sx={{ mt: 1 }}>
              <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                Not offered:
              </Typography>
              {skippedLines.map((line) => (
                <Typography key={line} variant="caption" color="text.secondary" sx={{ display: "block" }}>
                  {line}
                </Typography>
              ))}
            </Box>
          )}
        </Box>
      )}
    </FormDialog>
  );
};
