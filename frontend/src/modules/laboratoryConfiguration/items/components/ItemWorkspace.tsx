import { useState, useEffect } from "react";
import {
  Paper,
  Box,
  Typography,
  Stack,
  Tabs,
  Tab,
  IconButton,
  Button,
  Chip,
  Divider,
  Alert,
  CircularProgress,
  useTheme,
  Accordion,
  AccordionSummary,
  AccordionDetails,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import DescriptionIcon from "@mui/icons-material/Description";
import VerifiedIcon from "@mui/icons-material/Verified";
import DownloadIcon from "@mui/icons-material/Download";
import VisibilityIcon from "@mui/icons-material/Visibility";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import HistoryIcon from "@mui/icons-material/History";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import RuleIcon from "@mui/icons-material/Rule";
import { Item } from "../services/ItemService";
import { CategoryBadge, StatusBadge } from "../../../../components/StatusBadge";
import { ItemDocumentService, ItemDocumentDto, ItemDocumentType, MaterialDocumentStatus } from "../services/ItemDocumentService";
import { UploadItemDocumentDialog } from "./UploadItemDocumentDialog";
import { AuditHistoryDialog } from "../../../../components/AuditHistoryDialog";
import { ItemSpecificationsSection } from "./ItemSpecificationsSection";
import { ItemPreparationConfigurationSection } from "./ItemPreparationConfigurationSection";
import {
  ItemPreparationConfigurationService,
  type ItemPreparationConfiguration
} from "../../../testPreparation/services/ItemPreparationConfigurationService";

interface ItemWorkspaceProps {
  item: Item;
  onClose: () => void;
  onItemUpdated?: () => void;
}

export function ItemWorkspace({ item, onClose, onItemUpdated }: ItemWorkspaceProps) {
  const theme = useTheme();
  const [activeTab, setActiveTab] = useState(0);
  const [documents, setDocuments] = useState<ItemDocumentDto[]>([]);
  const [loadingDocs, setLoadingDocs] = useState(false);
  const [docError, setDocError] = useState<string | null>(null);
  const [uploadDialogOpen, setUploadDialogOpen] = useState(false);
  const [uploadDocType, setUploadDocType] = useState<ItemDocumentType>(ItemDocumentType.Sop);
  const [auditDialogOpen, setAuditDialogOpen] = useState(false);

  // Drives the Overview summary row; bumped by the tab when it saves or
  // approves so the row doesn't go stale behind the user.
  const [prepConfig, setPrepConfig] = useState<ItemPreparationConfiguration | null>(null);
  const [prepConfigVersion, setPrepConfigVersion] = useState(0);

  useEffect(() => {
    let cancelled = false;
    ItemPreparationConfigurationService.get(item.id)
      .then((c) => { if (!cancelled) setPrepConfig(c); })
      .catch(() => { if (!cancelled) setPrepConfig(null); });
    return () => { cancelled = true; };
  }, [item.id, prepConfigVersion]);

  const loadDocuments = async () => {
    setLoadingDocs(true);
    setDocError(null);
    try {
      const docs = await ItemDocumentService.getDocumentsForItem(item.id);
      setDocuments(docs);
    } catch (err: any) {
      setDocError("Failed to load documents.");
    } finally {
      setLoadingDocs(false);
    }
  };

  useEffect(() => {
    loadDocuments();
  }, [item.id]);

  const currentSop = documents.find((d) => d.documentType === ItemDocumentType.Sop && d.status === MaterialDocumentStatus.Current);
  const currentVr = documents.find(
    (d) => d.documentType === ItemDocumentType.VerificationReport && d.status === MaterialDocumentStatus.Current
  );

  const historicalSops = documents.filter((d) => d.documentType === ItemDocumentType.Sop && d.status !== MaterialDocumentStatus.Current);
  const historicalVrs = documents.filter(
    (d) => d.documentType === ItemDocumentType.VerificationReport && d.status !== MaterialDocumentStatus.Current
  );

  const openUploadDialog = (type: ItemDocumentType) => {
    setUploadDocType(type);
    setUploadDialogOpen(true);
  };

  const formatFileSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  };

  const formatDate = (dateStr?: string | null) => {
    if (!dateStr) return "—";
    return new Date(dateStr).toLocaleDateString("en-US", { year: "numeric", month: "short", day: "2-digit" });
  };

  const formatDateTime = (dateStr?: string | null) => {
    if (!dateStr) return "—";
    return new Date(dateStr).toLocaleString("en-US", {
      year: "numeric",
      month: "short",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  const specsCount = item.specifications?.length ?? 0;

  return (
    <Paper
      elevation={0}
      sx={{
        border: "1px solid",
        borderColor: "divider",
        borderRadius: 1.5,
        display: "flex",
        flexDirection: "column",
        height: "100%",
        minHeight: 500,
        bgcolor: "background.paper",
      }}
    >
      {/* Header */}
      <Box
        sx={{
          p: 2,
          borderBottom: "1px solid",
          borderColor: "divider",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start",
          bgcolor: "background.default",
        }}
      >
        <Box>
          <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" sx={{ mb: 0.5 }}>
            <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16, color: "text.primary" }}>
              {item.name}
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary", fontWeight: 500 }}>
              ({item.code})
            </Typography>
          </Stack>

          <Stack direction="row" spacing={1} alignItems="center">
            <CategoryBadge category={item.category} />
            <StatusBadge status={item.isActive ? "Active" : "Frozen"} />
            {item.sopNumber && (
              <Chip
                label={`SOP: ${item.sopNumber}`}
                size="small"
                variant="outlined"
                sx={{ fontSize: 11, height: 22, fontWeight: 500 }}
              />
            )}
          </Stack>
        </Box>

        <IconButton size="small" onClick={onClose} title="Close Workspace">
          <CloseIcon fontSize="small" />
        </IconButton>
      </Box>

      {/* Tabs */}
      <Box sx={{ borderBottom: 1, borderColor: "divider", px: 2, bgcolor: "background.paper" }}>
        <Tabs value={activeTab} onChange={(_, val) => setActiveTab(val)} variant="scrollable" scrollButtons="auto">
          <Tab label="Overview" sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }} />
          <Tab
            label={`Assigned Tests (${item.assignedTests?.length || 0})`}
            sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }}
          />
          <Tab
            label={`Specifications (${specsCount})`}
            sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }}
          />
          <Tab
            label={`Documents & Attachments (${documents.length})`}
            sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }}
          />
          <Tab label="Preparation Configuration" sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }} />
          <Tab label="Audit History" sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }} />
        </Tabs>
      </Box>

      {/* Tab Content */}
      <Box sx={{ p: 2.5, flexGrow: 1, overflowY: "auto" }}>
        {/* Tab 0: Overview */}
        {activeTab === 0 && (
          <Stack spacing={2.5}>
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}>
                  Item Code
                </Typography>
                <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5 }}>
                  {item.code}
                </Typography>
              </Paper>

              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}>
                  Category
                </Typography>
                <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5 }}>
                  {item.category}
                </Typography>
              </Paper>

              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}>
                  SOP Number
                </Typography>
                <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5 }}>
                  {item.sopNumber || "—"}
                </Typography>
              </Paper>

              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}>
                  Status
                </Typography>
                <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5, color: item.isActive ? "success.main" : "text.secondary" }}>
                  {item.isActive ? "Active (Sample receipt enabled)" : "Frozen (Sample receipt disabled)"}
                </Typography>
              </Paper>
            </Box>

            <Paper sx={{ p: 2, border: "1px solid", borderColor: "divider" }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                Configuration Summary
              </Typography>
              <Stack spacing={1}>
                <Box
                  sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    cursor: "pointer",
                    p: 0.5,
                    borderRadius: 1,
                    "&:hover": { bgcolor: "action.hover" },
                  }}
                  onClick={() => setActiveTab(1)}
                >
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    Assigned Tests:
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600, color: "primary.main" }}>
                    {item.assignedTests?.length || 0} tests configured →
                  </Typography>
                </Box>

                <Box
                  sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    cursor: "pointer",
                    p: 0.5,
                    borderRadius: 1,
                    "&:hover": { bgcolor: "action.hover" },
                  }}
                  onClick={() => setActiveTab(2)}
                >
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    Specifications:
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600, color: "primary.main" }}>
                    {specsCount} specifications defined →
                  </Typography>
                </Box>

                <Box
                  sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    cursor: "pointer",
                    p: 0.5,
                    borderRadius: 1,
                    "&:hover": { bgcolor: "action.hover" },
                  }}
                  onClick={() => setActiveTab(3)}
                >
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    Controlled Documents:
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600, color: "primary.main" }}>
                    {currentSop ? "SOP Available" : "No SOP"} • {currentVr ? "VR Available" : "No VR"} →
                  </Typography>
                </Box>

                <Box
                  sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    cursor: "pointer",
                    p: 0.5,
                    borderRadius: 1,
                    "&:hover": { bgcolor: "action.hover" },
                  }}
                  onClick={() => setActiveTab(4)}
                >
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    Preparation Steps:
                  </Typography>
                  <Typography
                    variant="body2"
                    sx={{
                      fontWeight: 600,
                      color: prepConfig?.approvalStatus === "PendingReview" ? "warning.main" : "primary.main"
                    }}
                  >
                    {prepConfig
                      ? `Configured · ${prepConfig.approvalStatus === "Approved" ? "Approved" : "Pending Approval"} →`
                      : "Not configured →"}
                  </Typography>
                </Box>
              </Stack>
            </Paper>
          </Stack>
        )}

        {/* Tab 1: Assigned Tests */}
        {activeTab === 1 && (
          <Stack spacing={2}>
            <Alert severity="info" sx={{ py: 0.5, fontSize: 12 }}>
              These tests are automatically assigned when a new sample is received for <strong>{item.name}</strong>.
            </Alert>

            {item.assignedTests && item.assignedTests.length > 0 ? (
              <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 1.5 }}>
                {item.assignedTests.map((t, idx) => (
                  <Paper
                    key={idx}
                    sx={{
                      p: 1.5,
                      border: "1px solid",
                      borderColor: "divider",
                      borderRadius: 1,
                      bgcolor: "background.default",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                    }}
                  >
                    <Typography variant="body2" sx={{ fontWeight: 700, color: "text.primary" }}>
                      {t.displayName || t.testCode}
                    </Typography>
                    <Chip label={t.testCode} size="small" sx={{ fontSize: 11, height: 20, fontWeight: 600 }} />
                  </Paper>
                ))}
              </Box>
            ) : (
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                No tests assigned to this item yet.
              </Typography>
            )}
          </Stack>
        )}

        {/* Tab 2: Specifications */}
        {activeTab === 2 && (
          <ItemSpecificationsSection
            item={item}
            onSpecsChanged={() => {
              onItemUpdated?.();
            }}
          />
        )}

        {/* Tab 3: Documents & Attachments */}
        {activeTab === 3 && (
          <Stack spacing={3}>
            {docError && <Alert severity="error">{docError}</Alert>}

            {/* SOP Section */}
            <Box>
              <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1.5 }}>
                <Typography variant="subtitle2" sx={{ fontWeight: 700, display: "flex", alignItems: "center", gap: 1 }}>
                  <DescriptionIcon fontSize="small" sx={{ color: theme.custom.status.purple.text }} />
                  SOP (Standard Operating Procedure)
                </Typography>
                <Button
                  size="small"
                  variant="outlined"
                  startIcon={<UploadFileIcon fontSize="small" />}
                  onClick={() => openUploadDialog(ItemDocumentType.Sop)}
                  sx={{ textTransform: "none" }}
                >
                  + Upload SOP
                </Button>
              </Stack>

              {currentSop ? (
                <DocumentCard doc={currentSop} formatFileSize={formatFileSize} formatDate={formatDate} formatDateTime={formatDateTime} />
              ) : (
                <Paper sx={{ p: 2, textAlign: "center", border: "1px dashed", borderColor: "divider", bgcolor: "action.hover" }}>
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    No current SOP uploaded for this item.
                  </Typography>
                </Paper>
              )}

              {historicalSops.length > 0 && (
                <Accordion sx={{ mt: 1, border: "1px solid", borderColor: "divider", boxShadow: "none" }}>
                  <AccordionSummary expandIcon={<ExpandMoreIcon fontSize="small" />}>
                    <Typography variant="caption" sx={{ fontWeight: 600, color: "text.secondary" }}>
                      Historical SOP Versions ({historicalSops.length})
                    </Typography>
                  </AccordionSummary>
                  <AccordionDetails sx={{ pt: 0 }}>
                    <Stack spacing={1}>
                      {historicalSops.map((doc) => (
                        <DocumentCard
                          key={doc.id}
                          doc={doc}
                          formatFileSize={formatFileSize}
                          formatDate={formatDate}
                          formatDateTime={formatDateTime}
                          isHistorical
                        />
                      ))}
                    </Stack>
                  </AccordionDetails>
                </Accordion>
              )}
            </Box>

            <Divider />

            {/* Verification Report Section */}
            <Box>
              <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1.5 }}>
                <Typography variant="subtitle2" sx={{ fontWeight: 700, display: "flex", alignItems: "center", gap: 1 }}>
                  <VerifiedIcon fontSize="small" sx={{ color: "success.main" }} />
                  Verification Report
                </Typography>
                <Button
                  size="small"
                  variant="outlined"
                  startIcon={<UploadFileIcon fontSize="small" />}
                  onClick={() => openUploadDialog(ItemDocumentType.VerificationReport)}
                  sx={{ textTransform: "none" }}
                >
                  + Upload Verification Report
                </Button>
              </Stack>

              {currentVr ? (
                <DocumentCard doc={currentVr} formatFileSize={formatFileSize} formatDate={formatDate} formatDateTime={formatDateTime} />
              ) : (
                <Paper sx={{ p: 2, textAlign: "center", border: "1px dashed", borderColor: "divider", bgcolor: "action.hover" }}>
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    No current Verification Report uploaded for this item.
                  </Typography>
                </Paper>
              )}

              {historicalVrs.length > 0 && (
                <Accordion sx={{ mt: 1, border: "1px solid", borderColor: "divider", boxShadow: "none" }}>
                  <AccordionSummary expandIcon={<ExpandMoreIcon fontSize="small" />}>
                    <Typography variant="caption" sx={{ fontWeight: 600, color: "text.secondary" }}>
                      Historical Verification Reports ({historicalVrs.length})
                    </Typography>
                  </AccordionSummary>
                  <AccordionDetails sx={{ pt: 0 }}>
                    <Stack spacing={1}>
                      {historicalVrs.map((doc) => (
                        <DocumentCard
                          key={doc.id}
                          doc={doc}
                          formatFileSize={formatFileSize}
                          formatDate={formatDate}
                          formatDateTime={formatDateTime}
                          isHistorical
                        />
                      ))}
                    </Stack>
                  </AccordionDetails>
                </Accordion>
              )}
            </Box>
          </Stack>
        )}

        {/* Tab 4: Preparation Configuration */}
        {activeTab === 4 && (
          <ItemPreparationConfigurationSection
            itemId={item.id}
            itemName={item.name}
            onChanged={() => setPrepConfigVersion((v) => v + 1)}
          />
        )}

        {/* Tab 5: Audit History */}
        {activeTab === 5 && (
          <Stack spacing={2} alignItems="flex-start">
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              View full GxP audit log history for Item <strong>{item.name}</strong> ({item.code}).
            </Typography>
            <Button
              variant="contained"
              startIcon={<HistoryIcon />}
              onClick={() => setAuditDialogOpen(true)}
              sx={{ textTransform: "none" }}
            >
              Open Audit History Log
            </Button>
          </Stack>
        )}
      </Box>

      {/* Upload Dialog */}
      <UploadItemDocumentDialog
        open={uploadDialogOpen}
        itemId={item.id}
        itemName={item.name}
        defaultDocType={uploadDocType}
        onClose={() => setUploadDialogOpen(false)}
        onSuccess={loadDocuments}
      />

      {/* Audit Dialog */}
      <AuditHistoryDialog
        open={auditDialogOpen}
        onClose={() => setAuditDialogOpen(false)}
        entityName="Item"
        entityId={item.id.toString()}
      />
    </Paper>
  );
}

// Subcomponent for displaying controlled document card
function DocumentCard({
  doc,
  formatFileSize,
  formatDate,
  formatDateTime,
  isHistorical = false,
}: {
  doc: ItemDocumentDto;
  formatFileSize: (b: number) => string;
  formatDate: (d?: string | null) => string;
  formatDateTime: (d?: string | null) => string;
  isHistorical?: boolean;
}) {
  const theme = useTheme();

  return (
    <Paper
      sx={{
        p: 2,
        border: "1px solid",
        borderColor: isHistorical ? "divider" : "primary.main",
        borderRadius: 1.5,
        bgcolor: isHistorical ? "action.hover" : "background.paper",
        opacity: isHistorical ? 0.8 : 1,
      }}
    >
      <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}>
        <Box>
          <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.5 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.primary" }}>
              {doc.originalFileName}
            </Typography>
            <Chip
              label={doc.version}
              size="small"
              color={isHistorical ? "default" : "primary"}
              sx={{ fontWeight: 700, fontSize: 11, height: 20 }}
            />
            {isHistorical && (
              <Chip label="Superseded" size="small" color="warning" sx={{ fontWeight: 600, fontSize: 10, height: 18 }} />
            )}
          </Stack>

          <Typography variant="caption" component="div" sx={{ color: "text.secondary", mt: 0.5 }}>
            Effective Date: <strong>{formatDate(doc.effectiveDate)}</strong> • Size: {formatFileSize(doc.fileSizeBytes)}
          </Typography>
          <Typography variant="caption" component="div" sx={{ color: "text.secondary", mt: 0.25 }}>
            Uploaded by: <strong>{doc.uploadedByUserName}</strong> on {formatDateTime(doc.uploadedAt)}
          </Typography>
        </Box>

        <Stack direction="row" spacing={1}>
          <Button
            size="small"
            variant="outlined"
            startIcon={<VisibilityIcon fontSize="small" />}
            href={ItemDocumentService.getContentUrl(doc.id)}
            target="_blank"
            rel="noopener noreferrer"
            sx={{ textTransform: "none" }}
          >
            View
          </Button>
          <Button
            size="small"
            variant="contained"
            color="primary"
            startIcon={<DownloadIcon fontSize="small" />}
            href={ItemDocumentService.getContentUrl(doc.id, true)}
            sx={{ textTransform: "none" }}
          >
            Download
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
}
