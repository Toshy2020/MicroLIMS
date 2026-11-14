import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import { useSearchParams } from "react-router-dom";
import {
  Box,
  Button,
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TablePagination,
  Typography,
  Chip,
  Alert,
  Snackbar,
  LinearProgress,
  useTheme,
  TableContainer
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";

import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { TableSkeleton } from "../../components/TableSkeleton";
import { AuditHistoryDialog } from "../../components/AuditHistoryDialog";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { tableHeadSx } from "../../theme";
import { getTable } from "../../components/lab/table/tableRegistry";
import { decodeState, encodeState, isOwnedParam, type TableState } from "../../components/lab/table/tableUrlCodec";
import { useRowSelection } from "../../components/lab/table/useRowSelection";
import { useTableState } from "../../components/lab/table/useTableState";
import type { BulkAction } from "../../components/lab/table/BulkActionBar";
import { clientSettingsService } from "../../services/clientSettingsService";
import { fetchAllPages } from "../../services/fetchAllPages";
import type { LabelTarget } from "../../services/labelService";
import { getErrorMessage, serverMessage } from "../../utils/errorMessage";

// Receiving Components & Dialogs
import { CreatedSampleRef, SampleRecord, SampleCategoryKey, TestOrderSummary as ReceivingTestOrderSummary } from "../receiving/types/receivingTypes";
import { ReceiveService, TestingWorkspaceFilter, WorkspaceTileCounts } from "../receiving/services/ReceiveService";
import {
  SampleStatusKpiCards,
  WorkloadFilterKey,
  AllWorkloadFilterKey
} from "../receiving/components/SampleStatusKpiCards";
import { SelectionSummaryBar } from "../receiving/components/SelectionSummaryBar";
import { SampleFilterBar } from "../receiving/components/SampleFilterBar";
import { SampleRegisterTable, WORKSPACE_REGISTER_DEFAULTS } from "../receiving/components/SampleRegisterTable";
import { NewSampleDialog } from "../receiving/dialogs/NewSampleDialog";
import { EditSampleDetailsDialog } from "../receiving/dialogs/EditSampleDetailsDialog";
import { AssignAnalystDialog } from "../receiving/dialogs/AssignAnalystDialog";
import { BulkAssignDialog, bulkAssignAction } from "../receiving/dialogs/BulkAssignDialog";
import { assignedAnalystNames } from "../receiving/utils/assignedAnalysts";

// Testing Workspace Components & Dialogs
import { SampleCard as WorkspaceSampleCard, TestOrderSummary as WorkspaceTestOrderSummary } from "../testingWorkspace/types/workspaceTypes";
import { SampleTableRow } from "../testingWorkspace/SampleTableRow";
import { SampleCardView } from "../testingWorkspace/SampleCardView";
import { SampleKanbanView } from "../testingWorkspace/SampleKanbanView";
import { SelectedSampleTestingPanel } from "../testingWorkspace/SelectedSampleTestingPanel";
import { GroupedActionsPanel } from "../testingWorkspace/components/GroupedActionsPanel";
import { TestWorkflowDialogRouter } from "../testingWorkspace/FloatingDialogs";
import { SampleSummaryDialog } from "../testingWorkspace/SampleSummaryDialog";
import { PreparationDialog } from "../testPreparation/PreparationDialog";
import { VoidSampleConfirmationDialog } from "../receiving/dialogs/VoidSampleConfirmationDialog";
import { PrintLabelsDialog } from "../../components/labels/PrintLabelsDialog";
import { usePostCreateLabelPrint } from "../../components/labels/usePostCreateLabelPrint";
import { toast } from "sonner";
import {
  LEGACY_FILTER_PARAMS,
  WORKSPACE_SERVER_SORTABLE_COLUMNS,
  isWhitelistedWorkload,
  legacyDisplayMode,
  readLegacyWorkspaceLink
} from "./workspaceTableState";

const MAX_BULK_ASSIGN = 200;

export type WorkspaceDisplayView = "table" | "card" | "kanban";

export interface WorkspaceLab {
  sectionId: number;
  code: "MICRO" | "FP";
  name: string;
  area?: "fp" | "rmpm";
}

// Microbiology keeps all six categories it has today.
// For Physicochemical: area fp gives ["product", "ac"], area rmpm gives ["rm", "pm", "water"].
export function getAllowedCategories(lab: WorkspaceLab): SampleCategoryKey[] {
  if (lab.code === "MICRO") {
    return ["product", "rm", "pm", "water", "em", "ac"];
  }
  if (lab.area === "rmpm") {
    return ["rm", "pm", "water"];
  }
  return ["product", "ac"];
}

interface Props {
  // Which laboratory this workspace instance is scoped to - resolved by
  // LabWorkspaceRoute from the caller's own lab membership. Every list/count
  // request is narrowed to this section, and a user who isn't a member of
  // it gets a 403 from the backend rather than another lab's data.
  lab: WorkspaceLab;
}

// A sample opened from a link or notification that is not on the current
// page is fetched on its own; if that fails the workspace used to open with
// nothing selected and no explanation. Both lookups below can fail for the
// same sample, so the toast id keeps it to one message.
function notifySampleLoadFailed(sampleId: number) {
  toast.error(`Sample #${sampleId} could not be opened. It may not belong to this laboratory, or the server could not be reached.`, {
    id: `workspace-sample-${sampleId}`
  });
}

type WorkspaceDeepLinkOpen = "step" | "incubation" | "review" | "approval" | "assign";

function isWorkspaceDeepLinkOpen(value: string): value is WorkspaceDeepLinkOpen {
  return (
    value === "step" ||
    value === "incubation" ||
    value === "review" ||
    value === "approval" ||
    value === "assign"
  );
}

const CLOSED_SAMPLE_STATUSES = new Set(["Approved", "Rejected", "Voided", "Cancelled"]);
const POST_STEP_TEST_STATUSES = new Set([
  "UnderReview",
  "Reviewed",
  "Approved",
  "Rejected",
  "Voided",
  "Cancelled",
  "Superseded",
  "OnHold",
  "Completed"
]);
const POST_STEP_WORKFLOW_STATES = new Set([
  "APPROVED",
  "REVIEWED",
  "RESULTS_RECORDED",
  "REJECTED",
  "VOIDED",
  "CANCELLED",
  "SUPERSEDED",
  "ON_HOLD"
]);

type DeepLinkEvaluation =
  | { eligible: true; action: WorkspaceDeepLinkOpen; test?: ReceivingTestOrderSummary }
  | { eligible: false; reason: string };

function isTestPastStep(test: ReceivingTestOrderSummary): boolean {
  if (POST_STEP_TEST_STATUSES.has(test.status)) return true;
  if (test.workflowStatus && POST_STEP_TEST_STATUSES.has(test.workflowStatus)) return true;
  if (test.workflowState && POST_STEP_WORKFLOW_STATES.has(test.workflowState)) return true;
  return false;
}

function evaluateWorkspaceDeepLinkOpen(
  action: WorkspaceDeepLinkOpen,
  sample: SampleRecord,
  testOrderId: number | null,
  permissions: string[]
): DeepLinkEvaluation {
  if (action === "step" || action === "incubation") {
    const foundTest = testOrderId
      ? sample.assignedTests?.find((t) => t.testOrderId === testOrderId)
      : undefined;
    if (!foundTest) {
      return { eligible: false, reason: "The requested test order could not be found on this sample." };
    }
    if (CLOSED_SAMPLE_STATUSES.has(sample.status) || isTestPastStep(foundTest)) {
      return {
        eligible: false,
        reason:
          action === "incubation"
            ? "This test is no longer waiting for an incubation read."
            : "This test is no longer waiting for this step."
      };
    }
    if (
      action === "incubation" &&
      foundTest.status === "Pending" &&
      (!foundTest.workflowStatus || foundTest.workflowStatus === "Pending")
    ) {
      return { eligible: false, reason: "This test is no longer waiting for an incubation read." };
    }
    if (sample.preparationStatus === "NeedsPreparation" && !foundTest.skipsPreparation) {
      return {
        eligible: false,
        reason:
          action === "incubation"
            ? "This sample must be prepared before performing incubation reads."
            : "This sample must be prepared before performing test steps."
      };
    }
    return { eligible: true, action, test: foundTest };
  }

  if (action === "review") {
    if (!permissions.includes(PERMISSIONS.SAMPLES_REVIEW)) {
      return { eligible: false, reason: "You do not have permission to review this sample." };
    }
    if (sample.status !== "UnderReview") {
      return { eligible: false, reason: "This sample is no longer waiting for review." };
    }
    return { eligible: true, action };
  }

  if (action === "approval") {
    if (!permissions.includes(PERMISSIONS.SAMPLES_APPROVE)) {
      return { eligible: false, reason: "You do not have permission to approve this sample." };
    }
    if (sample.status !== "UnderApproval") {
      return { eligible: false, reason: "This sample is no longer waiting for approval." };
    }
    return { eligible: true, action };
  }

  if (!permissions.includes(PERMISSIONS.SAMPLES_ASSIGN_ANALYST)) {
    return { eligible: false, reason: "You do not have permission to assign analysts." };
  }
  if (CLOSED_SAMPLE_STATUSES.has(sample.status)) {
    return { eligible: false, reason: "Analysts cannot be assigned to a closed sample." };
  }
  if (testOrderId) {
    const foundTest = sample.assignedTests?.find((t) => t.testOrderId === testOrderId);
    if (foundTest && CLOSED_SAMPLE_STATUSES.has(foundTest.status)) {
      return { eligible: false, reason: "This test is closed and can no longer be assigned." };
    }
  }
  return { eligible: true, action };
}

export function ReceivingTestingWorkspacePage({ lab }: Props) {
  const theme = useTheme();
  const { permissions } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();

  // Core Data State
  const tableKey = lab.code === "MICRO" ? "samples.workspace.micro" : "samples.workspace.fp";
  const tableState = useTableState(tableKey, WORKSPACE_REGISTER_DEFAULTS);
  const [records, setRecords] = useState<SampleRecord[] | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [workloadCounts, setWorkloadCounts] = useState<WorkspaceTileCounts | null>(null);
  const [loading, setLoading] = useState(false);
  const [notification, setNotification] = useState<{ text: string; severity: "success" | "error" | "info" | "warning" } | null>(null);

  // The address holds the filters, sort and page. Links that still use the
  // old parameters are rewritten once, before the first list request, so the
  // list is never fetched with the wrong filters.
  const [legacyReady, setLegacyReady] = useState(() => readLegacyWorkspaceLink(searchParams) === null && legacyDisplayMode(searchParams) === null);
  const legacyHandledRef = useRef(false);
  useEffect(() => {
    if (legacyHandledRef.current) return;
    legacyHandledRef.current = true;
    const legacy = readLegacyWorkspaceLink(searchParams);
    if (legacy || legacyDisplayMode(searchParams) !== null) {
      const def = getTable(tableKey);
      const legacyKeys: readonly string[] = LEGACY_FILTER_PARAMS;
      setSearchParams(
        (prev) => {
          const decoded = decodeState(def, prev);
          const nextState: TableState = {
            filters: { ...(decoded.state.filters ?? {}), ...(legacy?.filters ?? {}) },
            search: legacy?.search ?? decoded.state.search ?? "",
            sort: decoded.state.sort ?? null,
            pageSize: legacy?.pageSize ?? decoded.state.pageSize ?? 50,
            columns: [],
            density: "compact"
          };
          const next = new URLSearchParams();
          prev.forEach((value, key) => {
            if (!isOwnedParam(key) && !legacyKeys.includes(key)) next.append(key, value);
          });
          encodeState(def, nextState, decoded.page, decoded.viewId, { defaults: WORKSPACE_REGISTER_DEFAULTS }).forEach((value, key) => {
            next.append(key, value);
          });
          return next;
        },
        { replace: true }
      );
    }
    setLegacyReady(true);
  }, [searchParams, setSearchParams, tableKey]);

  // Filter, search, page and page size come from the table state.
  const filterValue = (name: string): string => {
    const v = tableState.state.filters[name];
    return typeof v === "string" ? v : "";
  };
  const page = tableState.page;
  const pageSize = tableState.state.pageSize === "all" ? 100 : tableState.state.pageSize;
  const debouncedSearch = tableState.state.search;
  const categoryFilter = filterValue("category") || "ALL";
  const sampleStatusFilter = filterValue("sampleStatus") || "ALL";
  const testStatusFilter = filterValue("testStatus") || "ALL";
  const analystIdValue = Number(filterValue("analystId"));
  const analystIdFilter: number | null = filterValue("analystId") && !Number.isNaN(analystIdValue) ? analystIdValue : null;
  const urgencyFilter = filterValue("urgency");
  const fromDate = filterValue("fromDate");
  const toDate = filterValue("toDate");
  const workloadValue = filterValue("workload");
  const workloadFilter: AllWorkloadFilterKey | null = isWhitelistedWorkload(workloadValue) ? workloadValue : null;
  const setFilter = tableState.setFilter;

  // Display View State
  const [viewMode, setViewMode] = useState<WorkspaceDisplayView>(() => legacyDisplayMode(searchParams) ?? "table");

  // Selection for Master-Detail Split Pane
  const [selectedSampleId, setSelectedSampleId] = useState<number | null>(() => {
    const s = searchParams.get("sampleId");
    return s ? Number(s) : null;
  });
  const [extraSelectedSample, setExtraSelectedSample] = useState<SampleRecord | null>(null);

  // Multi-select Sample Checkboxes for Grouped Actions. The selection is
  // deliberately not reset by a filter or page change: a checked sample that
  // the filters push out of view stays checked and is reported as hidden.
  const selection = useRowSelection<number>("workspace");
  const checkedSampleIds = selection.selected;
  const checkedSamplesCache = useRef<Map<number, SampleRecord>>(new Map());
  const requestedCheckedRef = useRef<Set<number>>(new Set());

  // Reveals every checked sample regardless of the active filters, so nobody
  // signs a grouped action over a set they cannot see in full.
  const [showSelectedOnly, setShowSelectedOnly] = useState(false);

  // Whatever way the selection is emptied (Deselect All, Clear, a finished
  // bulk assignment), the selected-only view ends with it.
  useEffect(() => {
    if (checkedSampleIds.size === 0 && !selection.allMatching && showSelectedOnly) {
      setShowSelectedOnly(false);
    }
  }, [checkedSampleIds, selection.allMatching, showSelectedOnly]);

  // Keeps the full sample of every checked row, even after a filter or page
  // change takes it off the list, and drops the ones no longer checked.
  useEffect(() => {
    const cache = checkedSamplesCache.current;
    for (const id of [...cache.keys()]) {
      if (!checkedSampleIds.has(id)) cache.delete(id);
    }
    for (const id of [...requestedCheckedRef.current]) {
      if (!checkedSampleIds.has(id)) requestedCheckedRef.current.delete(id);
    }
    for (const id of checkedSampleIds) {
      if (cache.has(id)) continue;
      const known = records?.find((r) => r.sampleId === id) ||
        (extraSelectedSample?.sampleId === id ? extraSelectedSample : null);
      if (known) {
        cache.set(id, known);
      } else if (!requestedCheckedRef.current.has(id)) {
        requestedCheckedRef.current.add(id);
        ReceiveService.getSample(id, lab.sectionId, lab.area).then((fetched) => {
          if (fetched) cache.set(id, fetched);
        // Cache warm-up only; the selection itself is already recorded.
        }).catch(() => {});
      }
    }
  }, [checkedSampleIds, records, extraSelectedSample, lab.sectionId, lab.area]);

  // A compact-register checkbox (the grouped-actions layout) ticks or unticks one sample.
  const handleToggleCheckSample = (sampleId: number, checked: boolean) => {
    if (checked !== selection.selected.has(sampleId)) selection.toggle(sampleId);
  };

  // Selects exactly these samples (the ?sampleIds= link).
  const { clear: clearSelection, togglePage: toggleSelectionPage } = selection;
  const selectExactly = useCallback((ids: number[]) => {
    clearSelection();
    toggleSelectionPage(ids);
  }, [clearSelection, toggleSelectionPage]);

  // The search box keeps what is typed; the table state (and the address) get it 300 ms after typing stops.
  const [search, setSearch] = useState(debouncedSearch);
  useEffect(() => {
    setSearch(debouncedSearch);
  }, [debouncedSearch]);
  const setTableSearch = tableState.setSearch;
  useEffect(() => {
    if (search === debouncedSearch) return;
    const timer = setTimeout(() => setTableSearch(search), 300);
    return () => clearTimeout(timer);
  }, [search, debouncedSearch, setTableSearch]);

  // Dialogs State
  const [newSampleDialogOpen, setNewSampleDialogOpen] = useState(false);
  const [editSample, setEditSample] = useState<SampleRecord | null>(null);
  const [assigningSample, setAssigningSample] = useState<SampleRecord | null>(null);
  const [bulkAssignOpen, setBulkAssignOpen] = useState(false);
  const [bulkAssignIds, setBulkAssignIds] = useState<number[]>([]);
  const [labelTargets, setLabelTargets] = useState<LabelTarget[]>([]);
  const [labelsDialogOpen, setLabelsDialogOpen] = useState(false);
  const [labelsEnabled, setLabelsEnabled] = useState(false);
  const [summarySampleId, setSummarySampleId] = useState<number | null>(null);
  const [activeTest, setActiveTest] = useState<WorkspaceTestOrderSummary | null>(null);
  const [activeSampleForTest, setActiveSampleForTest] = useState<WorkspaceSampleCard | null>(null);
  const [preparingSample, setPreparingSample] = useState<SampleRecord | null>(null);
  const [auditSampleId, setAuditSampleId] = useState<number | null>(null);
  const [voidingSample, setVoidingSample] = useState<SampleRecord | null>(null);
  const postCreatePrint = usePostCreateLabelPrint();

  const processedDeepLinkKeyRef = useRef<string | null>(null);
  const processedOpenKeyRef = useRef<string | null>(null);
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const activeSort =
    tableState.state.sort && WORKSPACE_SERVER_SORTABLE_COLUMNS.has(tableState.state.sort.key)
      ? tableState.state.sort
      : null;
  const sortBy = activeSort?.key;
  const sortDir = activeSort?.direction;

  const filterRef = useRef<TestingWorkspaceFilter>({});
  filterRef.current = {
    page,
    pageSize,
    search: debouncedSearch,
    category: categoryFilter,
    sampleStatus: sampleStatusFilter,
    testStatus: testStatusFilter,
    analystId: analystIdFilter,
    urgency: urgencyFilter,
    fromDate,
    toDate,
    workloadFilter,
    labSectionId: lab.sectionId,
    area: lab.area,
    ...(sortBy && sortDir ? { sortBy, sortDir } : {})
  };

  // The workload tile counts don't depend on the filters or page, so filter
  // and page changes skip them; first load, Refresh and every reload after a
  // change to the data still fetch them.
  const loadRecords = async (silent = false, includeCounts = true) => {
    if (!silent) setLoading(true);
    try {
      const currentFilter = filterRef.current;
      const [pagedData, countsData] = await Promise.all([
        ReceiveService.getRecordsPaged(currentFilter),
        includeCounts ? ReceiveService.getWorkloadCounts(lab.sectionId, lab.area) : Promise.resolve(null)
      ]);
      setRecords(pagedData.items);
      setTotalCount(pagedData.totalCount);
      if (countsData) setWorkloadCounts(countsData);

      // If a sample is selected, refresh its details if present in the reloaded page
      if (selectedSampleId) {
        const matching = pagedData.items.find((s) => s.sampleId === selectedSampleId);
        if (matching) {
          setExtraSelectedSample(matching);
        }
      }
    } catch (err: unknown) {
      setNotification({
        text: serverMessage(err) || "Failed to load laboratory sample records.",
        severity: "error"
      });
    } finally {
      if (!silent) setLoading(false);
    }
  };

  // Re-fetch whenever page, pageSize, or any filter changes
  useEffect(() => {
    if (!legacyReady) return;
    loadRecords(false, workloadCounts === null);
  }, [
    legacyReady,
    page,
    pageSize,
    debouncedSearch,
    workloadFilter,
    categoryFilter,
    sampleStatusFilter,
    testStatusFilter,
    analystIdFilter,
    urgencyFilter,
    fromDate,
    toDate,
    sortBy,
    sortDir
  ]);

  const handleTestClick = useCallback((test: ReceivingTestOrderSummary | WorkspaceTestOrderSummary, sample: SampleRecord | WorkspaceSampleCard) => {
    setActiveTest(test as unknown as WorkspaceTestOrderSummary);
    setActiveSampleForTest(sample as unknown as WorkspaceSampleCard);
  }, []);

  const handleViewSummary = useCallback((sample: SampleRecord | WorkspaceSampleCard) => {
    setSummarySampleId(sample.sampleId);
  }, []);

  const handleAssignAnalyst = useCallback((sample: SampleRecord) => {
    setAssigningSample(sample);
  }, []);

  const clearOpenParams = useCallback(() => {
    setSearchParams(
      (prev) => {
        if (!prev.has("open") && !prev.has("incubationId") && !prev.has("testOrderId")) return prev;
        const next = new URLSearchParams(prev);
        next.delete("open");
        next.delete("incubationId");
        next.delete("testOrderId");
        return next;
      },
      { replace: true }
    );
  }, [setSearchParams]);

  const applyWorkspaceOpenAction = useCallback((
    action: WorkspaceDeepLinkOpen,
    sample: SampleRecord,
    testOrderId: number | null
  ) => {
    const result = evaluateWorkspaceDeepLinkOpen(action, sample, testOrderId, permissions);
    if (!result.eligible) {
      setNotification({ text: result.reason, severity: "warning" });
      return;
    }

    if (result.action === "step" || result.action === "incubation") {
      if (result.test) {
        handleTestClick(result.test, sample);
      }
      return;
    }

    if (result.action === "review" || result.action === "approval") {
      handleViewSummary(sample);
      return;
    }

    handleAssignAnalyst(sample);
  }, [permissions, handleTestClick, handleViewSummary, handleAssignAnalyst]);

  // Deep-Link URL Parameter Processing (Strict Backward Compatibility)
  useEffect(() => {
    const paramSampleId = searchParams.get("sampleId");
    const paramTestOrderId = searchParams.get("testOrderId");
    const paramOpenSummary = searchParams.get("openSummary");
    const paramOpen = searchParams.get("open");
    const paramIncubationId = searchParams.get("incubationId");

    const sId = paramSampleId ? Number(paramSampleId) : null;
    const tId = paramTestOrderId ? Number(paramTestOrderId) : null;
    const currentDeepLinkKey = `${paramSampleId ?? ""}:${paramTestOrderId ?? ""}:${paramOpenSummary ?? ""}`;
    const clearedDeepLinkKey = `${paramSampleId ?? ""}::${paramOpenSummary ?? ""}`;

    if (paramOpen !== null) {
      if (!isWorkspaceDeepLinkOpen(paramOpen)) {
        processedDeepLinkKeyRef.current = clearedDeepLinkKey;
        if (sId) setSelectedSampleId(sId);
        clearOpenParams();
        return;
      }

      const openKey = `${paramSampleId ?? ""}:${paramTestOrderId ?? ""}:${paramOpen}:${paramIncubationId ?? ""}`;
      if (processedOpenKeyRef.current !== openKey) {
        if (!sId) {
          if (tId && records === null) {
            return;
          }
          processedOpenKeyRef.current = openKey;
          processedDeepLinkKeyRef.current = clearedDeepLinkKey;
          const byTest = tId
            ? records?.find((sample) => sample.assignedTests?.some((t) => t.testOrderId === tId))
            : undefined;
          if (byTest) {
            setSelectedSampleId(byTest.sampleId);
            setExtraSelectedSample(byTest);
            applyWorkspaceOpenAction(paramOpen, byTest, tId);
          } else {
            setNotification({ text: "The requested sample could not be found.", severity: "warning" });
          }
          clearOpenParams();
          return;
        }

        processedOpenKeyRef.current = openKey;
        processedDeepLinkKeyRef.current = clearedDeepLinkKey;
        setSelectedSampleId(sId);

        const foundInRecords = records?.find((s) => s.sampleId === sId);
        const resolvedSample =
          foundInRecords ?? (extraSelectedSample?.sampleId === sId ? extraSelectedSample : null);

        if (resolvedSample) {
          setExtraSelectedSample(resolvedSample);
          applyWorkspaceOpenAction(paramOpen, resolvedSample, tId);
          clearOpenParams();
        } else {
          ReceiveService.getSample(sId, lab.sectionId, lab.area)
            .then((fetched) => {
              if (!mountedRef.current) return;
              if (fetched) {
                setExtraSelectedSample(fetched);
                applyWorkspaceOpenAction(paramOpen, fetched, tId);
              } else {
                notifySampleLoadFailed(sId);
              }
              clearOpenParams();
            })
            .catch(() => {
              if (!mountedRef.current) return;
              notifySampleLoadFailed(sId);
              clearOpenParams();
            });
        }
      }
      return;
    }

    processedOpenKeyRef.current = null;

    if (processedDeepLinkKeyRef.current === currentDeepLinkKey) {
      return;
    }
    processedDeepLinkKeyRef.current = currentDeepLinkKey;

    if (sId) {
      setSelectedSampleId(sId);
      if (paramOpenSummary === "true") {
        setSummarySampleId(sId);
      }

      const foundInRecords = records?.find((s) => s.sampleId === sId);
      if (foundInRecords) {
        setExtraSelectedSample(foundInRecords);
        if (tId) {
          const foundTest = foundInRecords.assignedTests?.find((t) => t.testOrderId === tId);
          if (foundTest) {
            setActiveTest(foundTest as unknown as WorkspaceTestOrderSummary);
            setActiveSampleForTest(foundInRecords as unknown as WorkspaceSampleCard);
          }
        }
      } else {
        ReceiveService.getSample(sId, lab.sectionId, lab.area).then((fetched) => {
          if (fetched) {
            setExtraSelectedSample(fetched);
            if (tId) {
              const foundTest = fetched.assignedTests?.find((t) => t.testOrderId === tId);
              if (foundTest) {
                setActiveTest(foundTest as unknown as WorkspaceTestOrderSummary);
                setActiveSampleForTest(fetched as unknown as WorkspaceSampleCard);
              }
            }
          }
        }).catch(() => notifySampleLoadFailed(sId));
      }
    } else if (tId && records) {
      for (const sample of records) {
        const foundTest = sample.assignedTests?.find((t) => t.testOrderId === tId);
        if (foundTest) {
          setSelectedSampleId(sample.sampleId);
          setActiveTest(foundTest as unknown as WorkspaceTestOrderSummary);
          setActiveSampleForTest(sample as unknown as WorkspaceSampleCard);
          break;
        }
      }
    }

    const paramSampleIds = searchParams.get("sampleIds");
    if (paramSampleIds) {
      const ids = paramSampleIds.split(",").map(Number).filter((id) => !isNaN(id) && id > 0);
      if (ids.length >= 2) {
        selectExactly(ids);
      }
    }
  }, [
    records,
    extraSelectedSample,
    searchParams,
    lab.sectionId,
    lab.area,
    clearOpenParams,
    applyWorkspaceOpenAction,
    selectExactly
  ]);

  // Ensure selectedSampleId always resolves to a full sample even if not on page 1
  useEffect(() => {
    if (!selectedSampleId) return;
    const inRecords = records?.find((s) => s.sampleId === selectedSampleId);
    if (inRecords) {
      setExtraSelectedSample(inRecords);
      return;
    }
    if (extraSelectedSample?.sampleId === selectedSampleId) return;
    ReceiveService.getSample(selectedSampleId, lab.sectionId, lab.area).then((sample) => {
      if (sample) setExtraSelectedSample(sample);
    }).catch(() => notifySampleLoadFailed(selectedSampleId));
  }, [selectedSampleId, records, extraSelectedSample, lab.sectionId, lab.area]);

  // Workload tiles are a toggle: clicking the active one clears it.
  const handleSelectWorkload = (key: WorkloadFilterKey) => {
    setFilter("workload", workloadFilter === key ? null : key);
  };

  const handleCategoryFilterChange = (cat: string) => {
    setFilter("category", cat === "ALL" ? null : cat);
  };

  const handleSampleStatusFilterChange = (status: string) => {
    setFilter("sampleStatus", status === "ALL" ? null : status);
  };

  const handleTestStatusFilterChange = (ts: string) => {
    setFilter("testStatus", ts === "ALL" ? null : ts);
  };

  const handleFromDateChange = (from: string) => {
    setFilter("fromDate", from || null);
  };

  const handleToDateChange = (to: string) => {
    setFilter("toDate", to || null);
  };

  const applyTableState = tableState.applyState;
  const handleResetFilters = () => {
    setSearch("");
    setShowSelectedOnly(false);
    applyTableState(
      { filters: {}, search: "", sort: tableState.state.sort, pageSize: tableState.state.pageSize },
      tableState.viewId
    );
  };

  const hasActiveFilters = Boolean(
    search ||
    workloadFilter ||
    categoryFilter !== "ALL" ||
    sampleStatusFilter !== "ALL" ||
    testStatusFilter !== "ALL" ||
    analystIdFilter !== null ||
    urgencyFilter ||
    fromDate ||
    toDate
  );

  // Filtered/Displayed Samples Computation:
  // When showSelectedOnly is true, surface all checked samples from cache/records.
  // Otherwise, display server-filtered and paged records.
  const displayRecords = useMemo(() => {
    if (showSelectedOnly) {
      const list: SampleRecord[] = [];
      for (const id of checkedSampleIds) {
        const cached = checkedSamplesCache.current.get(id);
        if (cached) {
          list.push(cached);
        } else {
          const inRecords = records?.find((r) => r.sampleId === id);
          if (inRecords) {
            list.push(inRecords);
            checkedSamplesCache.current.set(id, inRecords);
          }
        }
      }
      return list;
    }
    return records || [];
  }, [showSelectedOnly, checkedSampleIds, records]);

  // Checked samples the current filters/page have pushed out of view.
  const hiddenCheckedCount = useMemo(() => {
    if (checkedSampleIds.size === 0) return 0;
    if (showSelectedOnly) return 0;
    const visible = new Set((records || []).map((r) => r.sampleId));
    let hidden = 0;
    checkedSampleIds.forEach((id) => {
      if (!visible.has(id)) hidden += 1;
    });
    return hidden;
  }, [checkedSampleIds, records, showSelectedOnly]);

  // Derive Selected Sample Object
  const selectedSample = useMemo(() => {
    if (!selectedSampleId) return null;
    if (records) {
      const found = records.find((s) => s.sampleId === selectedSampleId);
      if (found) return found;
    }
    if (extraSelectedSample && extraSelectedSample.sampleId === selectedSampleId) {
      return extraSelectedSample;
    }
    if (checkedSamplesCache.current.has(selectedSampleId)) {
      return checkedSamplesCache.current.get(selectedSampleId) || null;
    }
    return null;
  }, [selectedSampleId, records, extraSelectedSample]);

  // Every sample matching the current filters, for "select all that match".
  const loadAllMatching = useCallback(
    () => fetchAllPages((p, size) => ReceiveService.getRecordsPaged({ ...filterRef.current, page: p, pageSize: size })),
    []
  );

  // Whether labels can be printed comes from the server's client settings.
  useEffect(() => {
    const controller = new AbortController();
    clientSettingsService.get(controller.signal).then((settings) => {
      if (!controller.signal.aborted) setLabelsEnabled(settings.features.includes("labels"));
    // Without the setting the label action simply stays off.
    }).catch(() => {});
    return () => controller.abort();
  }, []);

  const openBulkAssign = (ids: number[]) => {
    setBulkAssignIds(ids);
    setBulkAssignOpen(true);
  };

  // The samples a bulk action acts on: the checked ones, or every match when
  // "select all that match" was used.
  const samplesForBulk = async (ids: number[]): Promise<SampleRecord[]> => {
    if (selection.allMatching) return loadAllMatching();
    const found: SampleRecord[] = [];
    for (const id of ids) {
      const known = records?.find((r) => r.sampleId === id) ?? checkedSamplesCache.current.get(id);
      const sample = known ?? (await ReceiveService.getSample(id, lab.sectionId, lab.area));
      if (sample) found.push(sample);
    }
    return found;
  };

  const bulkActions: BulkAction<string | number>[] = [
    bulkAssignAction((ids) => {
      void (async () => {
        try {
          const resolved = selection.allMatching ? (await loadAllMatching()).map((sample) => sample.sampleId) : ids;
          if (resolved.length > MAX_BULK_ASSIGN) {
            toast.error(`Bulk assignment is limited to ${MAX_BULK_ASSIGN} samples. Narrow your filters.`);
            return;
          }
          openBulkAssign(resolved);
        } catch (err) {
          toast.error(getErrorMessage(err, "Could not load the matching samples."));
        }
      })();
    }),
    ...(labelsEnabled
      ? [{
          key: "print-labels",
          label: "Print labels",
          run: async (ids: Array<string | number>) => {
            try {
              const samples = await samplesForBulk(ids.map(Number));
              setLabelTargets(samples.map((sample) => ({
                entityType: "Sample" as const,
                entityId: sample.sampleId,
                code: sample.referenceNumber,
                displayName: sample.displayName
              })));
              setLabelsDialogOpen(true);
            } catch (err) {
              toast.error(getErrorMessage(err, "Could not load the samples to print labels for."));
            }
          }
        }]
      : [])
  ];

  // The full-width table, with its own bulk bar, is what is on screen.
  const tableBulkBarVisible = Boolean(records) && viewMode === "table" && checkedSampleIds.size < 2 && !selectedSample;

  const handlePageChange = (_: unknown, newPage: number) => {
    tableState.setPage(newPage + 1);
  };

  // The table state holds 25, 50 or 100 rows a page (the server never returns more than 100).
  const handleRowsPerPageChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const n = parseInt(event.target.value, 10);
    tableState.setPageSize(n >= 100 ? 100 : n >= 50 ? 50 : 25);
  };

  // Event Handlers
  const handleSelectSample = (sample: SampleRecord | WorkspaceSampleCard) => {
    setSelectedSampleId(sample.sampleId);
    setExtraSelectedSample(sample as SampleRecord);
  };

  const handleDeselectSample = () => {
    setSelectedSampleId(null);
    setExtraSelectedSample(null);
  };

  const handleEdit = (sample: SampleRecord) => {
    setEditSample(sample);
  };

  const handleViewReport = (sample: SampleRecord | WorkspaceSampleCard) => {
    window.open(`/samples/${sample.sampleId}/report`, "_blank");
  };

  const handleViewAuditHistory = (sample: SampleRecord | WorkspaceSampleCard) => {
    setAuditSampleId(sample.sampleId);
  };

  const handlePrepareSample = (sample: SampleRecord | WorkspaceSampleCard) => {
    setPreparingSample(sample as SampleRecord);
  };

  const handleReceiveSuccess = (created: CreatedSampleRef[]) => {
    const count = created.length;
    setNotification({
      text: `Successfully received ${count} sample${count > 1 ? "s" : ""}. Test orders generated automatically.`,
      severity: "success"
    });
    loadRecords();
    postCreatePrint.offerPrint(
      created.map((sample) => ({
        entityType: "Sample",
        entityId: sample.id,
        code: sample.referenceNumber,
        displayName: "Sample"
      }))
    );
  };

  const handleEditSuccess = () => {
    setNotification({
      text: "Sample details corrected and signed.",
      severity: "success"
    });
    loadRecords();
  };

  // Compact register beside the grouped-actions panel and the selected-sample
  // panel - the two layouts differ only in the note beside the title.
  const renderCompactRegister = (headerNote: ReactNode) => (
    <Box
      sx={{
        width: { xs: "100%", md: "38%" },
        display: "flex",
        flexDirection: "column",
        gap: 1.5,
        flexShrink: 0,
        position: { md: "sticky" },
        top: { md: 0 },
        alignSelf: "flex-start"
      }}
    >
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main }}>
          Laboratory Register ({displayRecords.length}
          {!showSelectedOnly && totalCount > displayRecords.length ? ` of ${totalCount}` : ""})
        </Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          {headerNote}
        </Typography>
      </Box>

      <Paper
        elevation={0}
        sx={{
          border: "1px solid",
          borderColor: "divider",
          borderRadius: 2,
          overflowY: "auto",
          maxHeight: { xs: "340px", md: "calc(100vh - 330px)" },
          bgcolor: "background.paper"
        }}
      >
        <TableContainer>
          <Table size="small" stickyHeader>
            <TableHead>
              <TableRow sx={[tableHeadSx, { "& th": { fontWeight: 700, fontSize: 12, py: 1 } }]}>
                <TableCell>Item / Reference</TableCell>
                <TableCell sx={{ width: 65 }}>Type</TableCell>
                <TableCell sx={{ width: 95 }}>Batch/Ctrl</TableCell>
                <TableCell sx={{ width: 85 }}>Status</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {displayRecords.map((s) => (
                <SampleTableRow
                  key={s.sampleId}
                  sample={s as unknown as WorkspaceSampleCard}
                  isSelected={selectedSampleId === s.sampleId}
                  isChecked={checkedSampleIds.has(s.sampleId)}
                  onToggleCheck={handleToggleCheckSample}
                  onSelectSample={(sample) => handleSelectSample(sample)}
                  isCompact={true}
                  visibleColumns={new Set(["category", "batch", "control", "status"])}
                  colSpan={4}
                  onNeedsPreparationClick={() => handlePrepareSample(s)}                      onLifecycleBadgeClick={setSummarySampleId}
                />
              ))}
              {displayRecords.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} align="center" sx={{ py: 3, color: "text.secondary", fontSize: 12 }}>
                    No matching samples found.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
        {!showSelectedOnly && totalCount > 0 && (
          <TablePagination
            component="div"
            count={totalCount}
            page={Math.max(0, page - 1)}
            onPageChange={handlePageChange}
            rowsPerPage={pageSize}
            onRowsPerPageChange={handleRowsPerPageChange}
            rowsPerPageOptions={[25, 50, 100]}
            sx={{
              borderTop: "1px solid",
              borderColor: "divider",
              "& .MuiTablePagination-selectLabel, & .MuiTablePagination-displayedRows": {
                fontSize: 12
              },
              "& .MuiTablePagination-toolbar": {
                minHeight: 36,
                px: 1
              }
            }}
          />
        )}
      </Paper>
    </Box>
  );

  return (
    <>
      {/* Header Section */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, flexWrap: "wrap", gap: 1.5 }}>
        <PageHeader
          title={
            lab.code === "FP"
              ? lab.area === "rmpm"
                ? "RM & PM Workspace"
                : "FP Workspace"
              : `${lab.name} Workspace`
          }
          subtitle="Manage incoming samples, assignments, testing progress, review, and laboratory workflow execution from one workspace."
        />

        <Box sx={{ display: "flex", gap: 1.5, mt: 0.5 }}>
          <Button
            variant="outlined"
            size="medium"
            onClick={() => loadRecords()}
            disabled={loading}
            startIcon={<RefreshIcon />}
            sx={{
              borderColor: "divider",
              color: "text.secondary",
              fontWeight: 600,
              bgcolor: "background.paper",
              "&:hover": { bgcolor: "background.default", borderColor: "text.secondary" }
            }}
          >
            Refresh
          </Button>

          <Button
            variant="contained"
            color="primary"
            size="medium"
            onClick={() => setNewSampleDialogOpen(true)}
            startIcon={<AddIcon />}
            sx={{
              fontWeight: 600,
              px: 2.5
            }}
          >
            + New Sample
          </Button>
        </Box>
      </Box>

      {/* Unified KPI Status Cards */}
      <SampleStatusKpiCards
        counts={workloadCounts}
        activeKey={workloadFilter}
        onSelect={handleSelectWorkload}
        onClear={() => setFilter("workload", null)}
        allCount={workloadFilter || hasActiveFilters ? null : totalCount}
        canAssignAnalyst={permissions.includes(PERMISSIONS.SAMPLES_ASSIGN_ANALYST)}
      />

      {/* Deep-link-only workload filter indicator */}
      {workloadFilter && (workloadFilter === "retestInProgress" || workloadFilter === "reviewOverdue" || workloadFilter === "approvalOverdue") && (
        <Box sx={{ mb: 1.5, display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            Active Workload Filter:
          </Typography>
          <Chip
            label={
              workloadFilter === "retestInProgress"
                ? "Retests in Progress"
                : workloadFilter === "reviewOverdue"
                ? "Overdue Review (>24h)"
                : "Overdue Approval (>24h)"
            }
            color="primary"
            variant="outlined"
            size="small"
            onDelete={() => setFilter("workload", null)}
            sx={{ fontWeight: 600 }}
          />
        </Box>
      )}

      {/* Unified Filter Bar - search and display-mode toggle. Export is the audited table action. */}
      <SampleFilterBar
        search={search}
        onSearchChange={setSearch}
        categoryFilter={categoryFilter}
        onCategoryFilterChange={handleCategoryFilterChange}
        sampleStatusFilter={sampleStatusFilter}
        onSampleStatusFilterChange={handleSampleStatusFilterChange}
        testStatusFilter={testStatusFilter}
        onTestStatusFilterChange={handleTestStatusFilterChange}
        fromDate={fromDate}
        onFromDateChange={handleFromDateChange}
        toDate={toDate}
        onToDateChange={handleToDateChange}
        onResetFilters={handleResetFilters}
        hasActiveFilters={hasActiveFilters}
        viewMode={viewMode}
        onViewModeChange={(mode) => {
          setViewMode(mode);
          if (selectedSampleId !== null) {
            handleDeselectSample();
          }
        }}
      />

      {/* Selection summary - rendered above the layout branch so it survives
          the swap into the grouped-actions split view at 2+ selected. */}
      {records && (
        <SelectionSummaryBar
          selectedCount={checkedSampleIds.size}
          hiddenCount={hiddenCheckedCount}
          showingSelectedOnly={showSelectedOnly}
          onToggleShowSelectedOnly={() => setShowSelectedOnly((prev) => !prev)}
          onClear={selection.clear}
          onBulkAssign={
            // The table's own bulk bar offers this while the full-width table is showing.
            permissions.includes(PERMISSIONS.SAMPLES_ASSIGN_ANALYST) && !tableBulkBarVisible
              ? () => openBulkAssign(Array.from(checkedSampleIds))
              : undefined
          }
        />
      )}

      {/* Loading State - the skeleton only covers the very first load. Later
          reloads keep the register and selected sample on screen under a thin
          progress bar, so the test cards don't unmount and refetch. */}
      {records && loading && <LinearProgress sx={{ mb: 1, borderRadius: 1 }} />}
      {!records ? (
        <TableSkeleton rows={10} cols={6} />
      ) : checkedSampleIds.size >= 2 ? (
        /* GROUPED ACTIONS SPLIT-PANE LAYOUT (When 2+ samples are checked) */
        <Box
          sx={{
            display: "flex",
            flexDirection: { xs: "column", md: "row" },
            gap: 2,
            alignItems: "flex-start"
          }}
        >
          {/* Left Panel: Compact Sample Register with Checkboxes */}
          {renderCompactRegister(
            <>
              {checkedSampleIds.size} checked
              {hiddenCheckedCount > 0 ? ` · ${hiddenCheckedCount} hidden` : ""}
            </>
          )}

          {/* Right Panel: Grouped Actions Panel */}
          <Box
            sx={{
              flex: 1,
              minWidth: 0,
              display: "flex",
              flexDirection: "column"
            }}
          >
            <GroupedActionsPanel
              selectedSampleIds={[...selection.selected]}
              onDeselectAll={selection.clear}
              onOpenWorkflow={(sampleId, testOrderId) => {
                const sample = displayRecords.find((r) => r.sampleId === sampleId) ||
                  (extraSelectedSample?.sampleId === sampleId ? extraSelectedSample : null);
                const test = sample?.assignedTests?.find((t) => t.testOrderId === testOrderId);
                if (sample && test) {
                  handleTestClick(test as unknown as WorkspaceTestOrderSummary, sample as unknown as WorkspaceSampleCard);
                }
              }}
              onActionComplete={(result) => {
                setNotification({
                  text: `${result.succeededCount} test${result.succeededCount === 1 ? "" : "s"} started in incubation.${result.skippedCount > 0 ? ` (${result.skippedCount} skipped)` : ""}`,
                  severity: result.skippedCount > 0 ? "warning" : "success"
                });
                loadRecords(true);
              }}
              onPreparationComplete={(result) => {
                setNotification({
                  text: `${result.succeededCount} sample${result.succeededCount === 1 ? "" : "s"} prepared and signed.${result.skippedCount > 0 ? ` (${result.skippedCount} skipped)` : ""}`,
                  severity: result.skippedCount > 0 ? "warning" : "success"
                });
                loadRecords(true);
              }}
            />
          </Box>
        </Box>
      ) : selectedSample ? (
        /* MASTER-DETAIL SPLIT-PANE LAYOUT (When a single sample is selected) */
        <Box
          sx={{
            display: "flex",
            flexDirection: { xs: "column", md: "row" },
            gap: 2,
            alignItems: "flex-start"
          }}
        >
          {/* Left Panel: Compact Sample Register */}
          {renderCompactRegister("Click a sample to switch")}

          {/* Right Panel: Selected Sample & Analytical Workflows Panel */}
          <Box
            sx={{
              flex: 1,
              minWidth: 0,
              display: "flex",
              flexDirection: "column"
            }}
          >
            <SelectedSampleTestingPanel
              sample={selectedSample as unknown as WorkspaceSampleCard}
              onTestClick={(test, sample) => handleTestClick(test, sample)}
              onClose={handleDeselectSample}
              onNeedsPreparationClick={(sample) => handlePrepareSample(sample)}
              onLifecycleBadgeClick={setSummarySampleId}
              onCorrected={() => loadRecords(true)}
              onViewAuditHistory={(sampleId) => setAuditSampleId(sampleId)}
              onEdit={(sample) => setEditSample(sample as unknown as SampleRecord)}
              onVoid={(sample) => setVoidingSample(sample as unknown as SampleRecord)}
            />
          </Box>
        </Box>
      ) : (
        /* FULL-WIDTH WORKSPACE REGISTER (When no sample is selected) */
        <>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5, flexWrap: "wrap", gap: 1 }}>
            <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>
              Laboratory Register {records ? `(${showSelectedOnly ? displayRecords.length : totalCount} ${totalCount === 1 ? "sample" : "samples"})` : ""}
            </Typography>

            {viewMode !== "table" && !showSelectedOnly && totalCount > 0 && (
              <TablePagination
                component="div"
                count={totalCount}
                page={Math.max(0, page - 1)}
                onPageChange={handlePageChange}
                rowsPerPage={pageSize}
                onRowsPerPageChange={handleRowsPerPageChange}
                rowsPerPageOptions={[25, 50, 100]}
                sx={{
                  "& .MuiTablePagination-toolbar": { minHeight: 36, px: 0 },
                  "& .MuiTablePagination-selectLabel, & .MuiTablePagination-displayedRows": { fontSize: 12 }
                }}
              />
            )}
          </Box>

          {viewMode === "table" && (
            <SampleRegisterTable
              samples={displayRecords}
              tableKey={tableKey}
              tableState={tableState}
              legacyColumnsKey="microlims.sampleRegister.columns"
              selectedSampleId={selectedSampleId}
              selection={selection}
              bulkActions={bulkActions}
              serverTotal={showSelectedOnly ? displayRecords.length : totalCount}
              serverSortableColumns={WORKSPACE_SERVER_SORTABLE_COLUMNS}
              onServerSortChange={tableState.setSort}
              serverSort={activeSort}
              loadAllMatching={loadAllMatching}
              onSelectSample={handleSelectSample}
              onTestClick={(test, sample) => {
                handleTestClick(test, sample);
              }}
              onViewSummary={(sample) => {
                handleViewSummary(sample);
              }}
              onEdit={handleEdit}
              onViewReport={handleViewReport}
              onViewAuditHistory={handleViewAuditHistory}
              onPrepareSample={handlePrepareSample}
              onAssignAnalyst={handleAssignAnalyst}
              onVoid={(sample) => setVoidingSample(sample)}
            />
          )}

          {viewMode === "card" && (
            <SampleCardView
              samples={displayRecords as unknown as WorkspaceSampleCard[]}
              selectedSampleId={selectedSampleId}
              onSelectSample={(s) => setSelectedSampleId(s.sampleId)}
              onNeedsPreparationClick={(s) => handlePrepareSample(s)}
              onLifecycleBadgeClick={setSummarySampleId}
            />
          )}

          {viewMode === "kanban" && (
            <SampleKanbanView
              samples={displayRecords as unknown as WorkspaceSampleCard[]}
              onCardClick={(sampleId) => {
                setSelectedSampleId(sampleId);
              }}
            />
          )}

          {viewMode !== "table" && !showSelectedOnly && totalCount > pageSize && (
            <Paper
              elevation={0}
              sx={{
                mt: 1.5,
                border: "1px solid",
                borderColor: "divider",
                borderRadius: 2,
                bgcolor: "background.paper"
              }}
            >
              <TablePagination
                component="div"
                count={totalCount}
                page={Math.max(0, page - 1)}
                onPageChange={handlePageChange}
                rowsPerPage={pageSize}
                onRowsPerPageChange={handleRowsPerPageChange}
                rowsPerPageOptions={[25, 50, 100]}
                sx={{
                  "& .MuiTablePagination-selectLabel, & .MuiTablePagination-displayedRows": {
                    fontSize: 12
                  }
                }}
              />
            </Paper>
          )}
        </>
      )}

      {/* Dialogs Ecosystem */}

      {/* 1. New Sample Wizard Dialog */}
      <NewSampleDialog
        open={newSampleDialogOpen}
        onClose={() => setNewSampleDialogOpen(false)}
        onSuccess={handleReceiveSuccess}
        allowedCategories={getAllowedCategories(lab)}
        labMode={{ kind: "fixed", sectionId: lab.sectionId, code: lab.code }}
      />

      {/* 2. Signed Sample Details Correction Dialog */}
      <EditSampleDetailsDialog
        open={Boolean(editSample)}
        sample={editSample}
        onClose={() => setEditSample(null)}
        onSuccess={handleEditSuccess}
      />

      {/* 3. Assign Analyst Dialog (Section Head & Admin Only) */}
      <AssignAnalystDialog
        open={Boolean(assigningSample)}
        sample={assigningSample}
        onClose={() => setAssigningSample(null)}
        onAssigned={() => {
          setNotification({ text: "Analyst assignment updated successfully.", severity: "success" });
          loadRecords();
        }}
      />

      <BulkAssignDialog
        open={bulkAssignOpen}
        sampleIds={bulkAssignIds}
        selection={selection}
        onClose={() => setBulkAssignOpen(false)}
        onAssigned={() => {
          loadRecords(true);
        }}
      />

      {/* 4. Detailed Sample Summary & Electronic Signature Dialog */}
      <SampleSummaryDialog
        open={Boolean(summarySampleId)}
        sampleId={summarySampleId}
        onClose={() => {
          setSummarySampleId(null);
          loadRecords();
        }}
      />

      {/* 5. Test Workflow Execution Router Dialog */}
      <TestWorkflowDialogRouter
        open={Boolean(activeTest)}
        test={activeTest}
        sample={activeSampleForTest}
        onClose={() => {
          setActiveTest(null);
          setActiveSampleForTest(null);
          loadRecords(true);
        }}
      />

      {/* 6. Preparation Dialog (Water, EM, After Cleaning) */}
      <PreparationDialog
        open={Boolean(preparingSample)}
        sample={preparingSample ? {
          sampleId: preparingSample.sampleId,
          category: preparingSample.category,
          itemId: preparingSample.itemId,
          itemName: preparingSample.displayName,
          departmentId: preparingSample.departmentId,
          machineId: preparingSample.machineId,
          waterDepartmentId: preparingSample.waterDepartmentId,
          labSectionId: preparingSample.labSectionId,
          assignedAnalystIds: Array.from(new Set((preparingSample.assignedTests ?? [])
            .map((t) => t.assignedAnalystId)
            .filter((id): id is number => id != null))),
          assignedAnalystNames: assignedAnalystNames(preparingSample.assignedTests ?? [])
        } : null}
        onClose={() => {
          setPreparingSample(null);
          loadRecords();
        }}
      />

      {/* 7. Change History Audit Dialog */}
      <AuditHistoryDialog
        open={Boolean(auditSampleId)}
        entityName="Sample"
        entityId={auditSampleId}
        onClose={() => setAuditSampleId(null)}
      />

      {/* 8. Void Sample Confirmation Dialog */}
      <VoidSampleConfirmationDialog
        open={Boolean(voidingSample)}
        sample={voidingSample}
        onClose={() => setVoidingSample(null)}
        onSuccess={() => {
          setNotification({
            text: `Sample #${voidingSample?.sampleId} (${voidingSample?.displayName}) has been marked as Voided.`,
            severity: "success"
          });
          setVoidingSample(null);
          loadRecords();
        }}
      />

      <PrintLabelsDialog
        open={postCreatePrint.dialogOpen}
        targets={postCreatePrint.targets}
        onClose={postCreatePrint.closeDialog}
      />

      <PrintLabelsDialog
        open={labelsDialogOpen}
        targets={labelTargets}
        onClose={() => setLabelsDialogOpen(false)}
      />

      {/* Toast Notification Snackbar */}
      <Snackbar
        open={Boolean(notification)}
        autoHideDuration={5000}
        onClose={() => setNotification(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
      >
        {notification ? (
          <Alert
            severity={notification.severity}
            onClose={() => setNotification(null)}
            sx={{ borderRadius: 1.5 }}
          >
            {notification.text}
          </Alert>
        ) : undefined}
      </Snackbar>
    </>
  );
}
