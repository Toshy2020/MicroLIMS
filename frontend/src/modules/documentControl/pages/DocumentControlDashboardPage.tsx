import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import {
  Box,
  Typography,
  Paper,
  Button,
  Card,
  CardContent,
  CardActionArea,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  Alert,
  CircularProgress,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import LibraryBooksOutlinedIcon from "@mui/icons-material/LibraryBooksOutlined";
import CheckCircleOutlineOutlinedIcon from "@mui/icons-material/CheckCircleOutlineOutlined";
import EditNoteOutlinedIcon from "@mui/icons-material/EditNoteOutlined";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import PendingActionsOutlinedIcon from "@mui/icons-material/PendingActionsOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";

import { PageHeader } from "../../../components/PageHeader";
import { tableHeadSx } from "../../../theme";
import { StatusBadge } from "../../../components/StatusBadge";
import { documentControlService } from "../services/documentControlService";
import { RegisterDocumentDialog } from "../components/RegisterDocumentDialog";
import MenuBookOutlinedIcon from "@mui/icons-material/MenuBookOutlined";
import type {
  DocumentMasterSummaryDto,
  DocumentAuditItemDto
} from "../types/documentControlTypes";

export function DocumentControlDashboardPage() {
  const theme = useTheme();
  const navigate = useNavigate();

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Stats
  const [totalMasters, setTotalMasters] = useState(0);
  const [effectiveCount, setEffectiveCount] = useState(0);
  const [draftCount, setDraftCount] = useState(0);
  const [voidCount, setVoidCount] = useState(0);
  const [filesPendingCount, setFilesPendingCount] = useState(0);
  const [overdueCount, setOverdueCount] = useState(0);

  // Recent data
  const [recentDocs, setRecentDocs] = useState<DocumentMasterSummaryDto[]>([]);
  const [recentAudit, setRecentAudit] = useState<DocumentAuditItemDto[]>([]);

  const [registerOpen, setRegisterOpen] = useState(false);

  useEffect(() => {
    setLoading(true);
    setError(null);

    Promise.all([
      documentControlService.getLibrary({ pageSize: 100, includeCancelledAndVoided: true }),
      documentControlService.searchAudit({ pageSize: 5 })
    ])
      .then(([libRes, auditRes]) => {
        const items = libRes.items;
        const now = new Date();
        setRecentDocs(items.slice(0, 5));
        setRecentAudit(auditRes.items);

        setTotalMasters(libRes.totalCount);
        setEffectiveCount(items.filter((d) => d.currentEffectiveRevisionId != null && d.recordStatus === "Active").length);
        setDraftCount(items.filter((d) => d.currentEffectiveRevisionId == null && d.recordStatus === "Active").length);
        setVoidCount(items.filter((d) => d.recordStatus === "Void").length);
        setFilesPendingCount(items.filter((d) => !d.hasControlledPdf && d.recordStatus === "Active").length);
        setOverdueCount(items.filter((d) => d.currentEffectiveRevisionId != null && d.recordStatus === "Active" && d.nextReviewDate != null && new Date(d.nextReviewDate) < now).length);
      })
      .catch((err) => {
        setError(err.response?.data?.message || err.message || "Failed to load dashboard metrics.");
      })
      .finally(() => {
        setLoading(false);
      });
  }, []);

  return (
    <Box sx={{ pb: 4 }}>
      {/* Header */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 3 }}>
        <PageHeader
          title="Document Control Dashboard"
          subtitle="Document governance overview, lifecycle metrics, and quality records status"
        />
        <Box sx={{ display: "flex", gap: 1.5 }}>
          <Button
            variant="outlined"
            color="primary"
            startIcon={<MenuBookOutlinedIcon />}
            onClick={() => navigate("/document-control/my-reading-list")}
          >
            My Reading List
          </Button>
          <Button
            variant="outlined"
            startIcon={<LibraryBooksOutlinedIcon />}
            onClick={() => navigate("/document-control/library")}
          >
            Document Library
          </Button>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={() => setRegisterOpen(true)}
          >
            Register Document
          </Button>
        </Box>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 3 }}>{error}</Alert>}

      {/* KPI Cards */}
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr", md: "repeat(3, 1fr)", lg: "repeat(6, 1fr)" }, gap: 2, mb: 4 }}>
        {/* Total Documents */}
        <Card sx={{ bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
          <CardActionArea onClick={() => navigate("/document-control/library")}>
            <CardContent sx={{ p: 2 }}>
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, textTransform: "uppercase" }}>
                  Total Documents
                </Typography>
                <LibraryBooksOutlinedIcon color="primary" fontSize="small" />
              </Box>
              <Typography variant="h4" sx={{ fontWeight: 800, color: "text.primary" }}>
                {loading ? <CircularProgress size={24} /> : totalMasters}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                In system catalog
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>

        {/* Effective Documents */}
        <Card sx={{ bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
          <CardActionArea onClick={() => navigate("/document-control/library")}>
            <CardContent sx={{ p: 2 }}>
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, textTransform: "uppercase" }}>
                  Effective Records
                </Typography>
                <CheckCircleOutlineOutlinedIcon color="success" fontSize="small" />
              </Box>
              <Typography variant="h4" sx={{ fontWeight: 800, color: "success.main" }}>
                {loading ? <CircularProgress size={24} /> : effectiveCount}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Active & released
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>

        {/* Overdue Reviews */}
        <Card sx={{ bgcolor: overdueCount > 0 ? "error.50" : "background.paper", border: "1px solid", borderColor: overdueCount > 0 ? "error.main" : "divider" }}>
          <CardActionArea onClick={() => navigate("/document-control/library")}>
            <CardContent sx={{ p: 2 }}>
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                <Typography variant="caption" color={overdueCount > 0 ? "error.main" : "text.secondary"} sx={{ fontWeight: 700, textTransform: "uppercase" }}>
                  Review Overdue
                </Typography>
                <WarningAmberOutlinedIcon color="error" fontSize="small" />
              </Box>
              <Typography variant="h4" sx={{ fontWeight: 800, color: "error.main" }}>
                {loading ? <CircularProgress size={24} /> : overdueCount}
              </Typography>
              <Typography variant="caption" color={overdueCount > 0 ? "error.main" : "text.secondary"}>
                {overdueCount > 0 ? "Action Required" : "All current"}
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>

        {/* Draft Revisions */}
        <Card sx={{ bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
          <CardActionArea onClick={() => navigate("/document-control/library")}>
            <CardContent sx={{ p: 2 }}>
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, textTransform: "uppercase" }}>
                  Draft Revisions
                </Typography>
                <EditNoteOutlinedIcon color="info" fontSize="small" />
              </Box>
              <Typography variant="h4" sx={{ fontWeight: 800, color: "info.main" }}>
                {loading ? <CircularProgress size={24} /> : draftCount}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Pending publication
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>

        {/* File Upload Pending */}
        <Card sx={{ bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
          <CardActionArea onClick={() => navigate("/document-control/library")}>
            <CardContent sx={{ p: 2 }}>
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, textTransform: "uppercase" }}>
                  File Pending
                </Typography>
                <PendingActionsOutlinedIcon color="warning" fontSize="small" />
              </Box>
              <Typography variant="h4" sx={{ fontWeight: 800, color: "warning.main" }}>
                {loading ? <CircularProgress size={24} /> : filesPendingCount}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                No PDF attached
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>

        {/* Void Records */}
        <Card sx={{ bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
          <CardActionArea onClick={() => navigate("/document-control/library")}>
            <CardContent sx={{ p: 2 }}>
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, textTransform: "uppercase" }}>
                  Voided Masters
                </Typography>
                <BlockOutlinedIcon color="error" fontSize="small" />
              </Box>
              <Typography variant="h4" sx={{ fontWeight: 800, color: "error.main" }}>
                {loading ? <CircularProgress size={24} /> : voidCount}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Retired records
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>
      </Box>

      {/* Main Content: Recent Documents & Recent Audit Activity */}
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", lg: "7fr 5fr" }, gap: 3 }}>
        {/* Recent Documents Table */}
        <Paper sx={{ p: 2.5 }}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              Recently Registered Documents
            </Typography>
            <Button
              size="small"
              endIcon={<ArrowForwardIcon />}
              onClick={() => navigate("/document-control/library")}
            >
              View Full Library
            </Button>
          </Box>

          <TableContainer>
            <Table size="small">
              <TableHead sx={tableHeadSx(theme)}>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700 }}>Company Code</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Title</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Type</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Status</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Files</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Date</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {recentDocs.map((doc) => (
                  <TableRow
                    key={doc.id}
                    hover
                    sx={{ cursor: "pointer" }}
                    onClick={() => navigate(`/document-control/documents/${doc.id}`)}
                  >
                    <TableCell sx={{ fontWeight: 700, color: "primary.main" }}>
                      {doc.companyDocumentCode}
                    </TableCell>
                    <TableCell sx={{ maxWidth: 220, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                      {doc.title}
                    </TableCell>
                    <TableCell>
                      <Chip label={doc.documentTypeCode} size="small" variant="outlined" sx={{ height: 20, fontSize: 10 }} />
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={doc.recordStatus === "Void" ? "Void" : (doc.currentEffectiveRevisionId ? "Effective" : "Draft")} />
                    </TableCell>
                    <TableCell>
                      {doc.hasControlledPdf ? (
                        <Chip label="PDF" size="small" color="primary" sx={{ height: 18, fontSize: 9 }} />
                      ) : (
                        <Typography variant="caption" color="text.secondary">None</Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      <Typography variant="caption" color="text.secondary">
                        {new Date(doc.createdAt).toLocaleDateString()}
                      </Typography>
                    </TableCell>
                  </TableRow>
                ))}
                {recentDocs.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={6} align="center" sx={{ py: 3, color: "text.secondary" }}>
                      No documents registered yet.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>

        {/* Recent Audit Activity */}
        <Paper sx={{ p: 2.5 }}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
              <HistoryOutlinedIcon color="primary" fontSize="small" />
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                Recent Audit Trail Events
              </Typography>
            </Box>
            <Button
              size="small"
              endIcon={<ArrowForwardIcon />}
              onClick={() => navigate("/document-control/audit")}
            >
              All Events
            </Button>
          </Box>

          <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
            {recentAudit.map((log) => (
              <Box
                key={log.id}
                sx={{
                  p: 1.5,
                  borderRadius: 1,
                  bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50",
                  border: "1px solid",
                  borderColor: "divider",
                  display: "flex",
                  flexDirection: "column",
                  gap: 0.5
                }}
              >
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <Typography variant="body2" sx={{ fontWeight: 600, color: "text.primary" }}>
                    {log.actionCode || log.action}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {new Date(log.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                  </Typography>
                </Box>
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <Typography variant="caption" color="text.secondary">
                    By: <strong>{log.userName || "System"}</strong>
                  </Typography>
                  <Chip label={log.actionCategory} size="small" variant="outlined" sx={{ height: 18, fontSize: 9 }} />
                </Box>
                {log.reason && (
                  <Typography variant="caption" sx={{ fontStyle: "italic", color: "text.secondary" }}>
                    "{log.reason}"
                  </Typography>
                )}
              </Box>
            ))}
            {recentAudit.length === 0 && (
              <Typography variant="caption" color="text.secondary" sx={{ py: 3, textAlign: "center" }}>
                No recent audit events.
              </Typography>
            )}
          </Box>
        </Paper>
      </Box>

      {/* Registration Modal */}
      <RegisterDocumentDialog
        open={registerOpen}
        onClose={() => setRegisterOpen(false)}
        onSuccess={(created) => navigate(`/document-control/documents/${created.id}`)}
      />
    </Box>
  );
}


