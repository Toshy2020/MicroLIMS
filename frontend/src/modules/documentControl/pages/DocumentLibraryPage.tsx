import { monospaceFontFamily } from "../../../theme/palette";
import { compactChipStrongSx, compactChipSx } from "../documentControlStyles";
import { useState, useEffect, useCallback, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "../../../i18n";
import {
  Box,
  Typography,
  Button,
  TextField,
  MenuItem,
  InputAdornment,
  Paper,
  IconButton,
  Tooltip,
  Chip,
  ToggleButton,
  Alert
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import AddIcon from "@mui/icons-material/Add";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import PictureAsPdfOutlinedIcon from "@mui/icons-material/PictureAsPdfOutlined";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import ClearIcon from "@mui/icons-material/Clear";

import { PageHeader } from "../../../components/PageHeader";
import { RegisterTable, type RegisterColumn } from "../../../components/lab";
import { TableToolbar } from "../../../components/lab/table/TableToolbar";
import { useTableState } from "../../../components/lab/table/useTableState";
import { toast } from "sonner";
import { StatusBadge } from "../../../components/StatusBadge";
import { documentMasterStatusLabel } from "../documentStatusDisplay";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { serverMessage } from "../../../utils/errorMessage";
import { documentControlService } from "../services/documentControlService";
import { RegisterDocumentDialog } from "../components/RegisterDocumentDialog";
import { ControlledPdfViewer } from "../components/ControlledPdfViewer";
import { VoidDocumentDialog } from "../components/VoidDocumentDialog";
import type {
  DocumentMasterSummaryDto,
  DocumentTypeDto,
  DocumentDepartmentDto,
  DocumentRevisionStatus
} from "../types/documentControlTypes";
import { clickable } from "../../../utils/clickable";
import { formatLabCalendarDate } from "../../../utils/formatDate";

const TABLE_KEY = "documentControl.library";

const REVISION_STATUSES: readonly DocumentRevisionStatus[] = [
  "Draft",
  "InReview",
  "AwaitingApproval",
  "FutureEffective",
  "Effective",
  "Superseded",
  "Obsolete",
  "Cancelled"
];

const DOCUMENT_LIBRARY_SORTABLE_COLUMNS: ReadonlySet<string> = new Set([
  "companyDocumentCode",
  "microLimsDocumentId",
  "title",
  "effectiveDate"
]);

function textFilter(filters: Record<string, string | string[]>, name: string): string {
  const value = filters[name];
  return typeof value === "string" ? value : "";
}

function errorMessage(err: unknown, fallback: string): string {
  return serverMessage(err) || (err instanceof Error && err.message ? err.message : fallback);
}

export function DocumentLibraryPage() {
  const { t } = useTranslation("documentControl");
  const navigate = useNavigate();
  const { role, permissions } = useAuth();
  const isController = role === "SectionHead";
  // Voiding stays Controller-only (a System Administrator is explicitly
  // excluded, FRS-1A §4.1:86), while registration follows the
  // Documents.Register permission the server checks
  // (CanRegisterDocumentMasterAsync), so it needs its own flag.
  const canRegister = permissions.includes(PERMISSIONS.DOCUMENTS_REGISTER);

  // Data state
  const [documents, setDocuments] = useState<DocumentMasterSummaryDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Filter, paging, column choice and saved views live in the shared table state.
  const tableState = useTableState(TABLE_KEY);
  const { setFilter, setSearch, applyState } = tableState;
  const searchTerm = tableState.state.search;
  const selectedType = textFilter(tableState.state.filters, "documentTypeId");
  const selectedDept = textFilter(tableState.state.filters, "departmentId");
  const selectedStatus = textFilter(tableState.state.filters, "revisionStatus");
  const includeCancelledVoided = textFilter(tableState.state.filters, "showVoided") === "true";
  const showOverdueOnly = textFilter(tableState.state.filters, "reviewOverdue") === "true";
  const page = tableState.page;
  const pageSize = tableState.state.pageSize === "all" ? 100 : tableState.state.pageSize;
  const activeSort =
    tableState.state.sort && DOCUMENT_LIBRARY_SORTABLE_COLUMNS.has(tableState.state.sort.key)
      ? tableState.state.sort
      : null;
  const sortBy = activeSort?.key;
  const sortDir = activeSort?.direction;

  const parsedTypeId = selectedType !== "" && Number.isFinite(Number(selectedType)) ? Number(selectedType) : undefined;
  const parsedDeptId = selectedDept !== "" && Number.isFinite(Number(selectedDept)) ? Number(selectedDept) : undefined;
  const parsedStatus = REVISION_STATUSES.find((s) => s === selectedStatus);

  // Reference configurations
  const [types, setTypes] = useState<DocumentTypeDto[]>([]);
  const [departments, setDepartments] = useState<DocumentDepartmentDto[]>([]);

  // Dialogs state
  const [registerOpen, setRegisterOpen] = useState(false);
  const [pdfViewerState, setPdfViewerState] = useState<{
    open: boolean;
    fileId: number | null;
    fileName?: string;
    companyCode: string;
    microLimsId: string;
    revision: string;
    title: string;
    status: string;
  }>({
    open: false,
    fileId: null,
    companyCode: "",
    microLimsId: "",
    revision: "",
    title: "",
    status: ""
  });

  const [voidDialogState, setVoidDialogState] = useState<{
    open: boolean;
    masterId: number;
    code: string;
    title: string;
  }>({
    open: false,
    masterId: 0,
    code: "",
    title: ""
  });

  // Kept apart from `error`, which every library fetch clears.
  const [filtersFailed, setFiltersFailed] = useState(false);

  // Load types and departments
  useEffect(() => {
    Promise.all([
      documentControlService.getTypes(),
      documentControlService.getDepartments()
    ]).then(([t, d]) => {
      setTypes(t);
      setDepartments(d);
    }).catch(() => setFiltersFailed(true));
  }, []);

  const fetchLibrary = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const res = await documentControlService.getLibrary({
        searchTerm: searchTerm.trim() || undefined,
        documentTypeId: parsedTypeId,
        departmentId: parsedDeptId,
        revisionStatus: parsedStatus,
        includeCancelledAndVoided: includeCancelledVoided,
        isOverdue: showOverdueOnly ? true : undefined,
        sortBy,
        sortDir,
        page,
        pageSize
      });
      setDocuments(res.items);
      setTotalCount(res.totalCount);
    } catch (err: unknown) {
      setError(errorMessage(err, t("library.loadError")));
    } finally {
      setLoading(false);
    }
  }, [searchTerm, parsedTypeId, parsedDeptId, parsedStatus, includeCancelledVoided, showOverdueOnly, sortBy, sortDir, page, pageSize, t]);

  useEffect(() => {
    fetchLibrary();
  }, [fetchLibrary]);

  const handleClearFilters = () => {
    applyState(
      { filters: {}, search: "", sort: tableState.state.sort, pageSize: tableState.state.pageSize },
      tableState.viewId
    );
  };

  const handleOpenPdf = useCallback(async (doc: DocumentMasterSummaryDto) => {
    // To view the active PDF, we fetch the master details to get the current file ID
    try {
      const fullDoc = await documentControlService.getById(doc.id);
      const currentRev = fullDoc.revisions.find((r) => r.id === fullDoc.currentEffectiveRevisionId) ||
        fullDoc.revisions[0];
      const pdfFile = currentRev?.files.find((f) => f.fileRole === "ControlledPdf" && f.isActive);

      if (pdfFile) {
        setPdfViewerState({
          open: true,
          fileId: pdfFile.id,
          fileName: pdfFile.fileName,
          companyCode: doc.companyDocumentCode,
          microLimsId: doc.microLimsDocumentId,
          revision: currentRev.revisionNumber,
          title: doc.title,
          status: currentRev.revisionStatus
        });
      } else {
        toast.warning(t("library.noPdfWarning"));
      }
    } catch (err: unknown) {
      toast.error(t("library.loadFileError", { error: errorMessage(err, t("common.unknownError")) }));
    }
  }, [t]);

  const columns = useMemo<RegisterColumn<DocumentMasterSummaryDto>[]>(
    () => [
      {
        key: "companyDocumentCode",
        label: t("library.columns.companyCode"),
        nowrap: true,
        sortable: true,
        render: (doc) => (
          <Typography
            variant="body2"
            sx={{
              fontWeight: 700,
              color: "primary.main",
              cursor: "pointer",
              "&:hover": { textDecoration: "underline" }
            }}
            onClick={() => navigate(`/document-control/documents/${doc.id}`)}
            {...clickable(() => navigate(`/document-control/documents/${doc.id}`))}
          >
            {doc.companyDocumentCode}
          </Typography>
        )
      },
      {
        key: "microLimsDocumentId",
        label: t("library.columns.microLimsId"),
        nowrap: true,
        sortable: true,
        render: (doc) => (
          <Typography variant="caption" sx={{ fontFamily: monospaceFontFamily }}>
            {doc.microLimsDocumentId}
          </Typography>
        )
      },
      {
        key: "title",
        label: t("library.columns.title"),
        sortable: true,
        render: (doc) => (
          <Typography
            variant="body2"
            sx={{
              fontWeight: 500,
              maxWidth: 300,
              overflow: "hidden",
              textOverflow: "ellipsis",
              whiteSpace: "nowrap"
            }}
            title={doc.title}
          >
            {doc.title}
          </Typography>
        )
      },
      {
        key: "documentTypeCode",
        label: t("library.columns.type"),
        render: (doc) => (
          <Chip label={doc.documentTypeCode} size="small" variant="outlined" sx={compactChipSx} />
        )
      },
      {
        key: "departmentName",
        label: t("library.columns.deptSection"),
        render: (doc) => (
          <>
            <Typography
              variant="caption"
              sx={{
                display: "block",
                fontWeight: 600
              }}
            >
              {doc.departmentName}
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {doc.sectionNames && doc.sectionNames.length > 0 ? doc.sectionNames.join(", ") : doc.sectionName}
            </Typography>
          </>
        )
      },
      {
        key: "documentOwnerUserName",
        label: t("library.columns.owner"),
        render: (doc) => <Typography variant="body2">{doc.documentOwnerUserName}</Typography>
      },
      {
        key: "currentRevisionNumber",
        label: t("library.columns.rev"),
        align: "center",
        render: (doc) => (
          <Typography variant="body2" sx={{ fontWeight: 700 }}>
            {doc.currentRevisionNumber || "01"}
          </Typography>
        )
      },
      {
        key: "status",
        label: t("library.columns.status"),
        render: (doc) =>
          doc.recordStatus === "Void" ? (
            <StatusBadge status="Void" label={t("library.voidLabel")} />
          ) : (
            <Box sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}>
              <StatusBadge status={documentMasterStatusLabel(doc)} />
              {doc.currentEffectiveRevisionId && doc.nextReviewDate && new Date(doc.nextReviewDate) < new Date() && (
                <Chip label={t("library.reviewOverdueChip")} color="error" size="small" sx={{ ...compactChipStrongSx, width: "fit-content" }} />
              )}
            </Box>
          )
      },
      {
        key: "files",
        label: t("library.columns.files"),
        align: "center",
        render: (doc) => (
          <Box sx={{ display: "flex", justifyContent: "center", gap: 0.5 }}>
            {doc.hasControlledPdf && (
              <Tooltip title={t("library.viewPdfTooltip")}>
                <IconButton
                  aria-label={t("library.viewPdfAria", { code: doc.companyDocumentCode })}
                  size="small"
                  color="error"
                  onClick={() => handleOpenPdf(doc)}
                  sx={{ p: 0.5 }}
                >
                  <PictureAsPdfOutlinedIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            )}
            {doc.hasSourceFile && (
              <Tooltip title={t("library.sourceFileTooltip")}>
                <DescriptionOutlinedIcon fontSize="small" color="primary" sx={{ my: "auto" }} />
              </Tooltip>
            )}
            {!doc.hasControlledPdf && !doc.hasSourceFile && (
              <Typography variant="caption" sx={{ color: "text.disabled" }}>
                —
              </Typography>
            )}
          </Box>
        )
      },
      {
        key: "effectiveDate",
        label: t("library.columns.effectiveDate"),
        sortable: true,
        render: (doc) => (
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {doc.effectiveDate ? formatLabCalendarDate(doc.effectiveDate) : t("library.pending")}
          </Typography>
        )
      }
    ],
    [navigate, handleOpenPdf, t]
  );

  const hasFilters = Boolean(
    searchTerm || selectedType !== "" || selectedDept !== "" || selectedStatus !== "" || includeCancelledVoided || showOverdueOnly
  );

  return (
    <Box sx={{ pb: 4 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
        <PageHeader
          title={t("library.title")}
          subtitle={t("library.subtitle")}
        />
        {/* Mirrors CanRegisterDocumentMasterAsync. The server check is the
            control; hiding the button just avoids offering an action that will
            be refused. */}
        {canRegister && (
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={() => setRegisterOpen(true)}
            sx={{ fontWeight: 600 }}
          >
            {t("library.registerNew")}
          </Button>
        )}
      </Box>

      {filtersFailed && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {t("library.filtersFailed")}
        </Alert>
      )}
      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* Filter and Search Bar */}
      <Paper sx={{ p: 2, mb: 3 }}>
        <Box sx={{ display: "flex", flexWrap: "wrap", gap: 2, alignItems: "center" }}>
          <TextField
            size="small"
            placeholder={t("library.filters.searchPlaceholder")}
            value={searchTerm}
            onChange={(e) => setSearch(e.target.value)}
            sx={{ minWidth: 280, flexGrow: 1 }}
            slotProps={{
              htmlInput: { "aria-label": t("library.filters.searchAria") },
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon fontSize="small" color="action" />
                  </InputAdornment>
                )
              }
            }}
          />

          <TextField
            select
            size="small"
            label={t("library.filters.documentType")}
            value={types.some((docType) => String(docType.id) === selectedType) ? selectedType : ""}
            onChange={(e) => setFilter("documentTypeId", e.target.value || null)}
            sx={{ minWidth: 160 }}
          >
            <MenuItem value="">{t("library.filters.allTypes")}</MenuItem>
            {types.map((docType) => (
              <MenuItem key={docType.id} value={String(docType.id)}>
                {docType.code}: {docType.name}
              </MenuItem>
            ))}
          </TextField>

          <TextField
            select
            size="small"
            label={t("library.filters.department")}
            value={departments.some((d) => String(d.id) === selectedDept) ? selectedDept : ""}
            onChange={(e) => setFilter("departmentId", e.target.value || null)}
            sx={{ minWidth: 160 }}
          >
            <MenuItem value="">{t("library.filters.allDepartments")}</MenuItem>
            {departments.map((d) => (
              <MenuItem key={d.id} value={String(d.id)}>
                {d.code}: {d.name}
              </MenuItem>
            ))}
          </TextField>

          <TextField
            select
            size="small"
            label={t("library.filters.status")}
            value={parsedStatus ?? ""}
            onChange={(e) => setFilter("revisionStatus", e.target.value || null)}
            sx={{ minWidth: 150 }}
          >
            <MenuItem value="">{t("library.filters.allStatuses")}</MenuItem>
            <MenuItem value="Draft">{t("library.statuses.Draft")}</MenuItem>
            <MenuItem value="InReview">{t("library.statuses.InReview")}</MenuItem>
            <MenuItem value="AwaitingApproval">{t("library.statuses.AwaitingApproval")}</MenuItem>
            <MenuItem value="FutureEffective">{t("library.statuses.FutureEffective")}</MenuItem>
            <MenuItem value="Effective">{t("library.statuses.Effective")}</MenuItem>
            <MenuItem value="Superseded">{t("library.statuses.Superseded")}</MenuItem>
            <MenuItem value="Obsolete">{t("library.statuses.Obsolete")}</MenuItem>
            <MenuItem value="Cancelled">{t("library.statuses.Cancelled")}</MenuItem>
          </TextField>

          <Box role="group" aria-label={t("library.filters.showGroupAria")} sx={{ display: "flex", gap: 1 }}>
            <ToggleButton
              value="voided"
              size="small"
              selected={includeCancelledVoided}
              onChange={() => setFilter("showVoided", includeCancelledVoided ? null : "true")}
              sx={{ height: 40, px: 1.5, textTransform: "none", fontSize: 13, whiteSpace: "nowrap" }}
            >
              {t("library.filters.showVoided")}
            </ToggleButton>
            <ToggleButton
              value="overdue"
              size="small"
              color="error"
              selected={showOverdueOnly}
              onChange={() => setFilter("reviewOverdue", showOverdueOnly ? null : "true")}
              sx={{ height: 40, px: 1.5, textTransform: "none", fontSize: 13, whiteSpace: "nowrap" }}
            >
              {t("library.filters.reviewOverdueOnly")}
            </ToggleButton>
          </Box>

          <Button
            size="small"
            color="inherit"
            startIcon={<ClearIcon />}
            onClick={handleClearFilters}
            disabled={!hasFilters}
          >
            {t("library.filters.reset")}
          </Button>
        </Box>
      </Paper>

      {/* Library Table */}
      <RegisterTable
        columns={columns}
        rows={documents}
        getRowId={(doc) => doc.id}
        tableKey={TABLE_KEY}
        tableState={tableState}
        toolbar={<TableToolbar tableKey={TABLE_KEY} tableState={tableState} showSearch={false} />}
        loading={loading}
        serverPaged={{
          totalCount,
          sortableColumns: DOCUMENT_LIBRARY_SORTABLE_COLUMNS,
          onSortChange: tableState.setSort,
          sort: activeSort
        }}
        rowSx={(doc) => (doc.recordStatus === "Void" ? { bgcolor: "action.hover", opacity: 0.75 } : undefined)}
        trailingCell={{
          label: t("common.actions"),
          render: (doc) => (
            <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 0.5 }}>
              <Tooltip title={t("library.viewDetailsTooltip")}>
                <IconButton
                  aria-label={t("library.openDetailsAria", { code: doc.companyDocumentCode })}
                  size="small"
                  onClick={() => navigate(`/document-control/documents/${doc.id}`)}
                >
                  <VisibilityOutlinedIcon fontSize="small" />
                </IconButton>
              </Tooltip>

              {/* Void Button - Only Document Controller can void and only if never effective */}
              {isController && doc.recordStatus === "Active" && !doc.currentEffectiveRevisionId && (
                <Tooltip title={t("library.voidTooltip")}>
                  <IconButton
                    aria-label={t("library.voidAria", { code: doc.companyDocumentCode })}
                    size="small"
                    color="error"
                    onClick={() =>
                      setVoidDialogState({
                        open: true,
                        masterId: doc.id,
                        code: doc.companyDocumentCode,
                        title: doc.title
                      })
                    }
                  >
                    <BlockOutlinedIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              )}
            </Box>
          )
        }}
        empty={{
          title: t("library.emptyTitle"),
          description: t("library.emptyDescription")
        }}
      />

      {/* Controlled PDF Viewer Modal */}
      <ControlledPdfViewer
        open={pdfViewerState.open}
        onClose={() => setPdfViewerState((prev) => ({ ...prev, open: false }))}
        fileId={pdfViewerState.fileId}
        fileName={pdfViewerState.fileName}
        companyDocumentCode={pdfViewerState.companyCode}
        microLimsDocumentId={pdfViewerState.microLimsId}
        revisionNumber={pdfViewerState.revision}
        title={pdfViewerState.title}
        revisionStatus={pdfViewerState.status}
      />

      {/* Register New Document Modal */}
      <RegisterDocumentDialog
        open={registerOpen}
        onClose={() => setRegisterOpen(false)}
        onSuccess={(created) => {
          fetchLibrary();
          navigate(`/document-control/documents/${created.id}`);
        }}
      />

      {/* Void Dialog */}
      <VoidDocumentDialog
        open={voidDialogState.open}
        onClose={() => setVoidDialogState((prev) => ({ ...prev, open: false }))}
        onSuccess={() => fetchLibrary()}
        documentMasterId={voidDialogState.masterId}
        companyDocumentCode={voidDialogState.code}
        documentTitle={voidDialogState.title}
      />
    </Box>
  );
}
