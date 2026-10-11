import React, { useState, useEffect } from "react";
import {
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TablePagination,
  Box,
  Typography,
  Tooltip,
  Button,
  Checkbox,
  Avatar,
  IconButton,
  Popover,
  FormGroup,
  FormControlLabel,
  useTheme
} from "@mui/material";
import ViewColumnIcon from "@mui/icons-material/ViewColumn";
import HourglassTopIcon from "@mui/icons-material/HourglassTop";
import DescriptionIcon from "@mui/icons-material/Description";
import AssignmentIndIcon from "@mui/icons-material/AssignmentInd";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowRightIcon from "@mui/icons-material/KeyboardArrowRight";
import SubdirectoryArrowRightIcon from "@mui/icons-material/SubdirectoryArrowRight";
import { Chip } from "@mui/material";
import { SampleRecord, TestOrderSummary } from "../types/receivingTypes";
import { CategoryBadge, CauseBadge, categoryLabel } from "../../../components/StatusBadge";
import { StatusTone } from "../../../theme/statusTokens";
import { TestStatusSummaryCell } from "./TestStatusSummaryCell";
import { SampleActionMenu } from "./SampleActionMenu";
import { tableHeadSx } from "../../../theme";
import { ReadOnlyItemDocumentsDialog } from "../../../components/ReadOnlyItemDocumentsDialog";
import { ItemDocumentService } from "../../laboratoryConfiguration/items/services/ItemDocumentService";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { isInteractiveElement } from "../../../utils/isInteractiveElement";

interface Props {
  samples: SampleRecord[];
  selectedSampleId?: number | null;
  checkedSampleIds?: Set<number>;
  onToggleCheck?: (sampleId: number, checked: boolean) => void;
  onToggleCheckMany?: (sampleIds: number[], checked: boolean) => void;
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
  // localStorage key for this register's column choice, so each page that
  // uses the table remembers its own set.
  columnsStorageKey?: string;
}

// Columns the user can show or hide (the item column and actions always
// show). Whatever is hidden among received date, #, item type, cause and
// sampled-by is summarised under the item name instead, so hiding a column
// never hides the information.
export type RegisterColumnKey =
  | "receivedAt"
  | "sampleId"
  | "category"
  | "cause"
  | "sampledBy"
  | "batchControl"
  | "assignedTo"
  | "sampleStatus"
  | "tests"
  | "nextAction";

export const REGISTER_COLUMNS: { key: RegisterColumnKey; label: string }[] = [
  { key: "receivedAt", label: "Received At" },
  { key: "sampleId", label: "Sample #" },
  { key: "category", label: "Item Type" },
  { key: "cause", label: "Cause" },
  { key: "sampledBy", label: "Sampled By" },
  { key: "batchControl", label: "Batch / Control No." },
  { key: "assignedTo", label: "Assigned To" },
  { key: "sampleStatus", label: "Sample Status" },
  { key: "tests", label: "Tests" },
  { key: "nextAction", label: "Next Action" }
];

export const DEFAULT_REGISTER_COLUMNS: RegisterColumnKey[] = ["batchControl", "assignedTo", "sampleStatus", "tests", "nextAction"];

function loadColumns(storageKey: string): Set<RegisterColumnKey> {
  try {
    const raw = localStorage.getItem(storageKey);
    if (raw) {
      const valid = new Set(REGISTER_COLUMNS.map((c) => c.key));
      const parsed = (JSON.parse(raw) as string[]).filter((k): k is RegisterColumnKey => valid.has(k as RegisterColumnKey));
      return new Set(parsed);
    }
  } catch {
    // Storage blocked or corrupt - fall back to the default set.
  }
  return new Set(DEFAULT_REGISTER_COLUMNS);
}

function saveColumns(storageKey: string, cols: Set<RegisterColumnKey>) {
  try {
    localStorage.setItem(storageKey, JSON.stringify(Array.from(cols)));
  } catch {
    // Not persisted; the choice still applies for this visit.
  }
}

const CLOSED_STATUSES = new Set(["Rejected", "Voided", "Cancelled", "RetestRequested"]);
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

interface NextAction {
  label: string;
  // "wait" renders a passive chip; anything else renders a button.
  kind: "primary" | "secondary" | "wait";
  run?: () => void;
}

// Same statuses StatusBadge.tsx's central token map covers, but collapsed
// to this table's own coarser labels (e.g. RetestRequested/Cancelled/Voided
// all read "Cancelled / Voided" here) - so this stays a local label+tone
// map rather than reusing StatusBadge directly, while still sourcing its
// colors from the one shared theme.custom.status table (not its own hex).
const SAMPLE_STATUS_MAP: Record<string, { label: string; tone: StatusTone }> = {
  Received: { label: "Received", tone: "pending" },
  InTesting: { label: "Under Testing", tone: "info" },
  UnderReview: { label: "Pending Review", tone: "action" },
  UnderApproval: { label: "Pending Approval", tone: "action" },
  PendingReview: { label: "Pending Review", tone: "action" },
  Approved: { label: "Approved", tone: "notDetected" },
  Rejected: { label: "Rejected", tone: "detected" },
  RetestRequested: { label: "Cancelled / Voided", tone: "pending" },
  Cancelled: { label: "Cancelled / Voided", tone: "pending" },
  Voided: { label: "Cancelled / Voided", tone: "pending" }
};

function OverallSampleStatusBadge({ status }: { status: string }) {
  const theme = useTheme();
  const config = SAMPLE_STATUS_MAP[status] || { label: status, tone: "pending" as StatusTone };
  const tokens = theme.custom.status[config.tone];

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
      {config.label}
    </Box>
  );
}

export function SampleRegisterTable({
  samples,
  selectedSampleId,
  checkedSampleIds,
  onToggleCheck,
  onToggleCheckMany,
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
  columnsStorageKey = "microlims.sampleRegister.columns"
}: Props) {
  const theme = useTheme();
  const { permissions } = useAuth();
  const isAuthorizedToAssign = permissions.includes(PERMISSIONS.SAMPLES_ASSIGN_ANALYST);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);
  const [activeDocSample, setActiveDocSample] = useState<SampleRecord | null>(null);
  const [expandedSampleIds, setExpandedSampleIds] = useState<Set<number>>(new Set());
  const [visibleColumns, setVisibleColumns] = useState<Set<RegisterColumnKey>>(() => loadColumns(columnsStorageKey));
  const [columnsAnchor, setColumnsAnchor] = useState<HTMLElement | null>(null);
  const show = (key: RegisterColumnKey) => visibleColumns.has(key);

  const updateColumns = (next: Set<RegisterColumnKey>) => {
    setVisibleColumns(next);
    saveColumns(columnsStorageKey, next);
  };
  const toggleColumn = (key: RegisterColumnKey) => {
    const next = new Set(visibleColumns);
    if (next.has(key)) next.delete(key);
    else next.add(key);
    updateColumns(next);
  };

  // What the analyst should do next for this sample, routed to the same
  // handlers the row's menus already use. It reads only statuses the server
  // reported; no laboratory rule is decided here.
  const getNextAction = (sample: SampleRecord): NextAction | null => {
    if (CLOSED_STATUSES.has(sample.status)) return null;
    if (sample.preparationStatus === "NeedsPreparation") {
      return { label: "Prepare", kind: "primary", run: () => onPrepareSample(sample) };
    }
    if (sample.status === "UnderReview" || sample.status === "PendingReview") {
      return { label: "Review", kind: "primary", run: () => onViewSummary(sample) };
    }
    if (sample.status === "UnderApproval") {
      return { label: "Approve", kind: "primary", run: () => onViewSummary(sample) };
    }
    if (sample.status === "Approved") {
      return { label: "View Report", kind: "secondary", run: () => onViewReport(sample) };
    }
    const tests = sample.assignedTests || [];
    const ready = tests.find((t) => ["ReadyToRead", "EnterResult"].includes(effectiveTestStatus(t)));
    if (ready) {
      return { label: tests.length > 1 ? `Record ${ready.testCode}` : "Record Result", kind: "primary", run: () => onTestClick(ready, sample) };
    }
    const hasAnalyst = Boolean(sample.assignedAnalystName || tests.some((t) => t.assignedAnalystName));
    if (!hasAnalyst && isAuthorizedToAssign && onAssignAnalyst) {
      return { label: "Assign", kind: "secondary", run: () => onAssignAnalyst(sample) };
    }
    const notStarted = tests.find((t) => ["Pending", "Waiting", "NotStarted"].includes(effectiveTestStatus(t)));
    if (notStarted) {
      return { label: tests.length > 1 ? `Start ${notStarted.testCode}` : "Start Test", kind: "secondary", run: () => onTestClick(notStarted, sample) };
    }
    if (tests.some((t) => ["InProgress", "Running", "Incubating"].includes(effectiveTestStatus(t)))) {
      return { label: "In Progress", kind: "wait" };
    }
    return null;
  };

  const handleChangePage = (_: unknown, newPage: number) => {
    setPage(newPage);
  };

  const handleChangeRowsPerPage = (event: React.ChangeEvent<HTMLInputElement>) => {
    setRowsPerPage(parseInt(event.target.value, 10));
    setPage(0);
  };

  const toggleExpand = (sampleId: number, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    setExpandedSampleIds((prev) => {
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
  const sampleIdSet = new Set(samples.map((s) => s.sampleId));
  const childrenByParentId = new Map<number, SampleRecord[]>();
  const topLevelSamples: SampleRecord[] = [];

  for (const sample of samples) {
    if (sample.originSampleId != null && sampleIdSet.has(sample.originSampleId)) {
      const list = childrenByParentId.get(sample.originSampleId) || [];
      list.push(sample);
      childrenByParentId.set(sample.originSampleId, list);
    } else {
      topLevelSamples.push(sample);
    }
  }

  // The parent owns filtering, so narrowing a filter shrinks this list under
  // whatever page the user is on - which used to render the "no samples found"
  // empty state over a perfectly non-empty result set. Clamp rather than reset
  // to page 0, so a background refresh does not yank the user back to page 1.
  //
  // The clamp is applied during render, not only in the effect below: effects
  // run after commit, so relying on the effect alone would hand MUI an
  // out-of-range page prop for one render and trip its console warning.
  const lastPageIndex = Math.max(0, Math.ceil(topLevelSamples.length / rowsPerPage) - 1);
  const effectivePage = Math.min(page, lastPageIndex);

  useEffect(() => {
    if (page !== effectivePage) {
      setPage(effectivePage);
    }
  }, [page, effectivePage]);

  const paginatedTopLevelSamples = topLevelSamples.slice(
    effectivePage * rowsPerPage,
    effectivePage * rowsPerPage + rowsPerPage
  );

  // Every id the current page renders, including retest children of any
  // expanded parent - "select all" should mean what is on screen, not the
  // whole filtered set, which could be hundreds of rows away.
  const idsOnPage: number[] = [];
  const collectIds = (sample: SampleRecord) => {
    idsOnPage.push(sample.sampleId);
    if (expandedSampleIds.has(sample.sampleId)) {
      (childrenByParentId.get(sample.sampleId) || []).forEach(collectIds);
    }
  };
  paginatedTopLevelSamples.forEach(collectIds);

  const checkedOnPage = idsOnPage.filter((id) => checkedSampleIds?.has(id)).length;
  const allOnPageChecked = idsOnPage.length > 0 && checkedOnPage === idsOnPage.length;
  const someOnPageChecked = checkedOnPage > 0 && !allOnPageChecked;

  const formatReceivedDate = (d: string) => {
    try {
      const date = new Date(d);
      return date.toLocaleDateString("en-GB", {
        day: "2-digit",
        month: "short",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit"
      });
    } catch {
      return d;
    }
  };

  const renderSampleRow = (sample: SampleRecord, level = 0): React.ReactNode => {
    const assignedAnalystName =
      sample.assignedAnalystName ||
      sample.assignedTests.find((t) => t.assignedAnalystName)?.assignedAnalystName;

    const isSelected = selectedSampleId === sample.sampleId;
    const needsPreparation = sample.preparationStatus === "NeedsPreparation";
    const children = childrenByParentId.get(sample.sampleId) || [];
    const hasChildren = children.length > 0;
    const isExpanded = expandedSampleIds.has(sample.sampleId);
    const isNested = level > 0;
    const nextAction = show("nextAction") ? getNextAction(sample) : null;
    const itemMeta = [
      !show("sampleId") ? `#${sample.sampleId}` : null,
      !show("category") ? categoryLabel(sample.category) : null,
      !show("cause") ? sample.causeOfTesting : null,
      !show("sampledBy") ? sample.sampledBy : null,
      !show("receivedAt") ? formatReceivedDate(sample.receivedAt) : null
    ].filter((v): v is string => Boolean(v));

    return (
      <React.Fragment key={sample.sampleId}>
        <TableRow
          hover
          tabIndex={0}
          role="row"
          onClick={(e) => {
            if (isInteractiveElement(e.target, e.currentTarget)) {
              return;
            }
            onSelectSample?.(sample);
          }}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") {
              if (isInteractiveElement(e.target, e.currentTarget)) {
                return;
              }
              e.preventDefault();
              onSelectSample?.(sample);
            }
          }}
          sx={{
            cursor: "pointer",
            bgcolor: isSelected
              ? theme.custom.status.purple.bg
              : isNested
              ? "action.hover"
              : "inherit",
            borderLeft: isSelected
              ? `4px solid ${theme.custom.status.purple.border}`
              : needsPreparation
              ? `4px solid ${theme.custom.status.inconclusive.border}`
              : isNested
              ? `4px solid ${theme.palette.warning.main}`
              : "4px solid transparent",
            "&:last-child td, &:last-child th": { border: 0 },
            "&:hover": {
              bgcolor: isSelected
                ? theme.custom.status.purple.bg
                : isNested
                ? "action.selected"
                : "action.hover"
            }
          }}
        >
          {/* Received At */}
          {show("receivedAt") && (
            <TableCell sx={{ fontSize: 12, color: "text.secondary", whiteSpace: "nowrap" }}>
              {formatReceivedDate(sample.receivedAt)}
            </TableCell>
          )}

          {/* # ID */}
          {show("sampleId") && (
            <TableCell sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>
              #{sample.sampleId}
            </TableCell>
          )}

          {/* Item / Reference */}
          <TableCell sx={{ pl: isNested ? `${level * 24 + 16}px` : undefined }}>
            <Box sx={{ display: "flex", alignItems: "flex-start", gap: 0.75 }}>
              {onToggleCheck && (
                <Checkbox
                  size="small"
                  checked={Boolean(checkedSampleIds?.has(sample.sampleId))}
                  onChange={(e) => {
                    e.stopPropagation();
                    onToggleCheck(sample.sampleId, e.target.checked);
                  }}
                  onClick={(e) => e.stopPropagation()}
                  sx={{ p: 0.25, mr: 0.25 }}
                  slotProps={{
                    input: {
                      "aria-label": `Select ${sample.displayName} (${sample.referenceNumber}) for grouped actions`
                    }
                  }}
                />
              )}
              {isNested && (
                <SubdirectoryArrowRightIcon
                  sx={{ fontSize: 16, color: "warning.main", mt: 0.25, flexShrink: 0 }}
                />
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
                    {sample.referenceNumber}
                  </Typography>
                  {isNested && (
                    <Chip
                      size="small"
                      label="Retest"
                      color="warning"
                      variant="outlined"
                      sx={{ height: 20, fontSize: 12, fontWeight: 700 }}
                    />
                  )}
                  {sample.oosGroupCode && (
                    <Tooltip title={`OOS Investigation Chain: ${sample.oosGroupCode}`}>
                      <Chip
                        size="small"
                        label={sample.oosGroupCode}
                        sx={{ height: 20, fontSize: 12, fontFamily: "monospace", fontWeight: 600 }}
                      />
                    </Tooltip>
                  )}
                  {hasChildren && (
                    <Tooltip title={isExpanded ? "Collapse retest chain" : "Expand retest chain"}>
                      <Chip
                        size="small"
                        data-no-row-click="true"
                        icon={
                          isExpanded ? (
                            <KeyboardArrowDownIcon sx={{ fontSize: "14px !important" }} />
                          ) : (
                            <KeyboardArrowRightIcon sx={{ fontSize: "14px !important" }} />
                          )
                        }
                        label={`${children.length} retest${children.length === 1 ? "" : "s"}`}
                        onClick={(e) => toggleExpand(sample.sampleId, e)}
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
                {itemMeta.length > 0 && (
                  <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.25 }}>
                    {itemMeta.join(" · ")}
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
          </TableCell>

          {/* Item Type */}
          {show("category") && (
            <TableCell>
              <CategoryBadge category={sample.category} />
            </TableCell>
          )}

          {/* Cause of Testing */}
          {show("cause") && (
            <TableCell>
              <CauseBadge label={sample.causeOfTesting || "—"} />
            </TableCell>
          )}

          {/* Sampled By */}
          {show("sampledBy") && (
            <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
              {sample.sampledBy || "—"}
            </TableCell>
          )}

          {/* Batch / Control No. */}
          {show("batchControl") && (
          <TableCell>
            {sample.batchNumber ? (
              <Box sx={{ fontSize: 12 }}>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  B: {sample.batchNumber}
                </Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  C: {sample.controlNumber || "—"}
                </Typography>
              </Box>
            ) : (
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                C: {sample.controlNumber || "—"}
              </Typography>
            )}
          </TableCell>
          )}

          {/* Assigned To */}
          {show("assignedTo") && (
          <TableCell>
            {assignedAnalystName ? (
              isAuthorizedToAssign && onAssignAnalyst ? (
                <Tooltip title="Click to reassign analyst (Section Head)">
                  <Chip
                    size="small"
                    data-no-row-click="true"
                    avatar={<Avatar sx={initialsSx}>{initialsOf(assignedAnalystName)}</Avatar>}
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
                <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
                  <Avatar sx={{ ...initialsSx, width: 22, height: 22 }}>{initialsOf(assignedAnalystName)}</Avatar>
                  <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }} noWrap>
                    {assignedAnalystName}
                  </Typography>
                </Box>
              )
            ) : isAuthorizedToAssign && onAssignAnalyst ? (
              <Tooltip title="Assign responsible analyst (Section Head)">
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
                  Assign Analyst
                </Button>
              </Tooltip>
            ) : (
              <Chip
                size="small"
                label="Unassigned"
                variant="outlined"
                sx={{ fontSize: 12, height: 20, color: "text.disabled", borderColor: "divider" }}
              />
            )}
          </TableCell>
          )}

          {/* Sample Status */}
          {show("sampleStatus") && (
            <TableCell>
              <OverallSampleStatusBadge status={sample.status} />
            </TableCell>
          )}

          {/* Tests: progress bar + status summary */}
          {show("tests") && (
            <TableCell>
              <Box sx={{ display: "grid", gap: 0.75, justifyItems: "start", minWidth: 150 }}>
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
                />
              </Box>
            </TableCell>
          )}

          {/* Next Action */}
          {show("nextAction") && (
            <TableCell>
              {nextAction ? (
                nextAction.kind === "wait" ? (
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
                )
              ) : (
                <Typography sx={{ fontSize: 12, color: "text.disabled" }}>—</Typography>
              )}
            </TableCell>
          )}

          {/* Actions */}
          <TableCell align="center">
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
            />
          </TableCell>
        </TableRow>

        {/* Recursive rendering of children when expanded */}
        {hasChildren && isExpanded && children.map((child) => renderSampleRow(child, level + 1))}
      </React.Fragment>
    );
  };

  return (
    <Paper
      elevation={0}
      sx={{
        border: "1px solid",
        borderColor: "divider",
        borderRadius: 2,
        overflow: "hidden",
        bgcolor: "background.paper"
      }}
    >
      <Box sx={{ overflowX: "auto", maxHeight: { md: "calc(100vh - 240px)" } }}>
        <Table size="small" stickyHeader sx={{ minWidth: 640 + visibleColumns.size * 60 }}>
          <TableHead sx={tableHeadSx}>
            <TableRow>
              {show("receivedAt") && (
                <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 140 }}>Received At</TableCell>
              )}
              {show("sampleId") && <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 50 }}>#</TableCell>}
              <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 180 }}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                  {onToggleCheckMany && (
                    <Checkbox
                      size="small"
                      checked={allOnPageChecked}
                      indeterminate={someOnPageChecked}
                      disabled={idsOnPage.length === 0}
                      onChange={(e) => onToggleCheckMany(idsOnPage, e.target.checked)}
                      sx={{ p: 0.25 }}
                      slotProps={{
                        input: {
                          "aria-label": allOnPageChecked
                            ? `Deselect all ${idsOnPage.length} samples on this page`
                            : `Select all ${idsOnPage.length} samples on this page`
                        }
                      }}
                    />
                  )}
                  Item / Reference
                </Box>
              </TableCell>
              {show("category") && <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 110 }}>Item Type</TableCell>}
              {show("cause") && <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 120 }}>Cause</TableCell>}
              {show("sampledBy") && <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 110 }}>Sampled By</TableCell>}
              {show("batchControl") && <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 140 }}>Batch / Control No.</TableCell>}
              {show("assignedTo") && <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 130 }}>Assigned To</TableCell>}
              {show("sampleStatus") && <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 140 }}>Sample Status</TableCell>}
              {show("tests") && <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 170 }}>Tests</TableCell>}
              {show("nextAction") && <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 130 }}>Next Action</TableCell>}
              <TableCell align="center" sx={{ fontWeight: 700, fontSize: 12, width: 120 }}>
                <Box sx={{ display: "inline-flex", alignItems: "center", gap: 0.5 }}>
                  Actions
                  <Tooltip title="Choose columns">
                    <IconButton
                      size="small"
                      aria-label="Choose visible columns"
                      aria-haspopup="dialog"
                      onClick={(e) => setColumnsAnchor(e.currentTarget)}
                      sx={{ color: "inherit" }}
                    >
                      <ViewColumnIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </Box>
              </TableCell>
            </TableRow>
          </TableHead>

          <TableBody>
            {paginatedTopLevelSamples.length === 0 ? (
              <TableRow>
                <TableCell colSpan={2 + visibleColumns.size} align="center" sx={{ py: 6 }}>
                  <Typography sx={{ color: "text.secondary", fontSize: 14 }}>
                    No samples found matching the filter criteria.
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              paginatedTopLevelSamples.map((sample) => renderSampleRow(sample, 0))
            )}
          </TableBody>
        </Table>
      </Box>

      {topLevelSamples.length > 0 && (
        <TablePagination
          rowsPerPageOptions={[10, 25, 50, 100]}
          component="div"
          count={topLevelSamples.length}
          rowsPerPage={rowsPerPage}
          page={effectivePage}
          onPageChange={handleChangePage}
          onRowsPerPageChange={handleChangeRowsPerPage}
          sx={{
            borderTop: "1px solid",
            borderColor: "divider",
            "& .MuiTablePagination-selectLabel, & .MuiTablePagination-displayedRows": {
              fontSize: 12
            }
          }}
        />
      )}

      <Popover
        open={Boolean(columnsAnchor)}
        anchorEl={columnsAnchor}
        onClose={() => setColumnsAnchor(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
      >
        <Box sx={{ p: 2, minWidth: 220 }}>
          <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 0.5, color: "text.secondary" }}>
            Visible Columns
          </Typography>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1, maxWidth: 220 }}>
            Hidden details still show under the item name.
          </Typography>
          <FormGroup>
            {REGISTER_COLUMNS.map((col) => (
              <FormControlLabel
                key={col.key}
                label={<Typography sx={{ fontSize: 13 }}>{col.label}</Typography>}
                control={
                  <Checkbox size="small" checked={visibleColumns.has(col.key)} onChange={() => toggleColumn(col.key)} />
                }
              />
            ))}
          </FormGroup>
          <Button size="small" onClick={() => updateColumns(new Set(DEFAULT_REGISTER_COLUMNS))} sx={{ mt: 1, textTransform: "none" }}>
            Reset to default
          </Button>
        </Box>
      </Popover>

      {/* Read-Only Controlled Item Documents Dialog */}
      <ReadOnlyItemDocumentsDialog
        open={Boolean(activeDocSample)}
        itemId={activeDocSample?.itemId ?? null}
        itemName={activeDocSample?.displayName ?? ""}
        category={activeDocSample?.category}
        onClose={() => setActiveDocSample(null)}
      />
    </Paper>
  );
}

// Many rows share an item, so each item's document count is fetched once
// and reused for a few minutes instead of once per row on every render.
const DOC_COUNT_TTL_MS = 5 * 60 * 1000;
const docCountRequests = new Map<number, { at: number; request: Promise<number> }>();
function getDocCount(itemId: number): Promise<number> {
  const cached = docCountRequests.get(itemId);
  if (cached && Date.now() - cached.at < DOC_COUNT_TTL_MS) return cached.request;
  const request = ItemDocumentService.getDocumentCountForItem(itemId).catch(() => {
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
      aria-label={`View controlled item documents for ${sample.displayName} (${docCount} available)`}
      title="View Controlled Item Documents (SOP & Verification Report)"
    >
      <DescriptionIcon style={{ fontSize: 11 }} />
      {docCount > 0 ? `${docCount} Docs` : "0 Docs"}
    </Box>
  );
}

