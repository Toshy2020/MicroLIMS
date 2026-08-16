import { useEffect, useState } from "react";
import {
  Box, Paper, Typography, Table, TableHead, TableRow, TableCell, TableBody,
  Button, IconButton, Chip, Tooltip, Stack, Alert
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import AddIcon from "@mui/icons-material/Add";
import VisibilityIcon from "@mui/icons-material/Visibility";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import PrintIcon from "@mui/icons-material/Print";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import PowerSettingsNewIcon from "@mui/icons-material/PowerSettingsNew";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import { GeneratedReport, SavedReportConfiguration } from "../types/reportingTypes";
import { SavedReportsService } from "../services/SavedReportsService";
import { ReportMetadataDialog } from "./ReportMetadataDialog";
import { ReportPreviewModal } from "./ReportPreviewModal";
import { useAuth } from "../../../contexts/AuthContext";
import { brandColors } from "../../../theme";

interface SavedReportsTabProps {
  onNewReport: () => void;
  onRunConfiguration: (config: SavedReportConfiguration) => void;
}

export function SavedReportsTab({ onNewReport, onRunConfiguration }: SavedReportsTabProps) {
  const { fullName, userId } = useAuth();

  const [generatedReports, setGeneratedReports] = useState<GeneratedReport[]>([]);
  const [configurations, setConfigurations] = useState<SavedReportConfiguration[]>([]);
  const [loading, setLoading] = useState(true);

  // Dialog states
  const [metadataReport, setMetadataReport] = useState<GeneratedReport | null>(null);
  const [previewReport, setPreviewReport] = useState<GeneratedReport | null>(null);
  const [actionNotice, setActionNotice] = useState<string | null>(null);

  const loadData = () => {
    setLoading(true);
    Promise.all([
      SavedReportsService.getGeneratedReports(),
      SavedReportsService.getConfigurations()
    ]).then(([gen, cfgs]) => {
      setGeneratedReports(gen);
      setConfigurations(cfgs);
    }).finally(() => setLoading(false));
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleDuplicate = async (id: string) => {
    const clone = await SavedReportsService.duplicateConfiguration(id, fullName || "User", userId || 101);
    if (clone) {
      setActionNotice(`Configuration duplicated as "${clone.name}".`);
      loadData();
      setTimeout(() => setActionNotice(null), 3000);
    }
  };

  const handleToggleStatus = async (id: string) => {
    const updated = await SavedReportsService.toggleConfigurationStatus(id);
    if (updated) {
      setActionNotice(`Configuration status updated to ${updated.status}.`);
      loadData();
      setTimeout(() => setActionNotice(null), 3000);
    }
  };

  const handleDownload = (report: GeneratedReport) => {
    const dummyBlob = new Blob([`MicroLIMS Report: ${report.name}\nReport ID: ${report.id}\nGenerated: ${report.generatedOn}`], { type: "text/plain" });
    const url = URL.createObjectURL(dummyBlob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${report.id}_${report.name.toLowerCase().replace(/\s+/g, "_")}.txt`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <Stack spacing={3} sx={{ pb: 4 }}>
      {/* Top Header Bar */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Box>
          <Typography sx={{ fontSize: 18, fontWeight: 700, color: brandColors.sectionTitle }}>
            Saved & Generated Reports
          </Typography>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            Access controlled generated reports and execute saved reporting templates.
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button size="small" variant="outlined" startIcon={<RefreshIcon />} onClick={loadData}>
            Refresh
          </Button>
          <Button size="small" variant="contained" startIcon={<AddIcon />} onClick={onNewReport}>
            New Report
          </Button>
        </Stack>
      </Box>

      {actionNotice && (
        <Alert severity="success" onClose={() => setActionNotice(null)}>
          {actionNotice}
        </Alert>
      )}

      {/* SECTION A: Generated Reports */}
      <Paper sx={{ p: 2.5 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.25 }}>
            <Typography sx={{ fontSize: 15, fontWeight: 700, color: brandColors.pageTitle }}>
              Generated Reports
            </Typography>
            <Chip size="small" label={`${generatedReports.length} Available`} sx={{ bgcolor: brandColors.causeBadgeBg, color: brandColors.causeBadgeText, fontWeight: 700 }} />
          </Box>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
            Controlled generated document artifacts
          </Typography>
        </Box>

        <Table size="small" sx={{ "& th": { fontWeight: 700, fontSize: 12 }, "& td": { fontSize: 12 } }}>
          <TableHead>
            <TableRow>
              <TableCell>Report ID</TableCell>
              <TableCell>Report Name</TableCell>
              <TableCell>Type</TableCell>
              <TableCell>Purpose</TableCell>
              <TableCell>Period</TableCell>
              <TableCell>Generated On</TableCell>
              <TableCell>Generated By</TableCell>
              <TableCell>Status</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {generatedReports.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell sx={{ fontWeight: 700, color: brandColors.sectionTitle }}>{r.id}</TableCell>
                <TableCell sx={{ fontWeight: 600 }}>{r.name}</TableCell>
                <TableCell>{r.type}</TableCell>
                <TableCell>
                  <Chip size="small" label={r.purpose} variant="outlined" sx={{ fontSize: 10, height: 20 }} />
                </TableCell>
                <TableCell sx={{ color: "text.secondary" }}>{r.period}</TableCell>
                <TableCell>{r.generatedOn}</TableCell>
                <TableCell>{r.generatedBy}</TableCell>
                <TableCell>
                  <Chip size="small" label={r.status} color="success" sx={{ fontSize: 10, height: 20, fontWeight: 700 }} />
                </TableCell>
                <TableCell align="right">
                  <Stack direction="row" spacing={0.5} justifyContent="flex-end">
                    <Tooltip title="View Report">
                      <IconButton size="small" color="primary" onClick={() => setPreviewReport(r)}>
                        <VisibilityIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Download Report">
                      <IconButton size="small" onClick={() => handleDownload(r)}>
                        <FileDownloadIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Print">
                      <IconButton size="small" onClick={() => window.print()}>
                        <PrintIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="View Traceability Metadata">
                      <IconButton size="small" onClick={() => setMetadataReport(r)}>
                        <InfoOutlinedIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </Stack>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>

      {/* SECTION B: Saved Report Configurations */}
      <Paper sx={{ p: 2.5 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.25 }}>
            <Typography sx={{ fontSize: 15, fontWeight: 700, color: brandColors.pageTitle }}>
              Saved Configurations
            </Typography>
            <Chip size="small" label={`${configurations.length} Templates`} sx={{ bgcolor: "#f1f5f9", fontWeight: 700 }} />
          </Box>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
            Reusable criteria definitions for Report Builder
          </Typography>
        </Box>

        <Table size="small" sx={{ "& th": { fontWeight: 700, fontSize: 12 }, "& td": { fontSize: 12 } }}>
          <TableHead>
            <TableRow>
              <TableCell>Configuration Name</TableCell>
              <TableCell>Report Type</TableCell>
              <TableCell>Purpose</TableCell>
              <TableCell>Categories</TableCell>
              <TableCell>Last Modified</TableCell>
              <TableCell>Modified By</TableCell>
              <TableCell>Status</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {configurations.map((c) => (
              <TableRow key={c.id} hover>
                <TableCell sx={{ fontWeight: 600, color: brandColors.sectionTitle }}>{c.name}</TableCell>
                <TableCell>{c.reportType}</TableCell>
                <TableCell>
                  <Chip size="small" label={c.purpose} variant="outlined" sx={{ fontSize: 10, height: 20 }} />
                </TableCell>
                <TableCell>{c.categories.join(", ")}</TableCell>
                <TableCell sx={{ color: "text.secondary" }}>{c.lastModified}</TableCell>
                <TableCell>{c.modifiedBy}</TableCell>
                <TableCell>
                  <Chip
                    size="small"
                    label={c.status}
                    color={c.status === "Active" ? "success" : "default"}
                    sx={{ fontSize: 10, height: 20, fontWeight: 700 }}
                  />
                </TableCell>
                <TableCell align="right">
                  <Stack direction="row" spacing={0.5} justifyContent="flex-end">
                    <Tooltip title="Run Configuration in Report Builder">
                      <IconButton size="small" color="primary" onClick={() => onRunConfiguration(c)}>
                        <PlayArrowIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Duplicate Configuration">
                      <IconButton size="small" onClick={() => handleDuplicate(c.id)}>
                        <ContentCopyIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title={c.status === "Active" ? "Disable" : "Enable"}>
                      <IconButton size="small" onClick={() => handleToggleStatus(c.id)}>
                        <PowerSettingsNewIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </Stack>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>

      {/* Modals */}
      <ReportMetadataDialog
        open={!!metadataReport}
        onClose={() => setMetadataReport(null)}
        report={metadataReport}
      />

      <ReportPreviewModal
        open={!!previewReport}
        onClose={() => setPreviewReport(null)}
        previewData={previewReport ? {
          reportTitle: previewReport.name,
          reportPurpose: previewReport.purpose,
          reportingPeriod: previewReport.period,
          generatedAt: previewReport.generatedOn,
          generatedByName: previewReport.generatedBy,
          totalRecords: previewReport.sourceRecordIds.length,
          groups: [
            {
              groupKey: "G1",
              groupTitle: "Summary Group",
              items: []
            }
          ]
        } : null}
        options={{
          includeSpecifications: true,
          includeLimits: true,
          includeAnalyst: true,
          includeReviewer: true,
          includeApprovalInfo: true,
          includeAuditInfo: true,
          includeSignatures: true,
          includePageNumbers: true,
          templateId: previewReport?.templateId ?? "tmpl-micro-std",
          format: "PDF (Portrait)"
        }}
      />
    </Stack>
  );
}
