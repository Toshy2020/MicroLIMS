import { useEffect, useState } from "react";
import {
  Box, Paper, Typography, TextField, FormControl, InputLabel, Select, MenuItem,
  Button, Checkbox, FormControlLabel, FormGroup, Stack, Table, TableHead, TableRow, TableCell,
  TableBody, Divider, Chip, IconButton, Tooltip
} from "@mui/material";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import TableViewIcon from "@mui/icons-material/TableView";
import FullscreenIcon from "@mui/icons-material/Fullscreen";
import BookmarkBorderIcon from "@mui/icons-material/BookmarkBorder";
import RestartAltIcon from "@mui/icons-material/RestartAlt";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import {
  ReportBuilderCriteria,
  ReportBuilderOptions,
  ReportFormat,
  ReportPreviewData,
  ReportPurpose,
  ReportType,
  ResultLevel,
  ResultRecordItem,
  SampleCategory
} from "../types/reportingTypes";
import { REPORT_TYPES, REPORT_PURPOSES, REPORT_TEMPLATES } from "../constants/reportPresets";
import { ReportBuilderService } from "../services/ReportBuilderService";
import { StatusBadge } from "../../../components/StatusBadge";
import { ReportPreviewModal } from "./ReportPreviewModal";
import { SaveConfigDialog } from "./SaveConfigDialog";
import { useAuth } from "../../../contexts/AuthContext";
import { brandColors } from "../../../theme";

interface ReportBuilderTabProps {
  preloadedRecords?: ResultRecordItem[];
}

export function ReportBuilderTab({ preloadedRecords }: ReportBuilderTabProps) {
  const { fullName } = useAuth();

  const [criteria, setCriteria] = useState<ReportBuilderCriteria>({
    reportType: "Microbiology Results Report",
    reportPurpose: "Operational Report",
    fromDate: "2026-08-01",
    toDate: "2026-08-15",
    category: "",
    selectedProducts: [],
    selectedTests: [],
    location: "",
    resultLevel: "",
    status: "",
    approvalStatus: "Approved",
    groupBy: "Product",
    sortBy: "DateDesc"
  });

  const [options, setOptions] = useState<ReportBuilderOptions>({
    includeSpecifications: true,
    includeLimits: true,
    includeAnalyst: true,
    includeReviewer: true,
    includeApprovalInfo: true,
    includeAuditInfo: false,
    includeSignatures: true,
    includePageNumbers: true,
    templateId: "tmpl-micro-std",
    format: "PDF (Portrait)"
  });

  const [previewData, setPreviewData] = useState<ReportPreviewData | null>(null);
  const [loading, setLoading] = useState(false);
  const [fullPreviewOpen, setFullPreviewOpen] = useState(false);
  const [saveConfigOpen, setSaveConfigOpen] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);

  useEffect(() => {
    setLoading(true);
    ReportBuilderService.generatePreview(criteria, options, fullName || "Amal Hamdy")
      .then(setPreviewData)
      .finally(() => setLoading(false));
  }, [criteria, options, fullName]);

  const handleReset = () => {
    setCriteria({
      reportType: "Microbiology Results Report",
      reportPurpose: "Operational Report",
      fromDate: "2026-08-01",
      toDate: "2026-08-15",
      category: "",
      selectedProducts: [],
      selectedTests: [],
      location: "",
      resultLevel: "",
      status: "",
      approvalStatus: "Approved",
      groupBy: "Product",
      sortBy: "DateDesc"
    });
  };

  const handleExportCsv = () => {
    if (!previewData) return;
    const allItems = previewData.groups.flatMap((g) => g.items);
    ReportBuilderService.exportCsv(allItems, previewData.reportTitle);
  };

  const handleGeneratePdf = () => {
    setFullPreviewOpen(true);
  };

  return (
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", lg: "280px 1fr 280px" }, gap: 2, alignItems: "start", pb: 4 }}>
      {/* LEFT COLUMN: Report Criteria */}
      <Paper sx={{ p: 2.5 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
          <Typography sx={{ fontSize: 15, fontWeight: 700, color: brandColors.sectionTitle }}>
            Report Criteria
          </Typography>
          <IconButton size="small" onClick={handleReset} title="Reset Criteria">
            <RestartAltIcon fontSize="small" />
          </IconButton>
        </Box>

        <Stack spacing={2}>
          <FormControl fullWidth size="small">
            <InputLabel>Report Type</InputLabel>
            <Select
              label="Report Type"
              value={criteria.reportType}
              onChange={(e) => setCriteria((c) => ({ ...c, reportType: e.target.value as ReportType }))}
            >
              {REPORT_TYPES.map((t) => (
                <MenuItem key={t} value={t}>{t}</MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Report Purpose</InputLabel>
            <Select
              label="Report Purpose"
              value={criteria.reportPurpose}
              onChange={(e) => setCriteria((c) => ({ ...c, reportPurpose: e.target.value as ReportPurpose }))}
            >
              {REPORT_PURPOSES.map((p) => (
                <MenuItem key={p} value={p}>{p}</MenuItem>
              ))}
            </Select>
          </FormControl>

          <Box>
            <Typography sx={{ fontSize: 11.5, color: "text.secondary", mb: 0.5 }}>Date Range</Typography>
            <Box sx={{ display: "flex", gap: 1 }}>
              <TextField
                size="small"
                type="date"
                fullWidth
                value={criteria.fromDate ?? ""}
                onChange={(e) => setCriteria((c) => ({ ...c, fromDate: e.target.value }))}
              />
              <TextField
                size="small"
                type="date"
                fullWidth
                value={criteria.toDate ?? ""}
                onChange={(e) => setCriteria((c) => ({ ...c, toDate: e.target.value }))}
              />
            </Box>
          </Box>

          <FormControl fullWidth size="small">
            <InputLabel>Category</InputLabel>
            <Select
              label="Category"
              value={criteria.category ?? ""}
              onChange={(e) => setCriteria((c) => ({ ...c, category: (e.target.value || "") as SampleCategory }))}
            >
              <MenuItem value="">All Categories</MenuItem>
              <MenuItem value="FinishedProduct">Finished Product</MenuItem>
              <MenuItem value="RawMaterial">Raw Material</MenuItem>
              <MenuItem value="PackagingMaterial">Packaging Material</MenuItem>
              <MenuItem value="Water">Water</MenuItem>
              <MenuItem value="EnvironmentalMonitoring">Environmental Monitoring</MenuItem>
              <MenuItem value="AfterCleaning">After Cleaning</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Product / Item</InputLabel>
            <Select
              label="Product / Item"
              value={criteria.selectedProducts[0] ?? ""}
              onChange={(e) => setCriteria((c) => ({ ...c, selectedProducts: e.target.value ? [e.target.value] : [] }))}
            >
              <MenuItem value="">All Products / Items</MenuItem>
              <MenuItem value="Osteocare Liquid">Osteocare Liquid</MenuItem>
              <MenuItem value="Honey">Honey</MenuItem>
              <MenuItem value="SWT">SWT (Water)</MenuItem>
              <MenuItem value="Filling Line 1">Filling Line 1</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Test</InputLabel>
            <Select
              label="Test"
              value={criteria.selectedTests[0] ?? ""}
              onChange={(e) => setCriteria((c) => ({ ...c, selectedTests: e.target.value ? [e.target.value] : [] }))}
            >
              <MenuItem value="">All Tests</MenuItem>
              <MenuItem value="TAMC">TAMC</MenuItem>
              <MenuItem value="TYMC">TYMC</MenuItem>
              <MenuItem value="WATER_TAMC">Water TAMC</MenuItem>
              <MenuItem value="PATHOGEN_ECOLI">E. coli</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Result Level</InputLabel>
            <Select
              label="Result Level"
              value={criteria.resultLevel ?? ""}
              onChange={(e) => setCriteria((c) => ({ ...c, resultLevel: (e.target.value || "") as ResultLevel }))}
            >
              <MenuItem value="">All Levels</MenuItem>
              <MenuItem value="WithinLimit">Within Limit</MenuItem>
              <MenuItem value="AlertLevel">Alert Level</MenuItem>
              <MenuItem value="ActionLevel">Action Level</MenuItem>
              <MenuItem value="OutOfSpecification">Out of Specification</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Approval Status</InputLabel>
            <Select
              label="Approval Status"
              value={criteria.approvalStatus ?? ""}
              onChange={(e) => setCriteria((c) => ({ ...c, approvalStatus: (e.target.value || "") as any }))}
            >
              <MenuItem value="">All</MenuItem>
              <MenuItem value="Approved">Approved</MenuItem>
              <MenuItem value="Pending">Pending</MenuItem>
              <MenuItem value="Rejected">Rejected</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Group By</InputLabel>
            <Select
              label="Group By"
              value={criteria.groupBy}
              onChange={(e) => setCriteria((c) => ({ ...c, groupBy: e.target.value as any }))}
            >
              <MenuItem value="Product">Product</MenuItem>
              <MenuItem value="Category">Category</MenuItem>
              <MenuItem value="Test">Test</MenuItem>
              <MenuItem value="Location">Location</MenuItem>
              <MenuItem value="Date">Date</MenuItem>
              <MenuItem value="None">None</MenuItem>
            </Select>
          </FormControl>

          <Button
            variant="outlined"
            fullWidth
            startIcon={<BookmarkBorderIcon />}
            onClick={() => setSaveConfigOpen(true)}
          >
            Save Configuration
          </Button>
        </Stack>
      </Paper>

      {/* CENTER COLUMN: Live Report Preview */}
      <Paper sx={{ p: 3, minHeight: 600 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, pb: 1.5, borderBottom: "2px solid #7b2d8e" }}>
          <Box>
            <Typography sx={{ fontSize: 11, fontWeight: 700, color: brandColors.sectionTitle, textTransform: "uppercase", letterSpacing: 0.5 }}>
              Report Preview
            </Typography>
            <Typography sx={{ fontSize: 18, fontWeight: 800, color: "#1e293b" }}>
              {previewData?.reportTitle ?? criteria.reportType}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.25 }}>
              {previewData?.reportingPeriod}
            </Typography>
          </Box>
          <Box sx={{ textAlign: "right" }}>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              {previewData?.generatedAt}
            </Typography>
            <Chip size="small" label={criteria.reportPurpose} sx={{ mt: 0.5, fontSize: 10, height: 20 }} />
          </Box>
        </Box>

        {previewData?.groups.map((group, gIdx) => (
          <Box key={gIdx} sx={{ mb: 2.5 }}>
            <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.pageTitle, mb: 1, bgcolor: "#f8fafc", px: 1.25, py: 0.5, borderRadius: 1 }}>
              {group.groupTitle}
            </Typography>

            <Table size="small" sx={{ "& th": { bgcolor: "#fbfcfe", fontWeight: 700, fontSize: 11.5 }, "& td": { fontSize: 12 } }}>
              <TableHead>
                <TableRow>
                  <TableCell>Sample / Reference</TableCell>
                  <TableCell>Product / Item</TableCell>
                  <TableCell>Test</TableCell>
                  <TableCell>Result</TableCell>
                  {options.includeSpecifications && <TableCell>Specification</TableCell>}
                  <TableCell>Result Level</TableCell>
                  <TableCell>Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {group.items.map((row) => (
                  <TableRow key={row.id} hover>
                    <TableCell sx={{ fontWeight: 600 }}>{row.referenceNumber}</TableCell>
                    <TableCell>{row.subjectName}</TableCell>
                    <TableCell>{row.testDisplayName || row.testCode}</TableCell>
                    <TableCell sx={{ fontWeight: 700, color: brandColors.sectionTitle }}>
                      {row.reportedValue} {row.unit ?? ""}
                    </TableCell>
                    {options.includeSpecifications && (
                      <TableCell>{row.specLimit ?? "—"}</TableCell>
                    )}
                    <TableCell><StatusBadge status={row.resultLevel} /></TableCell>
                    <TableCell><StatusBadge status={row.approvalStatus} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        ))}

        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 3, pt: 1.5, borderTop: 1, borderColor: "divider" }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            Total Records: <strong>{previewData?.totalRecords ?? 0}</strong>
          </Typography>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <IconButton size="small" disabled={currentPage <= 1} onClick={() => setCurrentPage((p) => p - 1)}>
              <ChevronLeftIcon fontSize="small" />
            </IconButton>
            <Typography sx={{ fontSize: 12 }}>{currentPage}</Typography>
            <IconButton size="small" disabled onClick={() => setCurrentPage((p) => p + 1)}>
              <ChevronRightIcon fontSize="small" />
            </IconButton>
          </Box>
        </Box>
      </Paper>

      {/* RIGHT COLUMN: Report Options & Actions */}
      <Paper sx={{ p: 2.5 }}>
        <Typography sx={{ fontSize: 15, fontWeight: 700, color: brandColors.sectionTitle, mb: 1.5 }}>
          Report Options
        </Typography>

        <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", mb: 0.5 }}>
          Include in Report
        </Typography>

        <FormGroup sx={{ mb: 2 }}>
          <FormControlLabel
            control={
              <Checkbox
                size="small"
                checked={options.includeSpecifications}
                onChange={(e) => setOptions((o) => ({ ...o, includeSpecifications: e.target.checked }))}
              />
            }
            label={<Typography sx={{ fontSize: 12.5 }}>Include Specifications</Typography>}
          />
          <FormControlLabel
            control={
              <Checkbox
                size="small"
                checked={options.includeLimits}
                onChange={(e) => setOptions((o) => ({ ...o, includeLimits: e.target.checked }))}
              />
            }
            label={<Typography sx={{ fontSize: 12.5 }}>Include Limits</Typography>}
          />
          <FormControlLabel
            control={
              <Checkbox
                size="small"
                checked={options.includeAnalyst}
                onChange={(e) => setOptions((o) => ({ ...o, includeAnalyst: e.target.checked }))}
              />
            }
            label={<Typography sx={{ fontSize: 12.5 }}>Include Analyst</Typography>}
          />
          <FormControlLabel
            control={
              <Checkbox
                size="small"
                checked={options.includeReviewer}
                onChange={(e) => setOptions((o) => ({ ...o, includeReviewer: e.target.checked }))}
              />
            }
            label={<Typography sx={{ fontSize: 12.5 }}>Include Reviewer</Typography>}
          />
          <FormControlLabel
            control={
              <Checkbox
                size="small"
                checked={options.includeApprovalInfo}
                onChange={(e) => setOptions((o) => ({ ...o, includeApprovalInfo: e.target.checked }))}
              />
            }
            label={<Typography sx={{ fontSize: 12.5 }}>Include Approval Information</Typography>}
          />
          <FormControlLabel
            control={
              <Checkbox
                size="small"
                checked={options.includeAuditInfo}
                onChange={(e) => setOptions((o) => ({ ...o, includeAuditInfo: e.target.checked }))}
              />
            }
            label={<Typography sx={{ fontSize: 12.5 }}>Include Audit Information</Typography>}
          />
          <FormControlLabel
            control={
              <Checkbox
                size="small"
                checked={options.includeSignatures}
                onChange={(e) => setOptions((o) => ({ ...o, includeSignatures: e.target.checked }))}
              />
            }
            label={<Typography sx={{ fontSize: 12.5 }}>Include Signatures</Typography>}
          />
          <FormControlLabel
            control={
              <Checkbox
                size="small"
                checked={options.includePageNumbers}
                onChange={(e) => setOptions((o) => ({ ...o, includePageNumbers: e.target.checked }))}
              />
            }
            label={<Typography sx={{ fontSize: 12.5 }}>Include Page Numbers</Typography>}
          />
        </FormGroup>

        <Stack spacing={2} sx={{ mb: 3 }}>
          <FormControl fullWidth size="small">
            <InputLabel>Report Format</InputLabel>
            <Select
              label="Report Format"
              value={options.format}
              onChange={(e) => setOptions((o) => ({ ...o, format: e.target.value as ReportFormat }))}
            >
              <MenuItem value="PDF (Portrait)">PDF (Portrait)</MenuItem>
              <MenuItem value="PDF (Landscape)">PDF (Landscape)</MenuItem>
              <MenuItem value="Excel (.xlsx)">Excel (.xlsx)</MenuItem>
              <MenuItem value="CSV (.csv)">CSV (.csv)</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Template</InputLabel>
            <Select
              label="Template"
              value={options.templateId}
              onChange={(e) => setOptions((o) => ({ ...o, templateId: e.target.value }))}
            >
              {REPORT_TEMPLATES.map((tmpl) => (
                <MenuItem key={tmpl.id} value={tmpl.id}>{tmpl.name}</MenuItem>
              ))}
            </Select>
          </FormControl>
        </Stack>

        <Stack spacing={1.25}>
          <Button
            variant="contained"
            fullWidth
            startIcon={<PictureAsPdfIcon />}
            onClick={handleGeneratePdf}
          >
            Generate PDF
          </Button>
          <Button
            variant="outlined"
            fullWidth
            startIcon={<TableViewIcon />}
            onClick={handleExportCsv}
          >
            Export Excel / CSV
          </Button>
          <Button
            variant="outlined"
            fullWidth
            startIcon={<FullscreenIcon />}
            onClick={() => setFullPreviewOpen(true)}
          >
            Preview Full Report
          </Button>
        </Stack>
      </Paper>

      {/* Modals */}
      <ReportPreviewModal
        open={fullPreviewOpen}
        onClose={() => setFullPreviewOpen(false)}
        previewData={previewData}
        options={options}
      />

      <SaveConfigDialog
        open={saveConfigOpen}
        onClose={() => setSaveConfigOpen(false)}
        criteria={criteria}
        options={options}
        onSaved={() => {}}
      />
    </Box>
  );
}
