import React, { useState, useEffect, useCallback, useMemo } from "react";
import type { ClipboardEvent as ReactClipboardEvent } from "react";
import {
  Box,
  Typography,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Button,
  IconButton,
  Chip,
  Alert,
  CircularProgress,
  Paper,
  Stack,
  TextField,
  useTheme,
  TableContainer
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import ContentPasteIcon from "@mui/icons-material/ContentPaste";
import { Item } from "../services/ItemService";
import {
  SpecificationService,
  SpecificationDto,
  productionStageRoleLabel
} from "../../specifications/services/SpecificationService";
import { collapseGrades, gradesOf, siblingsOf } from "../../specifications/services/gradeGroups";
import { ChangeReasonDialog } from "../../../../components/config/ChangeReasonDialog";
import { parseClipboardTable } from "../../../../components/entry/parseClipboardTable";
import { PastePreviewDialog, type PasteAcceptedRow } from "../../../../components/entry/PastePreviewDialog";
import { PendingBatchList, type PendingBatchColumn } from "../../../../components/entry/PendingBatchList";
import { useMyLabs } from "../../../../hooks/useMyLabs";
import { useLaboratorySections } from "../../../../hooks/useLaboratorySections";
import { tableHeadSx } from "../../../../theme";
import { monospaceFontFamily } from "../../../../theme/palette";
import { extractBatchSaveErrors } from "../../../../utils/fieldErrors";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { SpecificationParameterDialog, TestDefinitionSummary } from "./SpecificationParameterDialog";
import { CopySpecificationsDialog } from "./CopySpecificationsDialog";
import { formatLimitCell, formatResultBasis, ownLabTestCodes } from "./copySpecifications";
import {
  buildItemSpecificationPastePlan,
  toCreateSpecificationPayload,
  toPendingItemSpecifications,
  type PendingItemSpecification
} from "./itemSpecificationPaste";
import { HplcMethodService } from "../../masterDataSimple/services/HplcMethodService";

interface ItemSpecificationsSectionProps {
  item: Item;
  onSpecsChanged?: () => void;
  // A viewer who may not manage items (the QC Manager) sees the specifications without edit controls.
  readOnly?: boolean;
}

// Limit types are categories, not states: the label carries the meaning, so
// one neutral chip style (theme-aware, AA contrast in both modes). The old
// per-type hues were decorative - two types shared a colour - and amber text
// on its own 12% tint measured about 2:1.
const LIMIT_TYPE_LABELS: Record<string, string> = {
  Range: "Range",
  NotMoreThan: "NMT",
  NotLessThan: "NLT",
  TargetWithTolerance: "Target \u00B1 Tol.",
  CountTiered: "Count-Tiered",
  Qualitative: "Qualitative",
  PresenceAbsence: "Presence/Absence",
  MultiStage: "Multi-Stage",
  StageCriteria: "Stage Criteria",
  DissolutionQ: "Dissolution Q",
  DisintegrationTime: "Disintegration Time",
  WeightVariation: "Weight Variation",
};

export const LimitTypeBadge: React.FC<{ type: string; labelOverride?: string }> = ({
  type,
  labelOverride
}) => {
  return (
    <Chip
      size="small"
      variant="outlined"
      label={labelOverride ?? LIMIT_TYPE_LABELS[type] ?? type}
      sx={{
        height: 22,
        fontSize: 12,
        fontWeight: 600,
        color: "text.secondary",
        borderColor: "divider",
        borderRadius: "4px"
      }}
    />
  );
};

const SPEC_PENDING_COLUMNS: PendingBatchColumn<PendingItemSpecification>[] = [
  {
    key: "testCode",
    label: "Test",
    sx: { fontFamily: monospaceFontFamily, fontWeight: 600 },
    render: (r) => r.testCode
  },
  { key: "parameterName", label: "Parameter name", render: (r) => r.parameterName || "—" },
  { key: "limitTypeLabel", label: "Limit type", render: (r) => r.limitTypeLabel },
  {
    key: "lowerLimit",
    label: "Lower limit",
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => (r.lowerLimit !== null ? String(r.lowerLimit) : "—")
  },
  {
    key: "upperLimit",
    label: "Upper limit",
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => (r.upperLimit !== null ? String(r.upperLimit) : "—")
  },
  {
    key: "target",
    label: "Target",
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => (r.target !== null ? String(r.target) : "—")
  },
  {
    key: "tolerance",
    label: "Tolerance",
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => (r.tolerance !== null ? String(r.tolerance) : "—")
  },
  {
    key: "toleranceModeLabel",
    label: "Tolerance mode",
    render: (r) => r.toleranceModeLabel || "—"
  },
  {
    key: "alertLimit",
    label: "Alert limit",
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => r.alertLimit || "—"
  },
  {
    key: "actionLimit",
    label: "Action limit",
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => r.actionLimit || "—"
  },
  {
    key: "specLimit",
    label: "Spec limit",
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => r.specLimit || "—"
  },
  { key: "expectedResultText", label: "Expected result text", render: (r) => r.expectedResultText || "—" },
  { key: "unit", label: "Unit", render: (r) => r.unit || "—" }
];

export const ItemSpecificationsSection: React.FC<ItemSpecificationsSectionProps> = ({
  item,
  onSpecsChanged,
  readOnly = false
}) => {
  const theme = useTheme();

  const [specs, setSpecs] = useState<SpecificationDto[]>(item.specifications ?? []);
  const [loading, setLoading] = useState(false);
  const [workflowTypeByCode, setWorkflowTypeByCode] = useState<Record<string, string>>({});
  const [testDefinitionByCode, setTestDefinitionByCode] = useState<Record<string, TestDefinitionSummary>>({});
  const [hplcAnalyteById, setHplcAnalyteById] = useState<Record<number, { name: string }>>({});
  const [icpElementById, setIcpElementById] = useState<Record<number, { symbol: string }>>({});
  const [error, setError] = useState<string | null>(null);
  // Test-definition and analyte-name lookups that failed. Without them the
  // "Analyte: ..." chips silently disappear, so per-analyte specifications
  // look identical; say so rather than show a quietly incomplete list.
  const [lookupsIncomplete, setLookupsIncomplete] = useState(false);

  // Dialog state
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingSpec, setEditingSpec] = useState<SpecificationDto | null>(null);
  const [preselectedTestCode, setPreselectedTestCode] = useState<string | null>(null);
  const [copyOpen, setCopyOpen] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  // Delete / retire / reinstate state
  const [pendingDelete, setPendingDelete] = useState<SpecificationDto | null>(null);
  const [pendingReinstate, setPendingReinstate] = useState<SpecificationDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [reasonError, setReasonError] = useState<string | null>(null);

  // Paste batch state
  const [pasteBarOpen, setPasteBarOpen] = useState(false);
  const [pastePreviewCells, setPastePreviewCells] = useState<string[][] | null>(null);
  const [pendingSpecs, setPendingSpecs] = useState<PendingItemSpecification[]>([]);
  const [savingBatch, setSavingBatch] = useState(false);
  const [batchError, setBatchError] = useState<string | null>(null);

  const { codes: myLabCodes } = useMyLabs();
  const { sections } = useLaboratorySections();
  const myLabSectionIds = useMemo(
    () => new Set(sections.filter((s) => myLabCodes.includes(s.sectionCode)).map((s) => s.sectionId)),
    [sections, myLabCodes]
  );

  useEffect(() => {
    masterDataOptions
      .getTestDefinitions()
      .then(async (defs: TestDefinitionSummary[]) => {
        const byCode: Record<string, TestDefinitionSummary> = {};
        const hplcMethodIds: number[] = [];
        for (const d of defs) {
          byCode[d.code] = d;
          if ((d.workflowType === "HplcMethodAssay" || d.equationType === "HplcMethodAssay") && d.hplcMethodId) {
            hplcMethodIds.push(d.hplcMethodId);
          }
        }
        setTestDefinitionByCode(byCode);
        setWorkflowTypeByCode(Object.fromEntries(defs.map((d) => [d.code, d.workflowType])));

        const uniqueHplcMethodIds = Array.from(new Set(hplcMethodIds));
        if (uniqueHplcMethodIds.length > 0) {
          const hMap: Record<number, { name: string }> = {};
          await Promise.all(
            uniqueHplcMethodIds.map((mId) =>
              HplcMethodService.getById(mId)
                .then((method) => {
                  for (const a of method.analytes ?? []) {
                    hMap[a.id] = { name: a.name };
                  }
                })
                .catch(() => setLookupsIncomplete(true))
            )
          );
          setHplcAnalyteById(hMap);
        }

        // ICP elements belong to the test itself, so the labels come with the test definitions.
        const iMap: Record<number, { symbol: string }> = {};
        for (const d of defs) {
          for (const el of d.icpElements ?? []) {
            iMap[el.id] = { symbol: el.symbol };
          }
        }
        setIcpElementById(iMap);
      })
      .catch(() => {
        // Non-fatal: the specifications still load, only labels are missing.
        setLookupsIncomplete(true);
      });
  }, []);

  const loadSpecs = useCallback(async () => {
    if (!item?.id) return;
    setLoading(true);
    try {
      const data = await SpecificationService.getForItem(item.id);
      if (Array.isArray(data)) {
        setSpecs(data);
      }
    } catch {
      setSpecs(item.specifications ?? []);
    } finally {
      setLoading(false);
    }
  }, [item?.id, item.specifications]);

  useEffect(() => {
    setSpecs(item.specifications ?? []);
    loadSpecs();
    setError(null);
  }, [item.id, item.specifications, loadSpecs]);

  const assignedTests = useMemo(() => item.assignedTests ?? [], [item.assignedTests]);

  const selectableAssignedTests = useMemo(() => {
    const allowedCodes = ownLabTestCodes(assignedTests, testDefinitionByCode, myLabSectionIds, item.category);
    return assignedTests.filter((t) => allowedCodes.has(t.testCode));
  }, [assignedTests, testDefinitionByCode, myLabSectionIds, item.category]);

  const pastePlan = useMemo(
    () => buildItemSpecificationPastePlan({ assignedTests: selectableAssignedTests }),
    [selectableAssignedTests]
  );

  useEffect(() => {
    setPendingSpecs([]);
    setBatchError(null);
    setPastePreviewCells(null);
    setPasteBarOpen(false);
  }, [item.id]);

  const anyModalOpen =
    dialogOpen ||
    copyOpen ||
    pendingDelete != null ||
    pastePreviewCells !== null;

  useEffect(() => {
    if (anyModalOpen || !pasteBarOpen || readOnly || assignedTests.length === 0) return;
    const handleDocumentPaste = (e: ClipboardEvent) => {
      if (e.defaultPrevented) return;
      const target = e.target as HTMLElement | null;
      if (
        target &&
        (target.tagName === "INPUT" ||
          target.tagName === "TEXTAREA" ||
          target.tagName === "SELECT" ||
          target.isContentEditable)
      ) {
        return;
      }
      const text = e.clipboardData?.getData("text/plain") ?? "";
      const table = parseClipboardTable(text);
      if (table.length === 0) return;
      e.preventDefault();
      setPastePreviewCells(table);
    };
    document.addEventListener("paste", handleDocumentPaste);
    return () => document.removeEventListener("paste", handleDocumentPaste);
  }, [anyModalOpen, pasteBarOpen, readOnly, assignedTests.length]);

  const handlePasteInput = (e: ReactClipboardEvent<HTMLElement>) => {
    if (!pasteBarOpen || readOnly || assignedTests.length === 0) return;
    const table = parseClipboardTable(e.clipboardData.getData("text/plain"));
    if (table.length === 0) return;
    e.preventDefault();
    e.stopPropagation();
    setPastePreviewCells(table);
  };

  const handleAcceptPaste = (rows: PasteAcceptedRow[]) => {
    setPendingSpecs(toPendingItemSpecifications(rows, selectableAssignedTests));
    setBatchError(null);
    setPastePreviewCells(null);
    setPasteBarOpen(false);
  };

  const saveSpecsBatch = async () => {
    if (pendingSpecs.length === 0) return;
    setSavingBatch(true);
    setBatchError(null);
    try {
      const payload = pendingSpecs.map((row) => toCreateSpecificationPayload(item.id, row));
      await SpecificationService.createSpecificationsBatch(payload);
      const count = pendingSpecs.length;
      setPendingSpecs([]);
      setNotice(
        `Added ${count} specification parameter${count === 1 ? "" : "s"} to ${item.name}.`
      );
      await loadSpecs();
      onSpecsChanged?.();
    } catch (e: unknown) {
      const { summary, rowErrors } = extractBatchSaveErrors(
        e,
        "Could not save specifications. Nothing was created."
      );
      if (rowErrors) {
        setPendingSpecs((prev) => prev.map((row, idx) => ({ ...row, errors: rowErrors[idx] })));
      }
      setBatchError(summary);
    } finally {
      setSavingBatch(false);
    }
  };

  const getTestDisplayName = useCallback(
    (testCode: string) => {
      const match = assignedTests.find((t) => t.testCode === testCode);
      return match?.displayName && match.displayName !== testCode
        ? `${match.displayName} (${testCode})`
        : match?.displayName || testCode;
    },
    [assignedTests]
  );

  const handleOpenAdd = () => {
    setEditingSpec(null);
    setPreselectedTestCode(null);
    setDialogOpen(true);
  };

  const handleAddParameterToTest = (testCode: string) => {
    setEditingSpec(null);
    setPreselectedTestCode(testCode);
    setDialogOpen(true);
  };

  const handleOpenEdit = (spec: SpecificationDto) => {
    setEditingSpec(spec);
    setPreselectedTestCode(null);
    setDialogOpen(true);
  };

  const handleDeleteConfirm = async (reason: string) => {
    if (!pendingDelete?.id) return;
    setDeleting(true);
    setError(null);
    setReasonError(null);
    const label = pendingDelete.parameterName || pendingDelete.testCode;
    try {
      let retiredCount = 0;
      let deletedCount = 0;
      for (const s of siblingsOf(pendingDelete)) {
        if (s.id != null) {
          const res = await SpecificationService.remove(s.id, reason);
          if (res?.outcome === "retired") retiredCount += 1;
          else deletedCount += 1;
        }
      }
      setPendingDelete(null);
      if (retiredCount > 0 && deletedCount === 0) {
        setNotice(`Specification "${label}" was retired because results are recorded against it.`);
      } else if (retiredCount > 0 && deletedCount > 0) {
        setNotice(`Specification "${label}": ${retiredCount} retired (has results), ${deletedCount} deleted.`);
      } else {
        setNotice(`Specification "${label}" was deleted.`);
      }
      await loadSpecs();
      onSpecsChanged?.();
    } catch (err: unknown) {
      const axiosError = err as {
        response?: { data?: { message?: string } };
        message?: string;
      };
      setReasonError(
        axiosError?.response?.data?.message ||
          axiosError?.message ||
          "Failed to delete specification."
      );
    } finally {
      setDeleting(false);
    }
  };

  const handleReinstateConfirm = async (reason: string) => {
    if (!pendingReinstate?.id) return;
    setDeleting(true);
    setError(null);
    setReasonError(null);
    const label = pendingReinstate.parameterName || pendingReinstate.testCode;
    try {
      for (const s of siblingsOf(pendingReinstate)) {
        if (s.id != null && s.isRetired) {
          await SpecificationService.reinstate(s.id, reason);
        }
      }
      setPendingReinstate(null);
      setNotice(`Specification "${label}" was reinstated.`);
      await loadSpecs();
      onSpecsChanged?.();
    } catch (err: unknown) {
      const axiosError = err as {
        response?: { data?: { message?: string } };
        message?: string;
      };
      setReasonError(
        axiosError?.response?.data?.message ||
          axiosError?.message ||
          "Failed to reinstate specification."
      );
    } finally {
      setDeleting(false);
    }
  };

  // Group specs by testCode (preserving order of appearance)
  const groupedSpecs: { testCode: string; specs: SpecificationDto[] }[] = [];
  const groupMap = new Map<string, SpecificationDto[]>();
  for (const spec of collapseGrades(specs)) {
    const list = groupMap.get(spec.testCode) ?? [];
    list.push(spec);
    groupMap.set(spec.testCode, list);
  }
  for (const [testCode, list] of groupMap.entries()) {
    groupedSpecs.push({ testCode, specs: list });
  }

  return (
    <Box sx={{ p: 0.5 }}>
      {/* Header section */}
      <Stack
        direction="row"
        sx={{
          justifyContent: "space-between",
          alignItems: "flex-start",
          mb: 1
        }}
      >
        <Box>
          <Typography
            variant="subtitle1"
            sx={{
              fontWeight: 800,
              fontSize: 13,
              letterSpacing: "0.5px",
              color: theme.palette.primary.main,
              textTransform: "uppercase"
            }}
          >
            PHARMACOPOEIAL SPECIFICATIONS & LIMITS ({specs.length})
          </Typography>
          <Typography
            variant="caption"
            sx={{ color: "text.secondary", display: "block", mt: 0.25 }}
          >
            One table for every limit type: count-based, numeric, qualitative and multi-stage.
          </Typography>
          {assignedTests.length > 0 && (
            <Typography
              variant="caption"
              sx={{ color: "text.secondary", display: "block" }}
            >
              Limits set for {assignedTests.filter((t) => specs.some((s) => s.testCode === t.testCode)).length} of{" "}
              {assignedTests.length} assigned tests
            </Typography>
          )}
        </Box>

        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          {loading && <CircularProgress size={16} />}
          {assignedTests.length > 0 && !readOnly && (
            <Button
              variant="outlined"
              size="small"
              startIcon={<ContentPasteIcon />}
              aria-expanded={pasteBarOpen}
              onClick={() => setPasteBarOpen((v) => !v)}
              sx={{
                fontWeight: 700,
                textTransform: "none",
                fontSize: 12
              }}
            >
              Paste specifications
            </Button>
          )}
          {assignedTests.length > 0 && !readOnly && (
            <Button
              variant="outlined"
              size="small"
              startIcon={<ContentCopyIcon />}
              onClick={() => setCopyOpen(true)}
              sx={{
                fontWeight: 700,
                textTransform: "none",
                fontSize: 12
              }}
            >
              Copy from another item
            </Button>
          )}
          {assignedTests.length > 0 && !readOnly && (
            <Button
              variant="contained"
              size="small"
              startIcon={<AddIcon />}
              onClick={handleOpenAdd}
              sx={{
                fontWeight: 700,
                textTransform: "none",
                fontSize: 12
              }}
            >
              Add Specification Parameter
            </Button>
          )}
        </Stack>
      </Stack>

      {pasteBarOpen && !readOnly && assignedTests.length > 0 && (
        <Paper variant="outlined" sx={{ p: 2, mb: 2, borderRadius: 2 }}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "flex-start" } }}>
            <TextField
              autoFocus
              size="small"
              fullWidth
              label="Paste specification rows"
              placeholder="Paste rows copied from a spreadsheet (Test, Parameter name, Limit type, Lower limit, Upper limit, Target, Tolerance, Tolerance mode, Alert limit, Action limit, Spec limit, Expected result text, Unit)"
              value=""
              onChange={() => {}}
              onPaste={handlePasteInput}
              helperText="Columns in order: Test, Parameter name, Limit type, Lower limit, Upper limit, Target, Tolerance, Tolerance mode, Alert limit, Action limit, Spec limit, Expected result text, Unit. A pasted range includes both limits; use Add Specification Parameter for an exclusive limit. Use Add Specification Parameter for stages, HPLC analytes/impurities, ICP elements, related-substance rows, dosage form, label claim, production stage, or grade."
            />
            <Button onClick={() => setPasteBarOpen(false)} sx={{ textTransform: "none", flexShrink: 0 }}>
              Close
            </Button>
          </Stack>
        </Paper>
      )}

      {/* Colour Legend */}
      <Stack useFlexGap
        direction="row"
        spacing={1}
        sx={{
          alignItems: "center",
          flexWrap: "wrap",
          gap: 1,
          mb: 2
        }}
      >
        <LimitTypeBadge type="Range" labelOverride="Range" />
        <LimitTypeBadge type="NotMoreThan" labelOverride="NMT/NLT" />
        <LimitTypeBadge type="TargetWithTolerance" labelOverride="Target ± Tol." />
        <LimitTypeBadge type="CountTiered" labelOverride="Count-Tiered" />
        <LimitTypeBadge type="Qualitative" labelOverride="Qualitative" />
        <LimitTypeBadge type="PresenceAbsence" labelOverride="Presence/Absence" />
        <LimitTypeBadge type="MultiStage" labelOverride="Multi-Stage" />
        <LimitTypeBadge type="DissolutionQ" labelOverride="Dissolution Q" />
        <LimitTypeBadge type="DisintegrationTime" labelOverride="Disintegration Time" />
        <LimitTypeBadge type="WeightVariation" labelOverride="Weight Variation" />
      </Stack>

      {lookupsIncomplete && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Some test and analyte names could not be loaded, so analyte labels may be missing from the specifications below. Reload the page to try again.
        </Alert>
      )}
      {error && (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}
      {notice && (
        <Alert severity="success" onClose={() => setNotice(null)} sx={{ mb: 2 }}>
          {notice}
        </Alert>
      )}

      <PendingBatchList
        title={`Pending specifications (${pendingSpecs.length})`}
        subtitle={`Review the ${pendingSpecs.length} specification parameter${pendingSpecs.length === 1 ? "" : "s"} below. Nothing is saved until you click Save.`}
        ariaLabel="Pending specifications"
        columns={SPEC_PENDING_COLUMNS}
        rows={pendingSpecs}
        getRowKey={(r, i) => `${r.testCode}-${r.parameterName}-${i}`}
        getRowLabel={(r) => (r.parameterName ? `${r.testCode} (${r.parameterName})` : r.testCode)}
        saving={savingBatch}
        error={batchError}
        onSave={saveSpecsBatch}
        onDiscard={() => {
          setPendingSpecs([]);
          setBatchError(null);
        }}
        onRemoveRow={(index) => {
          setPendingSpecs((prev) => prev.filter((_, i) => i !== index));
          setBatchError(null);
        }}
      />

      {assignedTests.length === 0 ? (
        <Alert severity="warning" sx={{ py: 1, fontSize: 13 }}>
          This item has no assigned tests yet. Assign tests under the{" "}
          <strong>Assigned Tests</strong> tab or edit the item before configuring
          specifications.
        </Alert>
      ) : (
        <Box>
          <TableContainer>
            <Table
              size="small"
              sx={{
                mb: 2,
                border: "1px solid",
                borderColor: "divider",
                borderRadius: 1,
                overflow: "hidden"
              }}
            >
              <TableHead>
                <TableRow sx={tableHeadSx}>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 220 }}>
                    Assigned Test / Parameter
                  </TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 140 }}>
                    Limit Type
                  </TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 200 }}>
                    Limit
                  </TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 100 }}>
                    Unit
                  </TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 150 }}>
                    Reference Standard
                  </TableCell>
                  <TableCell align="right" sx={{ fontWeight: 700, fontSize: 12, width: 90 }}>
                    Actions
                  </TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {groupedSpecs.length === 0 ? (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      sx={{ py: 3, textAlign: "center", color: "text.secondary", fontStyle: "italic" }}
                    >
                      No specifications defined yet. Click &ldquo;Add Specification Parameter&rdquo; above to add limits.
                    </TableCell>
                  </TableRow>
                ) : (
                  groupedSpecs.map((group) => {
                    const testDisplayName = getTestDisplayName(group.testCode);
                    const isMultiParam = group.specs.length > 1;

                    if (!isMultiParam) {
                      // Test with a single parameter
                      const spec = group.specs[0];
                      const isMultiStage = spec.limitType === "MultiStage";
                      const displayName = spec.parameterName || testDisplayName;

                      return (
                        <React.Fragment key={group.testCode}>
                          <TableRow
                            hover
                            sx={{ "&:nth-of-type(even)": { bgcolor: "background.default" } }}
                          >
                            <TableCell sx={{ fontWeight: 600, fontSize: 13 }}>
                              {displayName}
                              {(spec.isRetired || spec.resultBasis || spec.hplcMethodAnalyteId || spec.canEdit === false || spec.productionStageRole || spec.grade) && (
                                <Box sx={{ display: "flex", gap: 0.75, mt: 0.5, flexWrap: "wrap", alignItems: "center" }}>
                                  {spec.isRetired && (
                                    <Chip size="small" label="Retired" color="warning" variant="outlined" sx={{ height: 20, fontSize: 12, fontWeight: 700 }} />
                                  )}
                                  {spec.grade && (
                                    <Chip size="small" label={`Grade: ${gradesOf(spec).join(", ")}`} sx={{ height: 20, fontSize: 12, fontWeight: 600, color: "primary.main", border: "1px solid", borderColor: "primary.main", bgcolor: "transparent" }} />
                                  )}
                                  {spec.productionStageRole && (
                                    <Chip
                                      size="small"
                                      label={`Stage: ${productionStageRoleLabel(spec.productionStageRole)}`}
                                      sx={{ height: 20, fontSize: 12, fontWeight: 600, color: "secondary.main", border: "1px solid", borderColor: "secondary.main", bgcolor: "transparent" }}
                                    />
                                  )}
                                  {spec.canEdit === false && (
                                    <Chip
                                      size="small"
                                      label={spec.sectionName || "Other lab"}
                                      sx={{
                                        height: 20,
                                        fontSize: 12,
                                        fontWeight: 600,
                                        color: "text.secondary",
                                        bgcolor: "action.selected"
                                      }}
                                    />
                                  )}
                                  {spec.hplcMethodAnalyteId && hplcAnalyteById[spec.hplcMethodAnalyteId] && (
                                    <Chip
                                      size="small"
                                      label={`Analyte: ${hplcAnalyteById[spec.hplcMethodAnalyteId].name}`}
                                      sx={{
                                        height: 20,
                                        fontSize: 12,
                                        color: "primary.main",
                                        bgcolor: "primary.50",
                                        border: "1px solid",
                                        borderColor: "primary.200"
                                      }}
                                    />
                                  )}
                                  {spec.icpTestElementId && icpElementById[spec.icpTestElementId] && (
                                    <Chip
                                      size="small"
                                      label={`Element: ${icpElementById[spec.icpTestElementId].symbol}`}
                                      sx={{
                                        height: 20,
                                        fontSize: 12,
                                        color: "primary.main",
                                        bgcolor: "primary.50",
                                        border: "1px solid",
                                        borderColor: "primary.200"
                                      }}
                                    />
                                  )}
                                  {spec.resultBasis && (
                                    <Chip
                                      size="small"
                                      label={`Basis: ${formatResultBasis(
                                        spec.resultBasis as string,
                                        workflowTypeByCode[spec.testCode] === "IcpMethodAssay" ||
                                          testDefinitionByCode[spec.testCode]?.equationType === "IcpMethodAssay"
                                      )}`}
                                      sx={{
                                        height: 20,
                                        fontSize: 12,
                                        color: "text.secondary",
                                        bgcolor: "action.hover",
                                        border: "1px solid",
                                        borderColor: "divider"
                                      }}
                                    />
                                  )}
                                </Box>
                              )}
                            </TableCell>
                            <TableCell>
                              <LimitTypeBadge type={spec.limitType ?? "CountTiered"} />
                            </TableCell>
                            <TableCell sx={{ fontSize: 13 }}>
                              {formatLimitCell(spec)}
                              {spec.limitType === "CountTiered" && spec.dilutionFactor != null && (
                                <Box sx={{ mt: 0.5 }}>
                                  <Chip
                                    size="small"
                                    label={`Dilution Factor \u00D7${spec.dilutionFactor}`}
                                    sx={{
                                      height: 20,
                                      fontSize: 12,
                                      color: "text.secondary",
                                      bgcolor: "action.hover",
                                      border: "1px solid",
                                      borderColor: "divider"
                                    }}
                                  />
                                </Box>
                              )}
                              {spec.limitType === "DissolutionQ" && spec.labelClaim != null && (
                                <Box sx={{ mt: 0.5 }}>
                                  <Chip
                                    size="small"
                                    label={`Label Claim ${spec.labelClaim} ${spec.labelClaimUnit || "mg"}`}
                                    sx={{
                                      height: 20,
                                      fontSize: 12,
                                      color: "text.secondary",
                                      bgcolor: "action.hover",
                                      border: "1px solid",
                                      borderColor: "divider"
                                    }}
                                  />
                                </Box>
                              )}
                            </TableCell>
                            <TableCell sx={{ fontSize: 13 }}>{spec.unit || "\u2014"}</TableCell>
                            <TableCell sx={{ fontSize: 13 }}>
                              {spec.referenceStandard || "\u2014"}
                            </TableCell>
                            <TableCell align="right">
                              {readOnly || spec.canEdit === false ? (
                                <Typography sx={{ fontSize: 12, color: "text.disabled", fontStyle: "italic" }}>
                                  Read-only
                                </Typography>
                              ) : spec.isRetired ? (
                                <Button
                                  size="small"
                                  variant="outlined"
                                  onClick={() => { setReasonError(null); setPendingReinstate(spec); }}
                                  sx={{ textTransform: "none", fontSize: 12, fontWeight: 600 }}
                                >
                                  Reinstate
                                </Button>
                              ) : (
                                <Stack
                                  direction="row"
                                  spacing={0.5}
                                  sx={{ justifyContent: "flex-end" }}
                                >
                                  <IconButton aria-label={`Edit specification for ${spec.testCode}`}
                                    size="small"
                                    onClick={() => handleOpenEdit(spec)}
                                    title="Edit Specification"
                                  >
                                    <EditIcon fontSize="small" />
                                  </IconButton>
                                  <IconButton aria-label={`Delete specification for ${spec.testCode}`}
                                    size="small"
                                    color="error"
                                    onClick={() => { setReasonError(null); setPendingDelete(spec); }}
                                    title="Delete Specification"
                                  >
                                    <DeleteIcon fontSize="small" />
                                  </IconButton>
                                </Stack>
                              )}
                            </TableCell>
                          </TableRow>

                          {/* If MultiStage, always show stages as indented rows */}
                          {isMultiStage &&
                            spec.stages?.map((stage, sIdx) => (
                              <TableRow
                                key={`${spec.id ?? spec.testCode}-stage-${sIdx}`}
                                sx={{ bgcolor: "action.hover" }}
                              >
                                <TableCell sx={{ pl: 3.5 }}>
                                  <Stack
                                    direction="row"
                                    spacing={1}
                                    sx={{ alignItems: "center" }}
                                  >
                                    <Typography
                                      sx={{ color: "text.secondary", fontSize: 13 }}
                                    >
                                      &#8627;
                                    </Typography>
                                    <Typography sx={{ fontSize: 12 }}>
                                      {stage.stageLabel}
                                    </Typography>
                                  </Stack>
                                </TableCell>
                                <TableCell>
                                  <LimitTypeBadge type="StageCriteria" />
                                </TableCell>
                                <TableCell sx={{ fontSize: 13, color: "text.primary" }}>
                                  {stage.acceptanceCriteriaText}
                                </TableCell>
                                <TableCell sx={{ fontSize: 13 }}>
                                  {spec.unit || "\u2014"}
                                </TableCell>
                                <TableCell sx={{ fontSize: 13 }}>{"\u2014"}</TableCell>
                                <TableCell align="right" />
                              </TableRow>
                            ))}
                        </React.Fragment>
                      );
                    }

                    // Test with several parameters - ownership (canEdit/sectionName)
                    // is per TestCode, so it's the same for every spec in the group.
                    const groupCanEdit = group.specs[0]?.canEdit !== false;
                    const groupSectionName = group.specs[0]?.sectionName;

                    // Test with several parameters
                    return (
                      <React.Fragment key={group.testCode}>
                        {/* Group Header Row */}
                        <TableRow sx={{ bgcolor: "action.selected" }}>
                          <TableCell colSpan={6} sx={{ py: 1 }}>
                            <Stack
                              direction="row"
                              spacing={1.5}
                              sx={{ alignItems: "center" }}
                            >
                              <Typography sx={{ fontWeight: 700, fontSize: 13 }}>
                                {testDisplayName}
                              </Typography>
                              <Chip
                                size="small"
                                label={`${group.specs.length} parameters`}
                                sx={{
                                  height: 20,
                                  fontSize: 12,
                                  bgcolor: "background.paper"
                                }}
                              />
                              {!groupCanEdit && (
                                <Chip
                                  size="small"
                                  label={groupSectionName || "Other lab"}
                                  sx={{
                                    height: 20,
                                    fontSize: 12,
                                    fontWeight: 600,
                                    color: "text.secondary",
                                    bgcolor: "background.paper"
                                  }}
                                />
                              )}
                            </Stack>
                          </TableCell>
                        </TableRow>

                        {/* Indented parameter rows */}
                        {group.specs.map((spec) => {
                          const isMultiStage = spec.limitType === "MultiStage";
                          return (
                            <React.Fragment key={spec.id ?? spec.parameterName}>
                              <TableRow hover>
                                <TableCell sx={{ pl: 3.5 }}>
                                  <Stack
                                    direction="row"
                                    spacing={1}
                                    sx={{ alignItems: "center" }}
                                  >
                                    <Typography
                                      sx={{ color: "text.secondary", fontSize: 13 }}
                                    >
                                      &#8627;
                                    </Typography>
                                    <Box>
                                      <Typography sx={{ fontWeight: 600, fontSize: 13 }}>
                                        {spec.parameterName}
                                      </Typography>
                                      {(spec.isRetired || spec.resultBasis || spec.hplcMethodAnalyteId || spec.productionStageRole || spec.grade) && (
                                        <Box sx={{ display: "flex", gap: 0.75, mt: 0.5, flexWrap: "wrap", alignItems: "center" }}>
                                          {spec.isRetired && (
                                            <Chip size="small" label="Retired" color="warning" variant="outlined" sx={{ height: 20, fontSize: 12, fontWeight: 700 }} />
                                          )}
                                          {spec.grade && (
                                            <Chip size="small" label={`Grade: ${gradesOf(spec).join(", ")}`} sx={{ height: 20, fontSize: 12, fontWeight: 600, color: "primary.main", border: "1px solid", borderColor: "primary.main", bgcolor: "transparent" }} />
                                          )}
                                          {spec.productionStageRole && (
                                            <Chip
                                              size="small"
                                              label={`Stage: ${productionStageRoleLabel(spec.productionStageRole)}`}
                                              sx={{ height: 20, fontSize: 12, fontWeight: 600, color: "secondary.main", border: "1px solid", borderColor: "secondary.main", bgcolor: "transparent" }}
                                            />
                                          )}
                                        
                                          {spec.hplcMethodAnalyteId && hplcAnalyteById[spec.hplcMethodAnalyteId] && (
                                            <Chip
                                              size="small"
                                              label={`Analyte: ${hplcAnalyteById[spec.hplcMethodAnalyteId].name}`}
                                              sx={{
                                                height: 20,
                                                fontSize: 12,
                                                color: "primary.main",
                                                bgcolor: "primary.50",
                                                border: "1px solid",
                                                borderColor: "primary.200"
                                              }}
                                            />
                                          )}
                                          {spec.icpTestElementId && icpElementById[spec.icpTestElementId] && (
                                            <Chip
                                              size="small"
                                              label={`Element: ${icpElementById[spec.icpTestElementId].symbol}`}
                                              sx={{
                                                height: 20,
                                                fontSize: 12,
                                                color: "primary.main",
                                                bgcolor: "primary.50",
                                                border: "1px solid",
                                                borderColor: "primary.200"
                                              }}
                                            />
                                          )}
                                          {spec.resultBasis && (
                                            <Chip
                                              size="small"
                                              label={`Basis: ${formatResultBasis(
                                         spec.resultBasis as string,
                                         workflowTypeByCode[spec.testCode] === "IcpMethodAssay" ||
                                           testDefinitionByCode[spec.testCode]?.equationType === "IcpMethodAssay"
                                       )}`}
                                              sx={{
                                                height: 20,
                                                fontSize: 12,
                                                color: "text.secondary",
                                                bgcolor: "action.hover",
                                                border: "1px solid",
                                                borderColor: "divider"
                                              }}
                                            />
                                          )}
                                        </Box>
                                      )}
                                    </Box>
                                  </Stack>
                                </TableCell>
                                <TableCell>
                                  <LimitTypeBadge type={spec.limitType ?? "CountTiered"} />
                                </TableCell>
                                <TableCell sx={{ fontSize: 13 }}>
                                  {formatLimitCell(spec)}
                                  {spec.limitType === "CountTiered" &&
                                    spec.dilutionFactor != null && (
                                      <Box sx={{ mt: 0.5 }}>
                                        <Chip
                                          size="small"
                                          label={`Dilution Factor \u00D7${spec.dilutionFactor}`}
                                          sx={{
                                            height: 20,
                                            fontSize: 12,
                                            color: "text.secondary",
                                            bgcolor: "action.hover",
                                            border: "1px solid",
                                            borderColor: "divider"
                                          }}
                                        />
                                      </Box>
                                    )}
                                </TableCell>
                                <TableCell sx={{ fontSize: 13 }}>
                                  {spec.unit || "\u2014"}
                                </TableCell>
                                <TableCell sx={{ fontSize: 13 }}>
                                  {spec.referenceStandard || "\u2014"}
                                </TableCell>
                                <TableCell align="right">
                                  {readOnly || spec.canEdit === false ? (
                                    <Typography sx={{ fontSize: 12, color: "text.disabled", fontStyle: "italic" }}>
                                      Read-only
                                    </Typography>
                                  ) : spec.isRetired ? (
                                    <Button
                                      size="small"
                                      variant="outlined"
                                      onClick={() => { setReasonError(null); setPendingReinstate(spec); }}
                                      sx={{ textTransform: "none", fontSize: 12, fontWeight: 600 }}
                                    >
                                      Reinstate
                                    </Button>
                                  ) : (
                                    <Stack
                                      direction="row"
                                      spacing={0.5}
                                      sx={{ justifyContent: "flex-end" }}
                                    >
                                      <IconButton aria-label={`Edit specification for ${spec.testCode}`}
                                        size="small"
                                        onClick={() => handleOpenEdit(spec)}
                                        title="Edit Specification"
                                      >
                                        <EditIcon fontSize="small" />
                                      </IconButton>
                                      <IconButton aria-label={`Delete specification for ${spec.testCode}`}
                                        size="small"
                                        color="error"
                                        onClick={() => { setReasonError(null); setPendingDelete(spec); }}
                                        title="Delete Specification"
                                      >
                                        <DeleteIcon fontSize="small" />
                                      </IconButton>
                                    </Stack>
                                  )}
                                </TableCell>
                              </TableRow>

                              {/* Stages if MultiStage parameter */}
                              {isMultiStage &&
                                spec.stages?.map((stage, sIdx) => (
                                  <TableRow
                                    key={`${spec.id ?? spec.testCode}-stage-${sIdx}`}
                                    sx={{ bgcolor: "action.hover" }}
                                  >
                                    <TableCell sx={{ pl: 5.5 }}>
                                      <Stack
                                        direction="row"
                                        spacing={1}
                                        sx={{ alignItems: "center" }}
                                      >
                                        <Typography
                                          sx={{ color: "text.secondary", fontSize: 13 }}
                                        >
                                          &#8627;
                                        </Typography>
                                        <Typography sx={{ fontSize: 12 }}>
                                          {stage.stageLabel}
                                        </Typography>
                                      </Stack>
                                    </TableCell>
                                    <TableCell>
                                      <LimitTypeBadge type="StageCriteria" />
                                    </TableCell>
                                    <TableCell
                                      sx={{ fontSize: 13, color: "text.primary" }}
                                    >
                                      {stage.acceptanceCriteriaText}
                                    </TableCell>
                                    <TableCell sx={{ fontSize: 13 }}>
                                      {spec.unit || "\u2014"}
                                    </TableCell>
                                    <TableCell sx={{ fontSize: 13 }}>{"\u2014"}</TableCell>
                                    <TableCell align="right" />
                                  </TableRow>
                                ))}
                            </React.Fragment>
                          );
                        })}

                        {/* Add parameter link for this group - hidden for a
                            read-only (other lab's) test, same rule as the
                            per-row edit/delete buttons above */}
                        {groupCanEdit && !readOnly && (
                          <TableRow sx={{ "&:hover": { bgcolor: "transparent" } }}>
                            <TableCell
                              colSpan={6}
                              sx={{
                                pl: 3.5,
                                py: 0.75,
                                borderBottom: "1px solid",
                                borderColor: "divider"
                              }}
                            >
                              <Button
                                size="small"
                                startIcon={<AddIcon sx={{ fontSize: 16 }} />}
                                onClick={() => handleAddParameterToTest(group.testCode)}
                                sx={{
                                  textTransform: "none",
                                  fontSize: 12,
                                  fontWeight: 600,
                                  color: "primary.main",
                                  p: 0
                                }}
                              >
                                + Add parameter to {testDisplayName}
                              </Button>
                            </TableCell>
                          </TableRow>
                        )}
                      </React.Fragment>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </TableContainer>
        </Box>
      )}

      {/* Add / Edit Parameter Dialog */}
      <SpecificationParameterDialog
        open={dialogOpen}
        item={item}
        editingSpec={editingSpec}
        preselectedTestCode={preselectedTestCode}
        workflowTypeByCode={workflowTypeByCode}
        testDefinitionByCode={testDefinitionByCode}
        existingSpecs={specs}
        onClose={() => setDialogOpen(false)}
        onSuccess={async () => {
          await loadSpecs();
          onSpecsChanged?.();
        }}
      />

      {copyOpen && (
        <CopySpecificationsDialog
          item={item}
          testDefinitionByCode={testDefinitionByCode}
          onClose={() => setCopyOpen(false)}
          onSpecsCreated={() => {
            void loadSpecs().then(() => onSpecsChanged?.());
          }}
          onCompleted={(count) => {
            setError(null);
            setNotice(`Copied ${count} specification parameter${count === 1 ? "" : "s"} to ${item.name}.`);
          }}
        />
      )}

      {/* Delete / Retire Change Reason Dialog */}
      <ChangeReasonDialog
        open={pendingDelete != null}
        title="Delete or retire specification"
        message={
          pendingDelete
            ? `Delete specification parameter "${pendingDelete.parameterName || pendingDelete.testCode}"${gradesOf(pendingDelete).length > 1 ? ` (grades ${gradesOf(pendingDelete).join(", ")})` : ""} for "${item.name}"? Specifications with recorded results are retired instead of deleted.`
            : undefined
        }
        confirmText="Confirm"
        loading={deleting}
        error={reasonError}
        onCancel={() => { setPendingDelete(null); setReasonError(null); }}
        onConfirm={handleDeleteConfirm}
      />

      {/* Reinstate Change Reason Dialog */}
      <ChangeReasonDialog
        open={pendingReinstate != null}
        title="Reinstate specification"
        message={
          pendingReinstate
            ? `Reinstate retired specification "${pendingReinstate.parameterName || pendingReinstate.testCode}" for "${item.name}" so it is active for new testing again.`
            : undefined
        }
        confirmText="Reinstate"
        loading={deleting}
        error={reasonError}
        onCancel={() => { setPendingReinstate(null); setReasonError(null); }}
        onConfirm={handleReinstateConfirm}
      />

      {pastePreviewCells && (
        <PastePreviewDialog
          open
          plan={pastePlan}
          cells={pastePreviewCells}
          onAccept={handleAcceptPaste}
          onCancel={() => setPastePreviewCells(null)}
        />
      )}
    </Box>
  );
};
