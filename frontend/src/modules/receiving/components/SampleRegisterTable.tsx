import React, { useCallback, useEffect, useMemo, useRef, useState, type ReactElement } from "react";
import {
  Table,
  TableBody,
  TableCell,
  TableRow,
  Box,
  Typography,
  Tooltip,
  Button,
  type ButtonProps,
  Checkbox,
  Avatar,
  useTheme
} from "@mui/material";
import type { SxProps, Theme } from "@mui/material/styles";
import HourglassTopIcon from "@mui/icons-material/HourglassTop";
import DescriptionIcon from "@mui/icons-material/Description";
import AssignmentIndIcon from "@mui/icons-material/AssignmentInd";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowRightIcon from "@mui/icons-material/KeyboardArrowRight";
import SubdirectoryArrowRightIcon from "@mui/icons-material/SubdirectoryArrowRight";
import { Chip } from "@mui/material";
import { SampleRecord, TestOrderSummary } from "../types/receivingTypes";
import { CategoryBadge, CauseBadge, categoryLabel } from "../../../components/StatusBadge";
import { DirectionalIcon } from "../../../components/DirectionalIcon";
import { Ltr } from "../../../components/Ltr";
import { i18n as appI18n, useTranslation } from "../../../i18n";
import { StatusTone } from "../../../theme/statusTokens";
import { TestStatusSummaryCell, testSummaryText } from "./TestStatusSummaryCell";
import type { TFunction } from "i18next";
import { SampleActionMenu } from "./SampleActionMenu";
import { assignedAnalystsByTest, assignedAnalystsLabel } from "../utils/assignedAnalysts";
import { ReadOnlyItemDocumentsDialog } from "../../../components/ReadOnlyItemDocumentsDialog";
import { ItemDocumentService } from "../../laboratoryConfiguration/items/services/ItemDocumentService";
import { ItemStmService } from "../../laboratoryConfiguration/items/services/ItemStmService";
import { useAuth } from "../../../contexts/AuthContext";
import { GatedAction } from "../../../components/GatedAction";
import { useActionAvailability } from "../../../hooks/useActionAvailability";
import { PERMISSIONS } from "../../../routes/routes";
import { isInteractiveElement } from "../../../utils/isInteractiveElement";
import { RegisterTable, type RegisterColumn } from "../../../components/lab/RegisterTable";
import type { BulkAction } from "../../../components/lab/table/BulkActionBar";
import { TableToolbar } from "../../../components/lab/table/TableToolbar";
import type { TableState } from "../../../components/lab/table/tableUrlCodec";
import type { RowSelection } from "../../../components/lab/table/useRowSelection";
import { useTableState, type UseTableState } from "../../../components/lab/table/useTableState";
import type { ExportColumn } from "../../../components/lab/table/exportRows";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

export interface SampleRegisterTableProps {
  samples: SampleRecord[];
  // Registry key (frontend/src/tables/tableRegistry.json) naming this register.
  tableKey: string;
  // The page owns the state when it builds a server filter from it (the workspace);
  // without it the table keeps its own (the receiving register).
  tableState?: UseTableState;
  // Used only when the table creates its own state.
  tableDefaults?: Partial<TableState>;
  // localStorage key of this register's old column choice, read once into the table state and removed.
  legacyColumnsKey?: string;
  selectedSampleId?: number | null;
  // The checked samples; present only where a selection feeds grouped or bulk actions.
  selection?: RowSelection<number>;
  bulkActions?: BulkAction<string | number>[];
  // The samples are one page of a server-paged list with this many rows in total.
  serverTotal?: number;
  serverSortableColumns?: ReadonlySet<string> | readonly string[];
  onServerSortChange?: (next: { key: string; direction: "asc" | "desc" } | null) => void;
  serverSort?: { key: string; direction: "asc" | "desc" } | null;
  loadAllMatching?: () => Promise<SampleRecord[]>;
  onSelectSample?: (sample: SampleRecord) => void;
  onTestClick: (test: TestOrderSummary, sample: SampleRecord) => void;
  onViewSummary: (sample: SampleRecord) => void;
  onEdit?: (sample: SampleRecord) => void;
  onViewReport: (sample: SampleRecord) => void;
  onViewAuditHistory: (sample: SampleRecord) => void;
  onPrepareSample: (sample: SampleRecord) => void;
  onAssignAnalyst?: (sample: SampleRecord) => void;
  onVoid?: (sample: SampleRecord) => void;
  onAddLaboratory?: (sample: SampleRecord) => void;
  // Periodic retest awaiting its tests: opens the test-selection dialog.
  onSelectTests?: (sample: SampleRecord) => void;
}

// Columns the user can show or hide (the item column always shows). Whatever
// is hidden among received date, #, item type, cause, sampled-by and due is
// summarised under the item name instead, so hiding a column never hides the
// information. The keys are the ones in the table registry.
type RegisterColumnKey =
  | "item"
  | "receivedAt"
  | "sampleId"
  | "category"
  | "cause"
  | "sampledBy"
  | "batchControl"
  | "assignedTo"
  | "sampleStatus"
  | "due"
  | "tests"
  | "nextAction";

// Where each column sat in the old fixed layout; the old stored choice is
// turned into a column list in this order.
const LEGACY_COLUMN_ORDER: RegisterColumnKey[] = [
  "receivedAt", "sampleId", "item", "category", "cause", "sampledBy",
  "batchControl", "assignedTo", "sampleStatus", "due", "tests", "nextAction"
];

export const SAMPLE_REGISTER_DEFAULTS: Partial<TableState> = { density: "compact" };
export const WORKSPACE_REGISTER_DEFAULTS: Partial<TableState> = { density: "compact", pageSize: 50 };

// The columns an old stored choice asked for, or null when there is nothing usable to carry over.
function parseLegacyColumns(raw: string): string[] | null {
  try {
    const valid = new Set<string>(LEGACY_COLUMN_ORDER);
    const stored = JSON.parse(raw) as unknown;
    if (!Array.isArray(stored)) return null;
    const names = stored.filter((k): k is string => typeof k === "string");
    const chosen = new Set(names.filter((k) => valid.has(k) && k !== "item"));
    // A choice saved before the Due column existed gets it once, by default.
    if (!names.includes("due") && !names.includes("due:off")) chosen.add("due");
    return LEGACY_COLUMN_ORDER.filter((k) => k === "item" || chosen.has(k));
  } catch {
    return null;
  }
}

// What the next-action column offers, from the statuses the server reported. `target` names the handler the
// register routes a click to; an export carries only the label.
interface ResolvedNextAction {
  label: string;
  // The server-answered action that gates the button (review.complete, approval.approve); a refusal shows its sentence.
  gate?: string;
  // "wait" renders a passive chip; anything else renders a button.
  kind: "primary" | "secondary" | "wait";
  target?: "selectTests" | "prepare" | "summary" | "report" | "test" | "assign";
  test?: TestOrderSummary;
}

// Which next actions this user can be offered.
export interface NextActionCapabilities {
  canSelectTests: boolean;
  canAssign: boolean;
}

type ReceivingT = TFunction<"receiving">;

// Reads only statuses the server reported; no laboratory rule is decided here.
function resolveNextAction(sample: SampleRecord, caps: NextActionCapabilities, t: ReceivingT): ResolvedNextAction | null {
  if (CLOSED_STATUSES.has(sample.status)) return null;
  // A laboratory with tests of its own carries on; the rest is shown by the summary chip.
  if (sample.awaitingTestSelection && sample.assignedTests.length === 0) {
    return caps.canSelectTests
      ? { label: t("register.nextAction.selectTests"), kind: "primary", target: "selectTests" }
      : { label: t("register.nextAction.awaitingTestSelection"), kind: "wait" };
  }
  if (sample.preparationStatus === "NeedsPreparation") {
    return { label: t("register.nextAction.prepare"), kind: "primary", target: "prepare" };
  }
  if (sample.status === "UnderReview" || sample.status === "PendingReview") {
    return { label: t("register.nextAction.review"), kind: "primary", gate: "review.complete", target: "summary" };
  }
  if (sample.status === "UnderApproval") {
    return { label: t("register.nextAction.approve"), kind: "primary", gate: "approval.approve", target: "summary" };
  }
  if (sample.status === "Approved") {
    return { label: t("register.nextAction.viewReport"), kind: "secondary", target: "report" };
  }
  const tests = sample.assignedTests || [];
  const ready = tests.find((test) => ["ReadyToRead", "EnterResult"].includes(effectiveTestStatus(test)));
  if (ready) {
    return {
      label: tests.length > 1 ? t("register.nextAction.recordCode", { code: ready.testCode }) : t("register.nextAction.recordResult"),
      kind: "primary",
      target: "test",
      test: ready
    };
  }
  const hasAnalyst = Boolean(sample.assignedAnalystName || tests.some((test) => test.assignedAnalystName));
  if (!hasAnalyst && caps.canAssign) {
    return { label: t("register.nextAction.assign"), kind: "secondary", target: "assign" };
  }
  const notStarted = tests.find((test) => ["Pending", "Waiting", "NotStarted"].includes(effectiveTestStatus(test)));
  if (notStarted) {
    return {
      label: tests.length > 1 ? t("register.nextAction.startCode", { code: notStarted.testCode }) : t("register.nextAction.startTest"),
      kind: "secondary",
      target: "test",
      test: notStarted
    };
  }
  if (tests.some((test) => ["InProgress", "Running", "Incubating"].includes(effectiveTestStatus(test)))) {
    return { label: t("register.nextAction.inProgress"), kind: "wait" };
  }
  return null;
}

const safeDateTime = (iso: string): string => {
  try {
    return formatDateTimeWithTimeZone(iso, { english: true });
  } catch {
    return iso;
  }
};

// The register's columns without their cell renderers: what a CSV export of the register carries.
// `exportValue` is the text the screen shows for the column (dates with their zone, labels, never raw
// epochs or enums); `caps` decides whether a next action the user cannot take is offered.
const SAMPLE_COLUMN_DEFS: ReadonlyArray<{
  key: RegisterColumnKey;
  sortValue?: (s: SampleRecord) => string | number | null | undefined;
  exportValue: (s: SampleRecord, t: ReceivingT, caps: NextActionCapabilities) => string;
}> = [
  { key: "item", sortValue: (s) => s.displayName, exportValue: (s) => `${s.displayName} / ${s.referenceNumber}` },
  { key: "receivedAt", sortValue: (s) => new Date(s.receivedAt).getTime(), exportValue: (s) => safeDateTime(s.receivedAt) },
  { key: "sampleId", sortValue: (s) => s.sampleId, exportValue: (s) => `#${s.sampleId}` },
  { key: "category", sortValue: (s) => s.category, exportValue: (s) => categoryLabel(s.category) },
  { key: "cause", exportValue: (s) => s.causeOfTesting || "" },
  { key: "sampledBy", sortValue: (s) => s.sampledBy, exportValue: (s) => s.sampledBy || "" },
  {
    key: "batchControl",
    sortValue: (s) => s.batchNumber ?? s.controlNumber,
    exportValue: (s, t) =>
      [s.batchNumber ? `${t("register.batchPrefix")} ${s.batchNumber}` : "", `${t("register.controlPrefix")} ${s.controlNumber || "—"}`]
        .filter(Boolean)
        .join(" ")
  },
  {
    key: "assignedTo",
    sortValue: (s) => assignedAnalystsLabel(s.assignedTests),
    exportValue: (s, t) => assignedAnalystsLabel(s.assignedTests) ?? t("register.unassigned")
  },
  {
    key: "sampleStatus",
    sortValue: (s) => s.status,
    exportValue: (s, t) => t(`register.sampleStatus.${s.status}`, { defaultValue: s.status })
  },
  {
    key: "due",
    sortValue: (s) => (s.dueAt ? new Date(s.dueAt).getTime() : null),
    exportValue: (s) => (s.dueAt ? safeDateTime(s.dueAt) : "")
  },
  { key: "tests", exportValue: (s, t) => testSummaryText(t, s) },
  { key: "nextAction", exportValue: (s, t, caps) => resolveNextAction(s, caps, t)?.label ?? "" }
];

// The file is English whatever the interface language: the English record is the regulated one.
const englishT = (): ReceivingT => appI18n.getFixedT("en", "receiving") as ReceivingT;

// The export columns for the register columns a user has chosen to show, in their order. Labels and values
// are always English.
export function sampleExportColumns(
  visibleKeys: readonly string[],
  caps: NextActionCapabilities = { canSelectTests: false, canAssign: false }
): ExportColumn<SampleRecord>[] {
  const t = englishT();
  const byKey = new Map(SAMPLE_COLUMN_DEFS.map((d) => [String(d.key), d]));
  return visibleKeys
    .map((key) => byKey.get(key))
    .filter((d): d is NonNullable<typeof d> => Boolean(d))
    .map((d) => ({
      key: String(d.key),
      label: t(`register.columns.${d.key}`),
      exportLabel: t(`register.columns.${d.key}`),
      sortValue: d.sortValue,
      exportValue: (s: SampleRecord) => d.exportValue(s, t, caps)
    }));
}

const getSampleRowId = (sample: SampleRecord) => sample.sampleId;

const ROW_CELL_SX = { fontSize: 12, color: "text.secondary" } as const;

// "Due in 5 h" / "Overdue by 2 d" from the server's due time and flag. The
// relative wording is display only; whether it is overdue comes from the server.
export function dueLabel(dueAt: string | null | undefined, isOverdue: boolean | undefined, now = Date.now()): string | null {
  if (!dueAt) return null;
  const diffMs = new Date(dueAt).getTime() - now;
  const abs = Math.abs(diffMs);
  const span = abs >= 48 * 3600e3 ? appI18n.t("register.due.days", { ns: "receiving", count: Math.floor(abs / (24 * 3600e3)) })
    : abs >= 3600e3 ? appI18n.t("register.due.hours", { ns: "receiving", count: Math.floor(abs / 3600e3) })
    : appI18n.t("register.due.minutes", { ns: "receiving", count: Math.max(1, Math.floor(abs / 60e3)) });
  return isOverdue
    ? appI18n.t("register.due.overdueBy", { ns: "receiving", span })
    : diffMs > 0
    ? appI18n.t("register.due.in", { ns: "receiving", span })
    : appI18n.t("register.due.now", { ns: "receiving" });
}

const CLOSED_STATUSES = new Set(["Rejected", "Voided", "Cancelled", "RetestRequested"]);
// A sample in one of these has no open test left, so its analyst can no longer be set or changed (the server
// refuses with "no open tests you can assign"); the register shows no assign control for it.
const NO_ASSIGN_STATUSES = new Set([...CLOSED_STATUSES, "Approved"]);
const effectiveTestStatus = (t: TestOrderSummary) => t.workflowStatus ?? t.status;

// Colour of one test's segment in the progress bar - presentation only,
// from the status the server reported.
function testSegmentTone(status: string): StatusTone {
  if (status === "Approved" || status === "Completed") return "notDetected";
  if (status === "Rejected") return "detected";
  if (status === "ReadyToRead" || status === "EnterResult" || status === "ResultEntered") return "purple";
  if (status === "UnderReview" || status === "Reviewed" || status === "PendingReview") return "action";
  if (status === "InProgress" || status === "Running" || status === "Incubating") return "info";
  return "pending";
}

const initialsOf = (name: string) =>
  name.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0]!.toUpperCase()).join("");
const initialsSx = { fontSize: "10px !important", fontWeight: 700, bgcolor: "primary.main", color: "primary.contrastText" } as const;

// One next-action button gated by the server's answer for its sample. Only rows that offer a gated action mount
// it, so the register asks about the few samples waiting for review or approval, not about every row.
function GatedNextAction({ sampleId, action, children }: { sampleId: number; action: string; children: ReactElement<ButtonProps> }) {
  const availability = useActionAvailability("sample", sampleId);
  return (
    <GatedAction action={action} availability={availability}>
      {children}
    </GatedAction>
  );
}

interface NextAction {
  label: string;
  // The server-answered action that gates the button (review.complete, approval.approve); a refusal shows its sentence.
  gate?: string;
  // "wait" renders a passive chip; anything else renders a button.
  kind: "primary" | "secondary" | "wait";
  run?: () => void;
}

// Same statuses StatusBadge.tsx's central token map covers, but collapsed
// to this table's own coarser labels (e.g. RetestRequested/Cancelled/Voided
// all read "Cancelled / Voided" here) - so this stays a local label+tone
// map rather than reusing StatusBadge directly, while still sourcing its
// colors from the one shared theme.custom.status table (not its own hex).
const SAMPLE_STATUS_TONES: Record<string, StatusTone> = {
  Received: "pending",
  InTesting: "info",
  UnderReview: "action",
  UnderApproval: "action",
  PendingReview: "action",
  Approved: "notDetected",
  Rejected: "detected",
  RetestRequested: "pending",
  Cancelled: "pending",
  Voided: "pending"
};

function OverallSampleStatusBadge({ status }: { status: string }) {
  const theme = useTheme();
  const { t } = useTranslation("receiving");
  const tone = SAMPLE_STATUS_TONES[status] ?? "pending";
  const label = t(`register.sampleStatus.${status}`, { defaultValue: status });
  const tokens = theme.custom.status[tone];

  return (
    <Box
      component="span"
      sx={{
        display: "inline-flex",
        alignItems: "center",
        px: 1.25,
        py: 0.35,
        borderRadius: 5,
        fontSize: 12,
        fontWeight: 700,
        whiteSpace: "nowrap",
        bgcolor: tokens.bg,
        color: tokens.text,
        border: `1px solid ${tokens.border}`
      }}
    >
      {label}
    </Box>
  );
}

export function SampleRegisterTable(props: SampleRegisterTableProps) {
  if (props.tableState) {
    return <SampleRegisterTableInner {...props} tableState={props.tableState} />;
  }
  return <ManagedSampleRegisterTable {...props} />;
}

function ManagedSampleRegisterTable(props: SampleRegisterTableProps) {
  const tableState = useTableState(props.tableKey, props.tableDefaults ?? SAMPLE_REGISTER_DEFAULTS);
  return <SampleRegisterTableInner {...props} tableState={tableState} />;
}

function SampleRegisterTableInner({
  samples,
  tableKey,
  tableState,
  legacyColumnsKey,
  selectedSampleId,
  selection,
  bulkActions,
  serverTotal,
  serverSortableColumns,
  onServerSortChange,
  serverSort,
  loadAllMatching,
  onSelectSample,
  onTestClick,
  onViewSummary,
  onEdit,
  onViewReport,
  onViewAuditHistory,
  onPrepareSample,
  onAssignAnalyst,
  onVoid,
  onAddLaboratory,
  onSelectTests
}: SampleRegisterTableProps & { tableState: UseTableState }) {
  const theme = useTheme();
  const { t } = useTranslation("receiving");
  const { t: tCommon } = useTranslation("common");
  const { permissions, role } = useAuth();
  const isAuthorizedToAssign = permissions.includes(PERMISSIONS.SAMPLES_ASSIGN_ANALYST);
  // Mirrors the server: only a Section Head or Supervisor chooses a periodic retest's tests.
  const canChooseTests = permissions.includes(PERMISSIONS.SAMPLES_CORRECT) && (role === "SectionHead" || role === "Reviewer");
  const [activeDocSample, setActiveDocSample] = useState<SampleRecord | null>(null);
  const [expandedIds, setExpandedIds] = useState<ReadonlySet<string | number>>(() => new Set());

  const visibleColumns = useMemo(() => new Set<string>(tableState.state.columns), [tableState.state.columns]);
  const show = (key: RegisterColumnKey) => visibleColumns.has(key);

  // The old column choice (one localStorage key per register) is carried into
  // the table state once and then removed. A choice already made in the new
  // state wins; the old key goes either way.
  const setColumns = tableState.setColumns;
  const migratedRef = useRef(false);
  useEffect(() => {
    if (!legacyColumnsKey || migratedRef.current) return;
    migratedRef.current = true;
    try {
      const raw = localStorage.getItem(legacyColumnsKey);
      if (raw === null) return;
      localStorage.removeItem(legacyColumnsKey);
      if (localStorage.getItem(`microlims.table.${tableKey}`) !== null) return;
      const carried = parseLegacyColumns(raw);
      if (carried) setColumns(carried);
    } catch {
      // Storage blocked: the default columns apply.
    }
  }, [legacyColumnsKey, tableKey, setColumns]);

  const nextActionCaps: NextActionCapabilities = {
    canSelectTests: Boolean(onSelectTests) && canChooseTests,
    canAssign: isAuthorizedToAssign && Boolean(onAssignAnalyst)
  };

  // What the analyst should do next for this sample, routed to the same
  // handlers the row's menus already use.
  const getNextAction = (sample: SampleRecord): NextAction | null => {
    const resolved = resolveNextAction(sample, nextActionCaps, t);
    if (!resolved) return null;
    const { target, test, ...rest } = resolved;
    const runners: Record<NonNullable<ResolvedNextAction["target"]>, (() => void) | undefined> = {
      selectTests: () => onSelectTests?.(sample),
      prepare: () => onPrepareSample(sample),
      summary: () => onViewSummary(sample),
      report: () => onViewReport(sample),
      test: test ? () => onTestClick(test, sample) : undefined,
      assign: () => onAssignAnalyst?.(sample)
    };
    return { ...rest, run: target ? runners[target] : undefined };
  };

  const toggleExpand = (sampleId: string | number) => {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(sampleId)) {
        next.delete(sampleId);
      } else {
        next.add(sampleId);
      }
      return next;
    });
  };

  // Group samples into top-level samples and nested retest children
  const { topLevelSamples, childrenByParentId } = useMemo(() => {
    const sampleIdSet = new Set(samples.map((s) => s.sampleId));
    const byParent = new Map<number, SampleRecord[]>();
    const topLevel: SampleRecord[] = [];
    for (const sample of samples) {
      if (sample.originSampleId != null && sampleIdSet.has(sample.originSampleId)) {
        const list = byParent.get(sample.originSampleId) || [];
        list.push(sample);
        byParent.set(sample.originSampleId, list);
      } else {
        topLevel.push(sample);
      }
    }
    return { topLevelSamples: topLevel, childrenByParentId: byParent };
  }, [samples]);

  // Every id the current page renders, including retest children of any
  // expanded parent - "select all" should mean what is on screen, not the
  // whole filtered set, which could be hundreds of rows away.
  const pageSelectionIds = useCallback((paged: SampleRecord[]): number[] => {
    const ids: number[] = [];
    const collect = (sample: SampleRecord) => {
      ids.push(sample.sampleId);
      if (expandedIds.has(sample.sampleId)) {
        (childrenByParentId.get(sample.sampleId) || []).forEach(collect);
      }
    };
    paged.forEach(collect);
    return ids;
  }, [expandedIds, childrenByParentId]);

  const formatReceivedDate = (d: string) => {
    try {
      return formatDateTimeWithTimeZone(d);
    } catch {
      return d;
    }
  };

  const handleRowClick = (sample: SampleRecord, e?: React.SyntheticEvent<HTMLElement>) => {
    if (e && isInteractiveElement(e.target, e.currentTarget)) {
      return;
    }
    onSelectSample?.(sample);
  };

  // A row's state edge: overdue, selected, needs preparation or a nested
  // retest. Overdue wins even over selection, so a late sample never looks on
  // time. Drawn on the first cell - a border on the row itself does not
  // render in a collapsed-border table.
  const rowSxFor = (sample: SampleRecord, level: number): SxProps<Theme> => {
    const isSelected = selectedSampleId === sample.sampleId;
    const isNested = level > 0;
    const needsPreparation = sample.preparationStatus === "NeedsPreparation";
    const edgeOffset = theme.direction === "rtl" ? "-4px" : "4px";
    return {
      cursor: "pointer",
      bgcolor: isSelected
        ? theme.custom.status.purple.bg
        : isNested
        ? "action.hover"
        : "inherit",
      "& > td:first-of-type": {
        boxShadow: `inset ${edgeOffset} 0 0 ${
          sample.isOverdue
            ? theme.custom.status.detected.text
            : isSelected
            ? theme.custom.status.purple.border
            : needsPreparation
            ? theme.custom.status.inconclusive.border
            : isNested
            ? theme.palette.warning.main
            : "transparent"
        }`
      },
      "&:last-child td, &:last-child th": { border: 0 },
      "&:hover": {
        bgcolor: isSelected
          ? theme.custom.status.purple.bg
          : isNested
          ? "action.selected"
          : "action.hover"
      }
    };
  };

  const checkboxLabel = (sample: SampleRecord) => t("register.selectRowAria", { name: sample.displayName, reference: sample.referenceNumber });

  // Item / reference cell: name, reference, retest and OOS chips, whatever
  // columns are hidden, and the controlled-document badge. A nested retest
  // carries its own checkbox here because it is not a row of the main table.
  const renderItemCell = (sample: SampleRecord, level: number) => {
    const isSelected = selectedSampleId === sample.sampleId;
    const children = childrenByParentId.get(sample.sampleId) || [];
    const hasChildren = children.length > 0;
    const isExpanded = expandedIds.has(sample.sampleId);
    const isNested = level > 0;
    const itemMetaNodes: React.ReactNode[] = [
      !show("sampleId") ? <Ltr key="id">#{sample.sampleId}</Ltr> : null,
      !show("category") ? categoryLabel(sample.category, tCommon) : null,
      !show("cause") && sample.causeOfTesting?.trim()
        ? tCommon(`causeOfTesting.${sample.causeOfTesting}`, { defaultValue: sample.causeOfTesting })
        : null,
      !show("sampledBy") && sample.sampledBy?.trim() ? sample.sampledBy : null,
      !show("receivedAt") ? <Ltr key="received">{formatReceivedDate(sample.receivedAt)}</Ltr> : null,
      !show("due") && sample.isOverdue ? dueLabel(sample.dueAt, sample.isOverdue) : null
    ].filter((v): v is React.ReactElement | string => Boolean(typeof v === "string" ? v.trim() : v));

    return (
      <Box sx={{ display: "flex", alignItems: "flex-start", gap: 0.75, ps: isNested ? `${level * 24}px` : undefined }}>
        {isNested && selection && (
          <Checkbox
            size="small"
            checked={selection.isSelected(sample.sampleId)}
            onChange={() => selection.toggle(sample.sampleId)}
            onClick={(e) => e.stopPropagation()}
            sx={{ p: 0.25, mr: 0.25 }}
            slotProps={{ input: { "aria-label": checkboxLabel(sample) } }}
          />
        )}
        {isNested && (
          <DirectionalIcon>
            <SubdirectoryArrowRightIcon
              sx={{ fontSize: 16, color: "warning.main", mt: 0.25, flexShrink: 0 }}
            />
          </DirectionalIcon>
        )}
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography
            sx={{
              fontWeight: isSelected ? 700 : 600,
              fontSize: 13,
              color: isSelected ? theme.palette.primary.main : "text.primary",
              lineHeight: 1.2
            }}
          >
            {sample.displayName}
          </Typography>
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, flexWrap: "wrap", mt: 0.25 }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              <Ltr>{sample.referenceNumber}</Ltr>
            </Typography>
            {isNested && (
              <Chip
                size="small"
                label={t("register.retestChip")}
                color="warning"
                variant="outlined"
                sx={{ height: 20, fontSize: 12, fontWeight: 700 }}
              />
            )}
            {sample.oosGroupCode && (
              <Tooltip title={t("register.oosChainTooltip", { code: sample.oosGroupCode })}>
                <Chip
                  size="small"
                  label={<Ltr>{sample.oosGroupCode}</Ltr>}
                  sx={{ height: 20, fontSize: 12, fontFamily: "monospace", fontWeight: 600 }}
                />
              </Tooltip>
            )}
            {hasChildren && (
              <Tooltip title={isExpanded ? t("register.collapseRetest") : t("register.expandRetest")}>
                <Chip
                  size="small"
                  data-no-row-click="true"
                  icon={
                    isExpanded ? (
                      <KeyboardArrowDownIcon sx={{ fontSize: "14px !important" }} />
                    ) : (
                      <DirectionalIcon>
                        <KeyboardArrowRightIcon sx={{ fontSize: "14px !important" }} />
                      </DirectionalIcon>
                    )
                  }
                  label={t("register.retestCount", { count: children.length })}
                  onClick={(e) => {
                    e.stopPropagation();
                    toggleExpand(sample.sampleId);
                  }}
                  sx={{
                    height: 20,
                    fontSize: 12,
                    fontWeight: 700,
                    cursor: "pointer",
                    bgcolor: isExpanded ? "primary.main" : "action.selected",
                    color: isExpanded ? "primary.contrastText" : "text.primary",
                    "&:hover": {
                      bgcolor: isExpanded ? "primary.dark" : "action.focus"
                    }
                  }}
                />
              </Tooltip>
            )}
          </Box>
          {itemMetaNodes.length > 0 && (
            <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.25 }}>
              {itemMetaNodes.map((node, i) => (
                <React.Fragment key={i}>
                  {i > 0 && " · "}
                  {node}
                </React.Fragment>
              ))}
            </Typography>
          )}
          {sample.itemId && (
            <SampleDocIndicator
              sample={sample}
              onOpenDocs={(s) => setActiveDocSample(s)}
            />
          )}
        </Box>
      </Box>
    );
  };

  const renderCell = (key: RegisterColumnKey, sample: SampleRecord, level: number): React.ReactNode => {
    switch (key) {
      case "item":
        return renderItemCell(sample, level);
      case "receivedAt":
        return <Typography component="span" sx={{ ...ROW_CELL_SX, whiteSpace: "nowrap" }}><Ltr>{formatReceivedDate(sample.receivedAt)}</Ltr></Typography>;
      case "sampleId":
        return <Typography component="span" sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}><Ltr>#{sample.sampleId}</Ltr></Typography>;
      case "category":
        return <CategoryBadge category={sample.category} />;
      case "cause":
        return (
          <CauseBadge
            label={
              sample.causeOfTesting?.trim()
                ? t(`common:causeOfTesting.${sample.causeOfTesting}`, { defaultValue: sample.causeOfTesting })
                : "—"
            }
          />
        );
      case "sampledBy":
        return <Typography component="span" sx={ROW_CELL_SX}>{sample.sampledBy || "—"}</Typography>;
      case "batchControl":
        return sample.batchNumber ? (
          <Box sx={{ fontSize: 12, minWidth: 0, overflowWrap: "anywhere" }}>
            <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary", overflowWrap: "anywhere" }}>
              {t("register.batchPrefix")} <Ltr>{sample.batchNumber}</Ltr>
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary", overflowWrap: "anywhere" }}>
              {t("register.controlPrefix")} <Ltr>{sample.controlNumber || "—"}</Ltr>
            </Typography>
          </Box>
        ) : (
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            {t("register.controlPrefix")} <Ltr>{sample.controlNumber || "—"}</Ltr>
          </Typography>
        );
      case "assignedTo": {
        // Each test has its own analyst: "Mona Ali +1" when there are several.
        const assignedAnalystName = assignedAnalystsLabel(sample.assignedTests);
        const analystsByTest = assignedAnalystsByTest(sample.assignedTests);
        const assignable = isAuthorizedToAssign && Boolean(onAssignAnalyst) && !NO_ASSIGN_STATUSES.has(sample.status);
        if (assignedAnalystName) {
          return assignable && onAssignAnalyst ? (
            <Tooltip title={t("register.reassignTooltip", { analysts: analystsByTest })}>
              <Chip
                size="small"
                data-no-row-click="true"
                avatar={<Avatar>{initialsOf(assignedAnalystName)}</Avatar>}
                label={assignedAnalystName}
                onClick={(e) => {
                  e.stopPropagation();
                  onAssignAnalyst(sample);
                }}
                variant="outlined"
                sx={{
                  fontSize: 12,
                  fontWeight: 600,
                  cursor: "pointer",
                  "& .MuiChip-avatar": initialsSx,
                  color: theme.palette.primary.main,
                  borderColor: theme.palette.primary.main,
                  bgcolor: `${theme.palette.primary.main}0D`,
                  "&:hover": {
                    bgcolor: `${theme.palette.primary.main}1A`
                  }
                }}
              />
            </Tooltip>
          ) : (
            <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }} title={analystsByTest}>
              <Avatar sx={{ ...initialsSx, width: 22, height: 22 }}>{initialsOf(assignedAnalystName)}</Avatar>
              <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }} noWrap>
                {assignedAnalystName}
              </Typography>
            </Box>
          );
        }
        return assignable && onAssignAnalyst ? (
          <Tooltip title={t("register.assignTooltip")}>
            <Button
              size="small"
              variant="outlined"
              data-no-row-click="true"
              startIcon={<AssignmentIndIcon sx={{ fontSize: 15 }} />}
              onClick={(e) => {
                e.stopPropagation();
                onAssignAnalyst(sample);
              }}
              sx={{
                fontSize: 12,
                py: 0.25,
                px: 1,
                textTransform: "none",
                fontWeight: 600,
                borderRadius: 1.5
              }}
            >
              {t("register.assignAnalyst")}
            </Button>
          </Tooltip>
        ) : (
          <Chip
            size="small"
            label={t("register.unassigned")}
            variant="outlined"
            sx={{ fontSize: 12, height: 20, color: "text.disabled", borderColor: "divider" }}
          />
        );
      }
      case "sampleStatus":
        return <OverallSampleStatusBadge status={sample.status} />;
      case "due":
        return sample.dueAt ? (
          <Tooltip title={t("register.dueTooltip", { date: formatReceivedDate(sample.dueAt) })}>
            <Typography
              component="span"
              sx={{
                fontSize: 12,
                fontWeight: sample.isOverdue ? 700 : 500,
                color: sample.isOverdue ? theme.custom.status.detected.text : "text.secondary",
                whiteSpace: "nowrap"
              }}
            >
              {dueLabel(sample.dueAt, sample.isOverdue)}
            </Typography>
          </Tooltip>
        ) : (
          <Typography sx={{ fontSize: 12, color: "text.disabled" }}>—</Typography>
        );
      case "tests":
        // Progress bar + status summary
        return (
          <Box sx={{ display: "grid", gap: 0.75, justifyItems: "start", minWidth: 130 }}>
            {sample.assignedTests.length > 0 && (
              <Box
                aria-hidden="true"
                sx={{ display: "flex", gap: "2px", height: 6, width: "100%", maxWidth: 180 }}
              >
                {sample.assignedTests.map((t) => (
                  <Box
                    key={t.testOrderId}
                    sx={{ flex: 1, borderRadius: "2px", bgcolor: theme.custom.status[testSegmentTone(effectiveTestStatus(t))].text, opacity: testSegmentTone(effectiveTestStatus(t)) === "pending" ? 0.35 : 1 }}
                  />
                ))}
              </Box>
            )}
            <TestStatusSummaryCell
              sample={sample}
              onTestClick={onTestClick}
              onViewAllTests={onViewSummary}
              onPrepareSample={onPrepareSample}
              onSelectTests={onSelectTests}
            />
          </Box>
        );
      case "nextAction": {
        const nextAction = getNextAction(sample);
        if (!nextAction) {
          return <Typography sx={{ fontSize: 12, color: "text.disabled" }}>—</Typography>;
        }
        if (nextAction.kind !== "wait" && nextAction.gate) {
          return (
            <GatedNextAction sampleId={sample.sampleId} action={nextAction.gate}>
              <Button
                size="small"
                variant="contained"
                data-no-row-click="true"
                onClick={(e) => {
                  e.stopPropagation();
                  nextAction.run?.();
                }}
                sx={{ fontSize: 12, fontWeight: 600, textTransform: "none", whiteSpace: "nowrap", minWidth: 110 }}
              >
                {nextAction.label}
              </Button>
            </GatedNextAction>
          );
        }
        return nextAction.kind === "wait" ? (
          <Chip
            size="small"
            icon={<HourglassTopIcon sx={{ fontSize: "14px !important" }} />}
            label={nextAction.label}
            sx={{ fontSize: 12, fontWeight: 600, bgcolor: theme.custom.status.info.bg, color: theme.custom.status.info.text }}
          />
        ) : (
          <Button
            size="small"
            variant={nextAction.kind === "primary" ? "contained" : "outlined"}
            data-no-row-click="true"
            onClick={(e) => {
              e.stopPropagation();
              nextAction.run?.();
            }}
            sx={{ fontSize: 12, fontWeight: 600, textTransform: "none", whiteSpace: "nowrap", minWidth: 110 }}
          >
            {nextAction.label}
          </Button>
        );
      }
    }
  };

  const renderActions = (sample: SampleRecord) => (
    <SampleActionMenu
      sample={sample}
      onViewSummary={onViewSummary}
      onEdit={onEdit}
      onViewReport={onViewReport}
      onViewAuditHistory={onViewAuditHistory}
      onPrepareSample={onPrepareSample}
      onAssignAnalyst={onAssignAnalyst}
      onVoid={onVoid}
      onAddLaboratory={onAddLaboratory}
      onSelectTests={onSelectTests}
    />
  );

  const column = (
    key: RegisterColumnKey,
    label: string,
    sortValue?: (s: SampleRecord) => string | number | null | undefined
  ): RegisterColumn<SampleRecord> => ({
    key,
    label,
    sortable: Boolean(sortValue),
    sortValue,
    render: (sample) => renderCell(key, sample, 0)
  });

  const columns: RegisterColumn<SampleRecord>[] = SAMPLE_COLUMN_DEFS.map((d) => ({
    ...column(d.key, t(`register.columns.${d.key}`), d.sortValue),
    exportLabel: englishT()(`register.columns.${d.key}`),
    exportValue: (s: SampleRecord) => d.exportValue(s, englishT(), nextActionCaps)
  }));

  // Retest children of an expanded sample, each with the same visible columns
  // and the same row behaviour as a row of the table; their own retests follow.
  const renderChildren = (parent: SampleRecord, level: number): React.ReactNode =>
    (childrenByParentId.get(parent.sampleId) || []).map((child) => (
      <React.Fragment key={child.sampleId}>
        <TableRow
          hover
          tabIndex={0}
          onClick={(e) => handleRowClick(child, e)}
          onKeyDown={(e) => {
            if ((e.key === "Enter" || e.key === " ") && e.target === e.currentTarget) {
              e.preventDefault();
              handleRowClick(child);
            }
          }}
          sx={rowSxFor(child, level)}
        >
          {tableState.state.columns.map((key) => (
            <TableCell key={key} sx={{ py: 0.5 }}>
              {renderCell(key as RegisterColumnKey, child, level)}
            </TableCell>
          ))}
          <TableCell align="center" sx={{ py: 0.5 }}>{renderActions(child)}</TableCell>
        </TableRow>
        {expandedIds.has(child.sampleId) && renderChildren(child, level + 1)}
      </React.Fragment>
    ));

  const hasRetests = childrenByParentId.size > 0;

  return (
    <>
      <RegisterTable<SampleRecord>
        columns={columns}
        rows={topLevelSamples}
        getRowId={getSampleRowId}
        empty={{ title: t("register.emptyTitle") }}
        tableKey={tableKey}
        tableState={tableState}
        toolbar={<TableToolbar tableKey={tableKey} tableState={tableState} showSearch={false} />}
        onRowClick={handleRowClick}
        rowSx={(sample) => rowSxFor(sample, 0)}
        trailingCell={{ label: t("register.actionsColumn"), render: renderActions }}
        selection={selection}
        alwaysSelectable
        selectionLabels={{
          row: checkboxLabel,
          page: (count, allSelected) =>
            allSelected
              ? t("register.deselectAllPage", { count })
              : t("register.selectAllPage", { count })
        }}
        pageSelectionIds={pageSelectionIds}
        bulkActions={bulkActions}
        exportLabel={tCommon("table.exportSelected")}
        totalMatching={serverTotal}
        loadAllMatching={loadAllMatching}
        serverPaged={
          serverTotal !== undefined
            ? {
                totalCount: serverTotal,
                sortableColumns: serverSortableColumns,
                onSortChange: onServerSortChange,
                sort: serverSort
              }
            : undefined
        }
        renderExpanded={hasRetests ? (sample) => (
          <Table size="small" aria-label={t("register.retestsOfAria", { name: sample.displayName })}>
            <TableBody>{renderChildren(sample, 1)}</TableBody>
          </Table>
        ) : undefined}
        isExpandable={(sample) => (childrenByParentId.get(sample.sampleId) || []).length > 0}
        expandedIds={expandedIds}
        onToggleExpand={toggleExpand}
      />

      {/* Read-Only Controlled Item Documents Dialog */}
      <ReadOnlyItemDocumentsDialog
        open={Boolean(activeDocSample)}
        itemId={activeDocSample?.itemId ?? null}
        itemName={activeDocSample?.displayName ?? ""}
        category={activeDocSample?.category}
        onClose={() => setActiveDocSample(null)}
      />
    </>
  );
}

// Many rows share an item, so each item's document count is fetched once
// and reused for a few minutes instead of once per row on every render.
const DOC_COUNT_TTL_MS = 5 * 60 * 1000;
const docCountRequests = new Map<number, { at: number; request: Promise<number> }>();
export function clearSampleDocCountCache() {
  docCountRequests.clear();
}
function getDocCount(itemId: number): Promise<number> {
  const cached = docCountRequests.get(itemId);
  if (cached && Date.now() - cached.at < DOC_COUNT_TTL_MS) return cached.request;
  const request = Promise.all([
    ItemDocumentService.getDocumentCountForItem(itemId).catch(() => 0),
    ItemStmService.getForItem(itemId).catch(() => null),
  ])
    .then(([vrCount, stm]) => vrCount + (stm?.status === "Effective" ? 1 : 0))
    .catch(() => {
      docCountRequests.delete(itemId);
      return 0;
    });
  docCountRequests.set(itemId, { at: Date.now(), request });
  return request;
}

function SampleDocIndicator({
  sample,
  onOpenDocs,
}: {
  sample: SampleRecord;
  onOpenDocs: (sample: SampleRecord) => void;
}) {
  const { t } = useTranslation("receiving");
  const [docCount, setDocCount] = useState<number | null>(null);

  useEffect(() => {
    if (!sample.itemId) return;

    let cancelled = false;
    getDocCount(sample.itemId).then((count) => { if (!cancelled) setDocCount(count); });

    return () => { cancelled = true; };
  }, [sample.itemId]);

  if (!sample.itemId || docCount === null) return null;

  return (
    <Box
      component="span"
      role="button"
      tabIndex={0}
      data-no-row-click="true"
      onClick={(e) => {
        e.stopPropagation();
        onOpenDocs(sample);
      }}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          e.stopPropagation();
          onOpenDocs(sample);
        }
      }}
      sx={{
        display: "inline-flex",
        alignItems: "center",
        gap: 0.5,
        mt: 0.5,
        px: 0.75,
        py: 0.2,
        borderRadius: 1,
        fontSize: 12,
        fontWeight: 600,
        bgcolor: docCount > 0 ? "action.hover" : "transparent",
        color: docCount > 0 ? "primary.main" : "text.secondary",
        border: "1px solid",
        borderColor: docCount > 0 ? "primary.light" : "divider",
        cursor: "pointer",
        "&:hover": {
          bgcolor: "action.selected",
        },
      }}
      aria-label={t("register.docsAria", { name: sample.displayName, count: docCount })}
      title={t("register.docsTooltip")}
    >
      <DescriptionIcon style={{ fontSize: 11 }} />
      {t("register.docsCount", { count: docCount })}
    </Box>
  );
}

