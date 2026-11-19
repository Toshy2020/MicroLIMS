import { compactChipSx } from "../documentControlStyles";
import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";
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
import { documentMasterStatusLabel } from "../documentStatusDisplay";
import { documentControlService } from "../services/documentControlService";
import { RegisterDocumentDialog } from "../components/RegisterDocumentDialog";
import { ReassignmentsButton } from "../components/ReassignmentsButton";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import MenuBookOutlinedIcon from "@mui/icons-material/MenuBookOutlined";
import type {
  DocumentMasterSummaryDto,
  DocumentAuditItemDto
} from "../types/documentControlTypes";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

export function DocumentControlDashboardPage() {
  const { t } = useTranslation("documentControl");
  const theme = useTheme();
  const navigate = useNavigate();
  const { permissions } = useAuth();
  // Mirrors CanRegisterDocumentMasterAsync; the server check is the control.
  // Registration is permission-based on the server (CanRegisterDocumentMasterAsync).
  const canRegister = permissions.includes(PERMISSIONS.DOCUMENTS_REGISTER);

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
        // FS-1a-173: cancelled and voided records are excluded by default from
        // the library, search results, counts and active work lists. This query
        // has to pass includeCancelledAndVoided so the Void KPI below can be
        // counted, so the work list must filter them back out itself.
        // Only Void is filterable here - Cancelled is a revision status and
        // DocumentMasterSummaryDto carries no revision status (see
        // documentStatusDisplay.ts), so a master whose newest revision is
        // Cancelled cannot be excluded client-side yet.
        setRecentDocs(items.filter((d) => d.recordStatus === "Active").slice(0, 5));
        setRecentAudit(auditRes.items);

        setTotalMasters(libRes.totalCount);
        setEffectiveCount(items.filter((d) => d.currentEffectiveRevisionId != null && d.recordStatus === "Active").length);
        // Counts actual Drafts. This used to count every active master with no
        // effective revision, which lumped InReview, AwaitingApproval,
        // FutureEffective and Cancelled in with Draft and overstated the tile.
        setDraftCount(items.filter((d) => d.currentRevisionStatus === "Draft" && d.recordStatus === "Active").length);
        setVoidCount(items.filter((d) => d.recordStatus === "Void").length);
        setFilesPendingCount(items.filter((d) => !d.hasControlledPdf && d.recordStatus === "Active").length);
        setOverdueCount(items.filter((d) => d.currentEffectiveRevisionId != null && d.recordStatus === "Active" && d.nextReviewDate != null && new Date(d.nextReviewDate) < now).length);
      })
      .catch((err: unknown) => {
        setError(serverMessage(err) ?? getErrorMessage(err, t("dashboard.loadError")));
      })
      .finally(() => {
        setLoading(false);
      });
  }, [t]);

  return (
    <Box sx={{ pb: 4 }}>
      {/* Header */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 3 }}>
        <PageHeader
          title={t("dashboard.title")}
          subtitle={t("dashboard.subtitle")}
        />
        <Box sx={{ display: "flex", gap: 1.5 }}>
          <ReassignmentsButton />
          <Button
            variant="outlined"
            color="primary"
            startIcon={<MenuBookOutlinedIcon />}
            onClick={() => navigate("/document-control/my-reading-list")}
          >
            {t("dashboard.myReadingList")}
          </Button>
          <Button
            variant="outlined"
            startIcon={<LibraryBooksOutlinedIcon />}
            onClick={() => navigate("/document-control/library")}
          >
            {t("dashboard.documentLibrary")}
          </Button>
          {canRegister && (
            <Button
              variant="contained"
              startIcon={<AddIcon />}
              onClick={() => setRegisterOpen(true)}
            >
              {t("dashboard.registerDocument")}
            </Button>
          )}
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
                <Typography
                  variant="caption"
                  sx={{
                    color: "text.secondary",
                    fontWeight: 700,
                    textTransform: "uppercase"
                  }}>
                  {t("dashboard.kpis.totalDocuments")}
                </Typography>
                <LibraryBooksOutlinedIcon color="primary" fontSize="small" />
              </Box>
              <Typography variant="h4" sx={{ fontWeight: 800, color: "text.primary" }}>
                {loading ? <CircularProgress size={24} /> : totalMasters}
              </Typography>
              <Typography variant="caption" sx={{
                color: "text.secondary"
              }}>
                {t("dashboard.kpis.inSystemCatalog")}
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>

        {/* Effective Documents */}
        <Card sx={{ bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
          <CardActionArea onClick={() => navigate("/document-control/library")}>
            <CardContent sx={{ p: 2 }}>
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                <Typography
                  variant="caption"
                  sx={{
                    color: "text.secondary",
                    fontWeight: 700,
                    textTransform: "uppercase"
                  }}>
                  {t("dashboard.kpis.effectiveRecords")}
                </Typography>
                <CheckCircleOutlineOutlinedIcon color="success" fontSize="small" />
              </Box>
              <Typography variant="h4" sx={{ fontWeight: 800, color: "success.main" }}>
                {loading ? <CircularProgress size={24} /> : effectiveCount}
              </Typography>
              <Typography variant="caption" sx={{
                color: "text.secondary"
              }}>
                {t("dashboard.kpis.activeAndReleased")}
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
                  {t("dashboard.kpis.reviewOverdue")}
                </Typography>
                <WarningAmberOutlinedIcon color="error" fontSize="small" />
              </Box>
              <Typography variant="h4" sx={{ fontWeight: 800, color: "error.main" }}>
                {loading ? <CircularProgress size={24} /> : overdueCount}
              </Typography>
              <Typography variant="caption" color={overdueCount > 0 ? "error.main" : "text.secondary"}>
                {overdueCount > 0 ? t("dashboard.kpis.actionRequired") : t("dashboard.kpis.allCurrent")}
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>

        {/* Draft Revisions */}
        <Card sx={{ bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
          <CardActionArea onClick={() => navigate("/document-control/library")}>
            <CardContent sx={{ p: 2 }}>
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                <Typography
                  variant="caption"
                  sx={{
                    color: "text.secondary",
                    fontWeight: 700,
                    textTransform: "uppercase"
                  }}>
                  {t("dashboard.kpis.draftRevisions")}
                </Typography>
                <EditNoteOutlinedIcon color="info" fontSize="small" />
              </Box>
              <Typography variant="h4" sx={{ fontWeight: 800, color: "info.main" }}>
                {loading ? <CircularProgress size={24} /> : draftCount}
              </Typography>
              <Typography variant="caption" sx={{
                color: "text.secondary"
              }}>
                {t("dashboard.kpis.pendingPublication")}
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>

        {/* File Upload Pending */}
        <Card sx={{ bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
          <CardActionArea onClick={() => navigate("/document-control/library")}>
            <CardContent sx={{ p: 2 }}>
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                <Typography
                  variant="caption"
                  sx={{
                    color: "text.secondary",
                    fontWeight: 700,
                    textTransform: "uppercase"
                  }}>
                  {t("dashboard.kpis.filePending")}
                </Typography>
                <PendingActionsOutlinedIcon color="warning" fontSize="small" />
              </Box>
              <Typography variant="h4" sx={{ fontWeight: 800, color: "warning.main" }}>
                {loading ? <CircularProgress size={24} /> : filesPendingCount}
              </Typography>
              <Typography variant="caption" sx={{
                color: "text.secondary"
              }}>
                {t("dashboard.kpis.noPdfAttached")}
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>

        {/* Void Records */}
        <Card sx={{ bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
          <CardActionArea onClick={() => navigate("/document-control/library")}>
            <CardContent sx={{ p: 2 }}>
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                <Typography
                  variant="caption"
                  sx={{
                    color: "text.secondary",
                    fontWeight: 700,
                    textTransform: "uppercase"
                  }}>
                  {t("dashboard.kpis.voidedMasters")}
                </Typography>
                <BlockOutlinedIcon color="error" fontSize="small" />
              </Box>
              <Typography variant="h4" sx={{ fontWeight: 800, color: "error.main" }}>
                {loading ? <CircularProgress size={24} /> : voidCount}
              </Typography>
              <Typography variant="caption" sx={{
                color: "text.secondary"
              }}>
                {t("dashboard.kpis.retiredRecords")}
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
              {t("dashboard.recentDocs.title")}
            </Typography>
            <Button
              size="small"
              endIcon={<ArrowForwardIcon />}
              onClick={() => navigate("/document-control/library")}
            >
              {t("dashboard.recentDocs.viewFullLibrary")}
            </Button>
          </Box>

          <TableContainer sx={{ maxHeight: { md: "calc(100vh - 240px)" } }}>
            <Table stickyHeader size="small">
              <TableHead sx={tableHeadSx(theme)}>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700 }}>{t("dashboard.recentDocs.columns.companyCode")}</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>{t("dashboard.recentDocs.columns.title")}</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>{t("dashboard.recentDocs.columns.type")}</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>{t("dashboard.recentDocs.columns.status")}</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>{t("dashboard.recentDocs.columns.files")}</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>{t("dashboard.recentDocs.columns.date")}</TableCell>
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
                      <Chip label={doc.documentTypeCode} size="small" variant="outlined" sx={compactChipSx} />
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={documentMasterStatusLabel(doc)} />
                    </TableCell>
                    <TableCell>
                      {doc.hasControlledPdf ? (
                        <Chip label={t("dashboard.recentDocs.pdfChip")} size="small" color="primary" sx={compactChipSx} />
                      ) : (
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("common.none")}</Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>
                        {new Date(doc.createdAt).toLocaleDateString()}
                      </Typography>
                    </TableCell>
                  </TableRow>
                ))}
                {recentDocs.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={6} align="center" sx={{ py: 3, color: "text.secondary" }}>
                      {t("dashboard.recentDocs.empty")}
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
                {t("dashboard.recentAudit.title")}
              </Typography>
            </Box>
            <Button
              size="small"
              endIcon={<ArrowForwardIcon />}
              onClick={() => navigate("/document-control/audit")}
            >
              {t("dashboard.recentAudit.allEvents")}
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
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>
                    {formatDateTimeWithTimeZone(log.timestamp)}
                  </Typography>
                </Box>
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>
                    <Trans
                      i18nKey="dashboard.recentAudit.byUser"
                      ns="documentControl"
                      values={{ user: log.userName || t("common.system") }}
                      components={{ strong: <strong /> }}
                    />
                  </Typography>
                  <Chip label={log.actionCategory} size="small" variant="outlined" sx={compactChipSx} />
                </Box>
                {log.reason && (
                  <Typography variant="caption" sx={{ fontStyle: "italic", color: "text.secondary" }}>
                    "{log.reason}"
                  </Typography>
                )}
              </Box>
            ))}
            {recentAudit.length === 0 && (
              <Typography
                variant="caption"
                sx={{
                  color: "text.secondary",
                  py: 3,
                  textAlign: "center"
                }}>
                {t("dashboard.recentAudit.empty")}
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


