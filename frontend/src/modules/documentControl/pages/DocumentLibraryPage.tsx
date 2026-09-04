import { useState, useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import {
  Box,
  Typography,
  Button,
  TextField,
  MenuItem,
  InputAdornment,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  TablePagination,
  IconButton,
  Tooltip,
  Chip,
  FormControlLabel,
  Switch,
  Alert,
  CircularProgress
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import AddIcon from "@mui/icons-material/Add";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import PictureAsPdfOutlinedIcon from "@mui/icons-material/PictureAsPdfOutlined";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import FilterListOutlinedIcon from "@mui/icons-material/FilterListOutlined";
import ClearIcon from "@mui/icons-material/Clear";

import { PageHeader } from "../../../components/PageHeader";
import { StatusBadge } from "../../../components/StatusBadge";
import { useAuth } from "../../../contexts/AuthContext";
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

export function DocumentLibraryPage() {
  const navigate = useNavigate();
  const { role } = useAuth();
  const isController = role === "SectionHead";

  // Data state
  const [documents, setDocuments] = useState<DocumentMasterSummaryDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Filter state
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedType, setSelectedType] = useState<number | "">("");
  const [selectedDept, setSelectedDept] = useState<number | "">("");
  const [selectedStatus, setSelectedStatus] = useState<DocumentRevisionStatus | "">("");
  const [includeCancelledVoided, setIncludeCancelledVoided] = useState(false);
  const [showOverdueOnly, setShowOverdueOnly] = useState(false);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(10);

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

  // Load types and departments
  useEffect(() => {
    Promise.all([
      documentControlService.getTypes(),
      documentControlService.getDepartments()
    ]).then(([t, d]) => {
      setTypes(t);
      setDepartments(d);
    }).catch(() => {});
  }, []);

  const fetchLibrary = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const res = await documentControlService.getLibrary({
        searchTerm: searchTerm.trim() || undefined,
        documentTypeId: selectedType !== "" ? selectedType : undefined,
        departmentId: selectedDept !== "" ? selectedDept : undefined,
        revisionStatus: selectedStatus !== "" ? selectedStatus : undefined,
        includeCancelledAndVoided: includeCancelledVoided,
        isOverdue: showOverdueOnly ? true : undefined,
        page: page + 1,
        pageSize
      });
      setDocuments(res.items);
      setTotalCount(res.totalCount);
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to load document library.");
    } finally {
      setLoading(false);
    }
  }, [searchTerm, selectedType, selectedDept, selectedStatus, includeCancelledVoided, showOverdueOnly, page, pageSize]);

  useEffect(() => {
    fetchLibrary();
  }, [fetchLibrary]);

  const handleClearFilters = () => {
    setSearchTerm("");
    setSelectedType("");
    setSelectedDept("");
    setSelectedStatus("");
    setIncludeCancelledVoided(false);
    setShowOverdueOnly(false);
    setPage(0);
  };

  const handleOpenPdf = async (doc: DocumentMasterSummaryDto) => {
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
        alert("No active controlled PDF file is currently attached to this document revision.");
      }
    } catch (err: any) {
      alert("Failed to load document file details: " + (err.response?.data?.message || err.message));
    }
  };

  return (
    <Box sx={{ p: 3, maxWidth: 1600, mx: "auto" }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
        <PageHeader
          title="Document Library"
          subtitle="Authoritative repository of controlled documents, Standard Operating Procedures, and quality records"
        />
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={() => setRegisterOpen(true)}
          sx={{ fontWeight: 600 }}
        >
          Register New Document
        </Button>
      </Box>

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
            placeholder="Search code, title, or keywords..."
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value);
              setPage(0);
            }}
            sx={{ minWidth: 280, flexGrow: 1 }}
            InputProps={{
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" color="action" />
                </InputAdornment>
              )
            }}
          />

          <TextField
            select
            size="small"
            label="Document Type"
            value={selectedType}
            onChange={(e) => {
              setSelectedType(e.target.value as number | "");
              setPage(0);
            }}
            sx={{ minWidth: 160 }}
          >
            <MenuItem value="">All Types</MenuItem>
            {types.map((t) => (
              <MenuItem key={t.id} value={t.id}>
                {t.code} — {t.name}
              </MenuItem>
            ))}
          </TextField>

          <TextField
            select
            size="small"
            label="Department"
            value={selectedDept}
            onChange={(e) => {
              setSelectedDept(e.target.value as number | "");
              setPage(0);
            }}
            sx={{ minWidth: 160 }}
          >
            <MenuItem value="">All Departments</MenuItem>
            {departments.map((d) => (
              <MenuItem key={d.id} value={d.id}>
                {d.code} — {d.name}
              </MenuItem>
            ))}
          </TextField>

          <TextField
            select
            size="small"
            label="Status"
            value={selectedStatus}
            onChange={(e) => {
              setSelectedStatus(e.target.value as DocumentRevisionStatus | "");
              setPage(0);
            }}
            sx={{ minWidth: 150 }}
          >
            <MenuItem value="">All Statuses</MenuItem>
            <MenuItem value="Draft">Draft</MenuItem>
            <MenuItem value="Effective">Effective</MenuItem>
            <MenuItem value="InReview">In Review</MenuItem>
            <MenuItem value="Approved">Approved</MenuItem>
            <MenuItem value="Superseded">Superseded</MenuItem>
            <MenuItem value="Cancelled">Cancelled</MenuItem>
          </TextField>

          <FormControlLabel
            control={
              <Switch
                size="small"
                checked={includeCancelledVoided}
                onChange={(e) => {
                  setIncludeCancelledVoided(e.target.checked);
                  setPage(0);
                }}
              />
            }
            label={<Typography variant="caption">Show Voided</Typography>}
          />

          <FormControlLabel
            control={
              <Switch
                size="small"
                color="error"
                checked={showOverdueOnly}
                onChange={(e) => {
                  setShowOverdueOnly(e.target.checked);
                  setPage(0);
                }}
              />
            }
            label={<Typography variant="caption" sx={{ color: showOverdueOnly ? "error.main" : "inherit", fontWeight: showOverdueOnly ? 700 : 400 }}>Review Overdue Only</Typography>}
          />

          <Button
            size="small"
            color="inherit"
            startIcon={<ClearIcon />}
            onClick={handleClearFilters}
            disabled={!searchTerm && selectedType === "" && selectedDept === "" && selectedStatus === "" && !includeCancelledVoided && !showOverdueOnly}
          >
            Reset
          </Button>
        </Box>
      </Paper>

      {/* Library Table */}
      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead sx={{ bgcolor: "grey.50" }}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700 }}>Company Code</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>MicroLIMS ID</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Title</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Type</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Dept / Section</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Owner</TableCell>
              <TableCell sx={{ fontWeight: 700, textAlign: "center" }}>Rev</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Status</TableCell>
              <TableCell sx={{ fontWeight: 700, textAlign: "center" }}>Files</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Effective Date</TableCell>
              <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={11} align="center" sx={{ py: 6 }}>
                  <CircularProgress size={32} />
                  <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                    Loading Document Library...
                  </Typography>
                </TableCell>
              </TableRow>
            ) : documents.length === 0 ? (
              <TableRow>
                <TableCell colSpan={11} align="center" sx={{ py: 6 }}>
                  <FilterListOutlinedIcon sx={{ fontSize: 40, color: "text.disabled", mb: 1 }} />
                  <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                    No controlled documents match the selected filters
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    Try adjusting your search criteria or register a new document master.
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              documents.map((doc) => (
                <TableRow
                  key={doc.id}
                  hover
                  sx={{
                    bgcolor: doc.recordStatus === "Void" ? "action.hover" : "inherit",
                    opacity: doc.recordStatus === "Void" ? 0.75 : 1
                  }}
                >
                  <TableCell>
                    <Typography
                      variant="body2"
                      sx={{
                        fontWeight: 700,
                        color: "primary.main",
                        cursor: "pointer",
                        "&:hover": { textDecoration: "underline" }
                      }}
                      onClick={() => navigate(`/document-control/documents/${doc.id}`)}
                    >
                      {doc.companyDocumentCode}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="caption" sx={{ fontFamily: "monospace" }}>
                      {doc.microLimsDocumentId}
                    </Typography>
                  </TableCell>
                  <TableCell sx={{ maxWidth: 300 }}>
                    <Typography
                      variant="body2"
                      sx={{
                        fontWeight: 500,
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap"
                      }}
                      title={doc.title}
                    >
                      {doc.title}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Chip label={doc.documentTypeCode} size="small" variant="outlined" sx={{ height: 20, fontSize: 11 }} />
                  </TableCell>
                  <TableCell>
                    <Typography variant="caption" display="block" sx={{ fontWeight: 600 }}>
                      {doc.departmentName}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {doc.sectionName}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">{doc.documentOwnerUserName}</Typography>
                  </TableCell>
                  <TableCell align="center">
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>
                      {doc.currentRevisionNumber || "01"}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    {doc.recordStatus === "Void" ? (
                      <StatusBadge status="Void" label="VOID" />
                    ) : (
                      <Box sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}>
                        <StatusBadge status={doc.currentEffectiveRevisionId ? "Effective" : "Draft"} />
                        {doc.currentEffectiveRevisionId && doc.nextReviewDate && new Date(doc.nextReviewDate) < new Date() && (
                          <Chip label="REVIEW OVERDUE" color="error" size="small" sx={{ height: 16, fontSize: "0.55rem", fontWeight: 800, width: "fit-content" }} />
                        )}
                      </Box>
                    )}
                  </TableCell>
                  <TableCell align="center">
                    <Box sx={{ display: "flex", justifyContent: "center", gap: 0.5 }}>
                      {doc.hasControlledPdf && (
                        <Tooltip title="View Controlled PDF">
                          <IconButton
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
                        <Tooltip title="Source File Attached (.docx)">
                          <DescriptionOutlinedIcon fontSize="small" color="primary" sx={{ my: "auto" }} />
                        </Tooltip>
                      )}
                      {!doc.hasControlledPdf && !doc.hasSourceFile && (
                        <Typography variant="caption" color="text.disabled">—</Typography>
                      )}
                    </Box>
                  </TableCell>
                  <TableCell>
                    <Typography variant="caption" color="text.secondary">
                      {doc.effectiveDate ? new Date(doc.effectiveDate).toLocaleDateString() : "Pending"}
                    </Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 0.5 }}>
                      <Tooltip title="View Document Details">
                        <IconButton
                          size="small"
                          onClick={() => navigate(`/document-control/documents/${doc.id}`)}
                        >
                          <VisibilityOutlinedIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>

                      {/* Void Button - Only Document Controller can void and only if never effective */}
                      {isController && doc.recordStatus === "Active" && !doc.currentEffectiveRevisionId && (
                        <Tooltip title="Void Document Master">
                          <IconButton
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
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>

        <TablePagination
          component="div"
          count={totalCount}
          page={page}
          onPageChange={(_, newPage) => setPage(newPage)}
          rowsPerPage={pageSize}
          onRowsPerPageChange={(e) => {
            setPageSize(parseInt(e.target.value, 10));
            setPage(0);
          }}
          rowsPerPageOptions={[5, 10, 25, 50]}
        />
      </TableContainer>

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
