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
import { Trans } from "react-i18next";
import type { TFunction } from "i18next";
import { useTranslation } from "../../../../i18n";
import { Item } from "../services/ItemService";
import {
  SpecificationService,
  SpecificationDto,
  productionStageRoleLabel
} from "../../specifications/services/SpecificationService";
import { collapseGrades, gradesOf, siblingsOf } from "../../specifications/services/gradeGroups";
import { ChangeReasonDialog } from "../../../../components/config/ChangeReasonDialog";
import { useChangeControlGate } from "../../../../hooks/useChangeControlGate";
import { parseClipboardTable } from "../../../../components/entry/parseClipboardTable";
import { PastePreviewDialog, type PasteAcceptedRow } from "../../../../components/entry/PastePreviewDialog";
import { PendingBatchList, type PendingBatchColumn } from "../../../../components/entry/PendingBatchList";
import { useMyLabs } from "../../../../hooks/useMyLabs";
import { useLaboratorySections } from "../../../../hooks/useLaboratorySections";
import { tableHeadSx } from "../../../../theme";
import { monospaceFontFamily } from "../../../../theme/palette";
import { extractBatchSaveErrors } from "../../../../utils/fieldErrors";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { SpecificationWizard, type TestDefinitionSummary } from "../specWizard/SpecificationWizard";
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
const LIMIT_TYPE_KEYS: Record<string, string> = {
  Range: "items.specifications.limitTypes.Range",
  NotMoreThan: "items.specifications.limitTypes.NotMoreThan",
  NotLessThan: "items.specifications.limitTypes.NotLessThan",
  TargetWithTolerance: "items.specifications.limitTypes.TargetWithTolerance",
  CountTiered: "items.specifications.limitTypes.CountTiered",
  Qualitative: "items.specifications.limitTypes.Qualitative",
  PresenceAbsence: "items.specifications.limitTypes.PresenceAbsence",
  MultiStage: "items.specifications.limitTypes.MultiStage",
  StageCriteria: "items.specifications.limitTypes.StageCriteria",
  DissolutionQ: "items.specifications.limitTypes.DissolutionQ",
  DisintegrationTime: "items.specifications.limitTypes.DisintegrationTime",
  WeightVariation: "items.specifications.limitTypes.WeightVariation",
};

export const LimitTypeBadge: React.FC<{ type: string; labelOverride?: string }> = ({
  type,
  labelOverride
}) => {
  const { t } = useTranslation("labConfig");
  const key = LIMIT_TYPE_KEYS[type];
  return (
    <Chip
      size="small"
      variant="outlined"
      label={labelOverride ?? (key ? t(key) : type)}
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

function getSpecPendingColumns(t: TFunction<"labConfig">): PendingBatchColumn<PendingItemSpecification>[] {
  return [
    {
      key: "testCode",
      label: t("items.specifications.pendingColumns.testCode"),
      sx: { fontFamily: monospaceFontFamily, fontWeight: 600 },
      render: (r) => r.testCode
    },
    { key: "parameterName", label: t("items.specifications.pendingColumns.parameterName"), render: (r) => r.parameterName || "—" },
    { key: "limitTypeLabel", label: t("items.specifications.pendingColumns.limitType"), render: (r) => r.limitTypeLabel },
    {
      key: "lowerLimit",
      label: t("items.specifications.pendingColumns.lowerLimit"),
      sx: { fontVariantNumeric: "tabular-nums" },
      render: (r) => (r.lowerLimit !== null ? String(r.lowerLimit) : "—")
    },
    {
      key: "upperLimit",
      label: t("items.specifications.pendingColumns.upperLimit"),
      sx: { fontVariantNumeric: "tabular-nums" },
      render: (r) => (r.upperLimit !== null ? String(r.upperLimit) : "—")
    },
    {
      key: "target",
      label: t("items.specifications.pendingColumns.target"),
      sx: { fontVariantNumeric: "tabular-nums" },
      render: (r) => (r.target !== null ? String(r.target) : "—")
    },
    {
      key: "tolerance",
      label: t("items.specifications.pendingColumns.tolerance"),
      sx: { fontVariantNumeric: "tabular-nums" },
      render: (r) => (r.tolerance !== null ? String(r.tolerance) : "—")
    },
    {
      key: "toleranceModeLabel",
      label: t("items.specifications.pendingColumns.toleranceMode"),
      render: (r) => r.toleranceModeLabel || "—"
    },
    {
      key: "alertLimit",
      label: t("items.specifications.pendingColumns.alertLimit"),
      sx: { fontVariantNumeric: "tabular-nums" },
      render: (r) => r.alertLimit || "—"
    },
    {
      key: "actionLimit",
      label: t("items.specifications.pendingColumns.actionLimit"),
      sx: { fontVariantNumeric: "tabular-nums" },
      render: (r) => r.actionLimit || "—"
    },
    {
      key: "specLimit",
      label: t("items.specifications.pendingColumns.specLimit"),
      sx: { fontVariantNumeric: "tabular-nums" },
      render: (r) => r.specLimit || "—"
    },
    { key: "expectedResultText", label: t("items.specifications.pendingColumns.expectedResultText"), render: (r) => r.expectedResultText || "—" },
    { key: "unit", label: t("items.specifications.pendingColumns.unit"), render: (r) => r.unit || "—" }
  ];
}

export const ItemSpecificationsSection: React.FC<ItemSpecificationsSectionProps> = ({
  item,
  onSpecsChanged,
  readOnly = false
}) => {
  const { t } = useTranslation("labConfig");
  const theme = useTheme();
  const pendingColumns = useMemo(() => getSpecPendingColumns(t), [t]);

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

  // Deleting, reinstating and creating specifications are gated writes (CONF-227). The number the server
  // accepted is remembered for the specification being changed only (the batch counts as one session).
  const gate = useChangeControlGate({
    sectionId: null,
    recordKey: pendingDelete?.id != null
      ? `spec-${pendingDelete.id}`
      : pendingReinstate?.id != null
        ? `spec-${pendingReinstate.id}`
        : `item-${item.id}-specs`
  });
  const sectionOfSpec = (testCode: string) => testDefinitionByCode[testCode]?.sectionId ?? null;

  const saveSpecsBatch = async () => {
    if (pendingSpecs.length === 0) return;
    setSavingBatch(true);
    setBatchError(null);
    try {
      const payload = pendingSpecs.map((row) => toCreateSpecificationPayload(item.id, row));
      await gate.run((cc) => (cc ? SpecificationService.createSpecificationsBatch(payload, cc) : SpecificationService.createSpecificationsBatch(payload)));
      const count = pendingSpecs.length;
      setPendingSpecs([]);
      setNotice(
        t("items.specifications.messages.batchAdded", { count, itemName: item.name })
      );
      await loadSpecs();
      onSpecsChanged?.();
    } catch (e: unknown) {
      const { summary, rowErrors } = extractBatchSaveErrors(
        e,
        t("items.specifications.messages.batchSaveFailed")
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
          const specId = s.id;
          const res = await gate.run(
            (cc) => (cc ? SpecificationService.remove(specId, reason, cc) : SpecificationService.remove(specId, reason)),
            { sectionId: sectionOfSpec(s.testCode) }
          );
          if (res?.outcome === "retired") retiredCount += 1;
          else deletedCount += 1;
        }
      }
      setPendingDelete(null);
      if (retiredCount > 0 && deletedCount === 0) {
        setNotice(t("items.specifications.messages.retired", { label }));
      } else if (retiredCount > 0 && deletedCount > 0) {
        setNotice(t("items.specifications.messages.retiredAndDeleted", { label, retiredCount, deletedCount }));
      } else {
        setNotice(t("items.specifications.messages.deleted", { label }));
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
          t("items.specifications.messages.deleteFailed")
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
          const specId = s.id;
          await gate.run(
            (cc) => (cc ? SpecificationService.reinstate(specId, reason, cc) : SpecificationService.reinstate(specId, reason)),
            { sectionId: sectionOfSpec(s.testCode) }
          );
        }
      }
      setPendingReinstate(null);
      setNotice(t("items.specifications.messages.reinstated", { label }));
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
          t("items.specifications.messages.reinstateFailed")
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
            {t("items.specifications.heading", { count: specs.length })}
          </Typography>
          <Typography
            variant="caption"
            sx={{ color: "text.secondary", display: "block", mt: 0.25 }}
          >
            {t("items.specifications.subheading")}
          </Typography>
          {assignedTests.length > 0 && (
            <Typography
              variant="caption"
              sx={{ color: "text.secondary", display: "block" }}
            >
              {t("items.specifications.coverage", {
                set: assignedTests.filter((a) => specs.some((s) => s.testCode === a.testCode)).length,
                total: assignedTests.length
              })}
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
              {t("items.specifications.pasteButton")}
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
              {t("items.specifications.copyButton")}
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
              {t("items.specifications.addButton")}
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
              label={t("items.specifications.pasteLabel")}
              placeholder={t("items.specifications.pastePlaceholder")}
              value=""
              onChange={() => {}}
              onPaste={handlePasteInput}
              helperText={t("items.specifications.pasteHelper")}
            />
            <Button onClick={() => setPasteBarOpen(false)} sx={{ textTransform: "none", flexShrink: 0 }}>
              {t("items.specifications.close")}
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
        <LimitTypeBadge type="Range" />
        <LimitTypeBadge type="NotMoreThan" labelOverride={t("items.specifications.limitTypes.NotMoreLessLegend")} />
        <LimitTypeBadge type="TargetWithTolerance" />
        <LimitTypeBadge type="CountTiered" />
        <LimitTypeBadge type="Qualitative" />
        <LimitTypeBadge type="PresenceAbsence" />
        <LimitTypeBadge type="MultiStage" />
        <LimitTypeBadge type="DissolutionQ" />
        <LimitTypeBadge type="DisintegrationTime" />
        <LimitTypeBadge type="WeightVariation" />
      </Stack>

      {lookupsIncomplete && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {t("items.specifications.lookupsIncomplete")}
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
        title={t("items.specifications.pendingTitle", { count: pendingSpecs.length })}
        subtitle={t("items.specifications.pendingSubtitle", { count: pendingSpecs.length })}
        ariaLabel={t("items.specifications.pendingAriaLabel")}
        columns={pendingColumns}
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
          <Trans
            i18nKey="items.specifications.noAssignedTests"
            ns="labConfig"
            components={{ bold: <strong /> }}
          />
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
                    {t("items.specifications.columns.parameter")}
                  </TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 140 }}>
                    {t("items.specifications.columns.limitType")}
                  </TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 200 }}>
                    {t("items.specifications.columns.limit")}
                  </TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 100 }}>
                    {t("items.specifications.columns.unit")}
                  </TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 150 }}>
                    {t("items.specifications.columns.referenceStandard")}
                  </TableCell>
                  <TableCell align="right" sx={{ fontWeight: 700, fontSize: 12, width: 90 }}>
                    {t("items.specifications.columns.actions")}
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
                      {t("items.specifications.empty")}
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
                                    <Chip size="small" label={t("items.specifications.badges.retired")} color="warning" variant="outlined" sx={{ height: 20, fontSize: 12, fontWeight: 700 }} />
                                  )}
                                  {spec.grade && (
                                    <Chip size="small" label={t("items.specifications.badges.grade", { grades: gradesOf(spec).join(", ") })} sx={{ height: 20, fontSize: 12, fontWeight: 600, color: "primary.main", border: "1px solid", borderColor: "primary.main", bgcolor: "transparent" }} />
                                  )}
                                  {spec.productionStageRole && (
                                    <Chip
                                      size="small"
                                      label={t("items.specifications.badges.stage", { stage: productionStageRoleLabel(spec.productionStageRole) })}
                                      sx={{ height: 20, fontSize: 12, fontWeight: 600, color: "secondary.main", border: "1px solid", borderColor: "secondary.main", bgcolor: "transparent" }}
                                    />
                                  )}
                                  {spec.canEdit === false && (
                                    <Chip
                                      size="small"
                                      label={spec.sectionName || t("items.specifications.badges.otherLab")}
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
                                      label={t("items.specifications.badges.analyte", { name: hplcAnalyteById[spec.hplcMethodAnalyteId].name })}
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
                                      label={t("items.specifications.badges.element", { symbol: icpElementById[spec.icpTestElementId].symbol })}
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
                                      label={t("items.specifications.badges.basis", {
                                        basis: formatResultBasis(
                                          spec.resultBasis as string,
                                          workflowTypeByCode[spec.testCode] === "IcpMethodAssay" ||
                                            testDefinitionByCode[spec.testCode]?.equationType === "IcpMethodAssay",
                                          t
                                        )
                                      })}
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
                              {formatLimitCell(spec, t)}
                              {spec.limitType === "CountTiered" && spec.dilutionFactor != null && (
                                <Box sx={{ mt: 0.5 }}>
                                  <Chip
                                    size="small"
                                    label={t("items.specifications.badges.dilutionFactor", { factor: spec.dilutionFactor })}
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
                                    label={t("items.specifications.badges.labelClaim", { claim: spec.labelClaim, unit: spec.labelClaimUnit || "mg" })}
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
                                  {t("items.specifications.actions.readOnly")}
                                </Typography>
                              ) : spec.isRetired ? (
                                <Button
                                  size="small"
                                  variant="outlined"
                                  onClick={() => { setReasonError(null); setPendingReinstate(spec); }}
                                  sx={{ textTransform: "none", fontSize: 12, fontWeight: 600 }}
                                >
                                  {t("items.specifications.actions.reinstate")}
                                </Button>
                              ) : (
                                <Stack
                                  direction="row"
                                  spacing={0.5}
                                  sx={{ justifyContent: "flex-end" }}
                                >
                                  <IconButton aria-label={t("items.specifications.actions.editAriaLabel", { testCode: spec.testCode })}
                                    size="small"
                                    onClick={() => handleOpenEdit(spec)}
                                    title={t("items.specifications.actions.editTitle")}
                                  >
                                    <EditIcon fontSize="small" />
                                  </IconButton>
                                  <IconButton aria-label={t("items.specifications.actions.deleteAriaLabel", { testCode: spec.testCode })}
                                    size="small"
                                    color="error"
                                    onClick={() => { setReasonError(null); setPendingDelete(spec); }}
                                    title={t("items.specifications.actions.deleteTitle")}
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
                                label={t("items.specifications.badges.parameterCount", { count: group.specs.length })}
                                sx={{
                                  height: 20,
                                  fontSize: 12,
                                  bgcolor: "background.paper"
                                }}
                              />
                              {!groupCanEdit && (
                                <Chip
                                  size="small"
                                  label={groupSectionName || t("items.specifications.badges.otherLab")}
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
                                            <Chip size="small" label={t("items.specifications.badges.retired")} color="warning" variant="outlined" sx={{ height: 20, fontSize: 12, fontWeight: 700 }} />
                                          )}
                                          {spec.grade && (
                                            <Chip size="small" label={t("items.specifications.badges.grade", { grades: gradesOf(spec).join(", ") })} sx={{ height: 20, fontSize: 12, fontWeight: 600, color: "primary.main", border: "1px solid", borderColor: "primary.main", bgcolor: "transparent" }} />
                                          )}
                                          {spec.productionStageRole && (
                                            <Chip
                                              size="small"
                                              label={t("items.specifications.badges.stage", { stage: productionStageRoleLabel(spec.productionStageRole) })}
                                              sx={{ height: 20, fontSize: 12, fontWeight: 600, color: "secondary.main", border: "1px solid", borderColor: "secondary.main", bgcolor: "transparent" }}
                                            />
                                          )}
                                        
                                          {spec.hplcMethodAnalyteId && hplcAnalyteById[spec.hplcMethodAnalyteId] && (
                                            <Chip
                                              size="small"
                                              label={t("items.specifications.badges.analyte", { name: hplcAnalyteById[spec.hplcMethodAnalyteId].name })}
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
                                              label={t("items.specifications.badges.element", { symbol: icpElementById[spec.icpTestElementId].symbol })}
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
                                              label={t("items.specifications.badges.basis", {
                                                basis: formatResultBasis(
                                                  spec.resultBasis as string,
                                                  workflowTypeByCode[spec.testCode] === "IcpMethodAssay" ||
                                                    testDefinitionByCode[spec.testCode]?.equationType === "IcpMethodAssay",
                                                  t
                                                )
                                              })}
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
                                  {formatLimitCell(spec, t)}
                                  {spec.limitType === "CountTiered" &&
                                    spec.dilutionFactor != null && (
                                      <Box sx={{ mt: 0.5 }}>
                                        <Chip
                                          size="small"
                                          label={t("items.specifications.badges.dilutionFactor", { factor: spec.dilutionFactor })}
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
                                      {t("items.specifications.actions.readOnly")}
                                    </Typography>
                                  ) : spec.isRetired ? (
                                    <Button
                                      size="small"
                                      variant="outlined"
                                      onClick={() => { setReasonError(null); setPendingReinstate(spec); }}
                                      sx={{ textTransform: "none", fontSize: 12, fontWeight: 600 }}
                                    >
                                      {t("items.specifications.actions.reinstate")}
                                    </Button>
                                  ) : (
                                    <Stack
                                      direction="row"
                                      spacing={0.5}
                                      sx={{ justifyContent: "flex-end" }}
                                    >
                                      <IconButton aria-label={t("items.specifications.actions.editAriaLabel", { testCode: spec.testCode })}
                                        size="small"
                                        onClick={() => handleOpenEdit(spec)}
                                        title={t("items.specifications.actions.editTitle")}
                                      >
                                        <EditIcon fontSize="small" />
                                      </IconButton>
                                      <IconButton aria-label={t("items.specifications.actions.deleteAriaLabel", { testCode: spec.testCode })}
                                        size="small"
                                        color="error"
                                        onClick={() => { setReasonError(null); setPendingDelete(spec); }}
                                        title={t("items.specifications.actions.deleteTitle")}
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
                                {t("items.specifications.actions.addParameterTo", { testName: testDisplayName })}
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

      {/* Add / Edit Parameter Wizard */}
      <SpecificationWizard
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
            setNotice(t("items.specifications.messages.copied", { count, itemName: item.name }));
          }}
        />
      )}

      {/* Delete / Retire Change Reason Dialog */}
      <ChangeReasonDialog
        open={pendingDelete != null}
        title={t("items.specifications.reasonDialogs.deleteTitle")}
        message={
          pendingDelete
            ? t("items.specifications.reasonDialogs.deleteMessage", {
                label: pendingDelete.parameterName || pendingDelete.testCode,
                gradesText: gradesOf(pendingDelete).length > 1
                  ? t("items.specifications.reasonDialogs.gradesSuffix", { grades: gradesOf(pendingDelete).join(", ") })
                  : "",
                itemName: item.name,
              })
            : undefined
        }
        confirmText={t("items.specifications.reasonDialogs.confirm")}
        loading={deleting}
        error={reasonError}
        onCancel={() => { setPendingDelete(null); setReasonError(null); }}
        onConfirm={handleDeleteConfirm}
      />

      {/* Reinstate Change Reason Dialog */}
      <ChangeReasonDialog
        open={pendingReinstate != null}
        title={t("items.specifications.reasonDialogs.reinstateTitle")}
        message={
          pendingReinstate
            ? t("items.specifications.reasonDialogs.reinstateMessage", {
                label: pendingReinstate.parameterName || pendingReinstate.testCode,
                itemName: item.name,
              })
            : undefined
        }
        confirmText={t("items.specifications.reasonDialogs.reinstateConfirm")}
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
      {gate.prompt}
    </Box>
  );
};
