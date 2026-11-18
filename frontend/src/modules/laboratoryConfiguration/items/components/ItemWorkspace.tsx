import { useState, useEffect, useMemo } from "react";
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
  Accordion,
  AccordionSummary,
  AccordionDetails,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import VerifiedIcon from "@mui/icons-material/Verified";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import HistoryIcon from "@mui/icons-material/History";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";
import { Item } from "../services/ItemService";
import { CategoryBadge, StatusBadge } from "../../../../components/StatusBadge";
import { ItemDocumentService, ItemDocumentDto, ItemDocumentType, MaterialDocumentStatus } from "../services/ItemDocumentService";
import { UploadItemDocumentDialog } from "./UploadItemDocumentDialog";
import { AuditHistoryDialog } from "../../../../components/AuditHistoryDialog";
import { ItemDocumentActionButtons } from "../../../../components/ItemDocumentActionButtons";
import { ItemStmPanel } from "../../../../components/ItemStmPanel";
import { ItemSpecificationsSection } from "./ItemSpecificationsSection";
import { ItemAssignedTestsSection } from "./ItemAssignedTestsSection";
import { ItemPreparationConfigurationSection } from "./ItemPreparationConfigurationSection";
import { NotApplicableTestsSection } from "./NotApplicableTestsSection";
import { SupplierAttributesSection } from "./SupplierAttributesSection";
import {
  ItemPreparationConfigurationService,
  type ItemPreparationConfiguration
} from "../../../testPreparation/services/ItemPreparationConfigurationService";
import { clickable } from "../../../../utils/clickable";
import { useHasPermission } from "../../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../../routes/routes";
import { useTrackRecent } from "../../../navigation/useTrackRecent";
import { formatDateTimeWithTimeZone, formatLabCalendarDate } from "../../../../utils/formatDate";

export const ITEM_WORKSPACE_TABS = [
  "overview",
  "tests",
  "specifications",
  "records",
  "documents",
  "preparation",
  "audit",
] as const;
export type ItemWorkspaceTab = (typeof ITEM_WORKSPACE_TABS)[number];

/** Resolves an untrusted value (e.g. a URL parameter) to a workspace tab; unknown values fall back to Overview. */
export function parseItemWorkspaceTab(value: string | null | undefined): ItemWorkspaceTab {
  return ITEM_WORKSPACE_TABS.find((t) => t === value) ?? "overview";
}

interface ItemWorkspaceProps {
  item: Item;
  onClose: () => void;
  onItemUpdated?: () => void;
  /** Controlled active tab (e.g. kept in the URL). When absent the workspace keeps its own tab state. */
  activeTab?: ItemWorkspaceTab;
  onTabChange?: (tab: ItemWorkspaceTab) => void;
}

export function ItemWorkspace({ item, onClose, onItemUpdated, activeTab: controlledTab, onTabChange }: ItemWorkspaceProps) {
  const { t } = useTranslation("labConfig");
  // The QC Manager opens this workspace to approve not-applicable tests; changing the item needs Items.Manage.
  const canManage = useHasPermission(PERMISSIONS.ITEMS_MANAGE);
  useTrackRecent("item", item.id);
  const [internalTab, setInternalTab] = useState<ItemWorkspaceTab>("overview");
  const activeTab = controlledTab ?? internalTab;
  const setActiveTab = (tab: ItemWorkspaceTab) => {
    if (controlledTab === undefined) setInternalTab(tab);
    onTabChange?.(tab);
  };
  const [documents, setDocuments] = useState<ItemDocumentDto[]>([]);
  const [_loadingDocs, setLoadingDocs] = useState(false);
  const [docError, setDocError] = useState<string | null>(null);
  const [uploadDialogOpen, setUploadDialogOpen] = useState(false);
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
    } catch {
      setDocError(t("items.workspace.loadDocsError"));
    } finally {
      setLoadingDocs(false);
    }
  };

  useEffect(() => {
    loadDocuments();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item.id]);

  const currentVr = documents.find(
    (d) => d.documentType === ItemDocumentType.VerificationReport && d.status === MaterialDocumentStatus.Current
  );

  const historicalVrs = documents.filter(
    (d) => d.documentType === ItemDocumentType.VerificationReport && d.status !== MaterialDocumentStatus.Current
  );

  const openUploadDialog = () => {
    setUploadDialogOpen(true);
  };

  const formatFileSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  };

  const formatDate = (dateStr?: string | null) => {
    if (!dateStr) return "—";
    return formatLabCalendarDate(dateStr);
  };

  const formatDateTime = (dateStr?: string | null) => {
    if (!dateStr) return "—";
    return formatDateTimeWithTimeZone(dateStr);
  };

  const specsCount = item.specifications?.length ?? 0;

  const grades = useMemo(() => {
    const declaredGrades = (item as Item & { grades?: unknown }).grades;
    if (Array.isArray(declaredGrades)) {
      return declaredGrades as string[];
    }
    const set = new Set<string>();
    for (const s of item.specifications || []) {
      if (s.grade && s.grade.trim()) {
        set.add(s.grade.trim());
      }
    }
    return Array.from(set).sort();
  }, [item]);

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
          <Stack useFlexGap
            direction="row"
            spacing={1}
            sx={{
              alignItems: "center",
              flexWrap: "wrap",
              mb: 0.5
            }}>
            <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16, color: "text.primary" }}>
              {item.name}
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary", fontWeight: 500 }}>
              ({item.code})
            </Typography>
          </Stack>

          <Stack direction="row" spacing={1} sx={{
            alignItems: "center"
          }}>
            <CategoryBadge category={item.category} />
            <StatusBadge status={item.isActive ? "Active" : "Frozen"} />
            {item.sopNumber && (
              <Chip
                label={t("items.workspace.sopChip", { sopNumber: item.sopNumber })}
                size="small"
                variant="outlined"
                sx={{ fontSize: 12, height: 22, fontWeight: 500 }}
              />
            )}
          </Stack>
        </Box>

        <IconButton aria-label={t("items.workspace.closeAriaLabel")} size="small" onClick={onClose} title={t("items.workspace.closeTitle")}>
          <CloseIcon fontSize="small" />
        </IconButton>
      </Box>

      {/* Tabs */}
      <Box sx={{ borderBottom: 1, borderColor: "divider", px: 2, bgcolor: "background.paper" }}>
        <Tabs value={activeTab} onChange={(_, val: ItemWorkspaceTab) => setActiveTab(val)} variant="scrollable" scrollButtons="auto">
          <Tab value="overview" label={t("items.workspace.tabs.overview")} sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }} />
          <Tab
            value="tests"
            label={t("items.workspace.tabs.tests", { count: item.assignedTests?.length || 0 })}
            sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }}
          />
          <Tab
            value="specifications"
            label={t("items.workspace.tabs.specifications", { count: specsCount })}
            sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }}
          />
          <Tab value="records" label={t("items.workspace.tabs.records")} sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }} />
          <Tab
            value="documents"
            label={t("items.workspace.tabs.documents", { count: documents.length })}
            sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }}
          />
          <Tab value="preparation" label={t("items.workspace.tabs.preparation")} sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }} />
          <Tab value="audit" label={t("items.workspace.tabs.audit")} sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }} />
        </Tabs>
      </Box>

      {/* Tab Content */}
      <Box sx={{ p: 2.5, flexGrow: 1, overflowY: "auto" }}>
        {/* Tab 0: Overview */}
        {activeTab === "overview" && (
          <Stack spacing={2.5}>
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}>
                  {t("items.workspace.overview.itemCode")}
                </Typography>
                <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5 }}>
                  {item.code}
                </Typography>
              </Paper>

              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}>
                  {t("items.workspace.overview.category")}
                </Typography>
                <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5 }}>
                  {item.category}
                </Typography>
              </Paper>

              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}>
                  {t("items.workspace.overview.sopNumber")}
                </Typography>
                <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5 }}>
                  {item.sopNumber || "—"}
                </Typography>
              </Paper>

              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}>
                  {t("items.workspace.overview.status")}
                </Typography>
                <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5, color: item.isActive ? "success.main" : "text.secondary" }}>
                  {item.isActive ? t("items.workspace.overview.activeStatus") : t("items.workspace.overview.frozenStatus")}
                </Typography>
              </Paper>

              {item.category === "RawMaterial" && (
                <>
                  <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                    <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}>
                      {t("items.workspace.overview.minSampleQty")}
                    </Typography>
                    <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5 }}>
                      {item.minSampleQuantity != null ? `${item.minSampleQuantity} ${item.minSampleQuantityUnit || ""}`.trim() : "—"}
                    </Typography>
                  </Paper>

                  <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                    <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}>
                      {t("items.workspace.overview.minRetestQty")}
                    </Typography>
                    <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5 }}>
                      {item.minRetestSampleQuantity != null ? `${item.minRetestSampleQuantity} ${item.minSampleQuantityUnit || ""}`.trim() : "—"}
                    </Typography>
                  </Paper>

                  <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                    <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}>
                      {t("items.workspace.overview.minSampleQtyUnit")}
                    </Typography>
                    <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5 }}>
                      {item.minSampleQuantityUnit || "—"}
                    </Typography>
                  </Paper>
                </>
              )}
            </Box>

            <Paper sx={{ p: 2, border: "1px solid", borderColor: "divider" }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                {t("items.workspace.overview.summaryTitle")}
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
                  onClick={() => setActiveTab("tests")}
                  {...clickable(() => setActiveTab("tests"))}
                >
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    {t("items.workspace.overview.assignedTestsLabel")}
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600, color: "primary.main" }}>
                    {t("items.workspace.overview.assignedTestsCount", { count: item.assignedTests?.length || 0 })}
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
                  onClick={() => setActiveTab("specifications")}
                  {...clickable(() => setActiveTab("specifications"))}
                >
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    {t("items.workspace.overview.specificationsLabel")}
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600, color: "primary.main" }}>
                    {t("items.workspace.overview.specificationsCount", { count: specsCount })}
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
                  onClick={() => setActiveTab("documents")}
                  {...clickable(() => setActiveTab("documents"))}
                >
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    {t("items.workspace.overview.controlledDocsLabel")}
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600, color: "primary.main" }}>
                    {t("items.workspace.overview.controlledDocsSummary", {
                      sop: item.sopNumber ? t("items.workspace.sopChip", { sopNumber: item.sopNumber }) : t("items.workspace.overview.noSop"),
                      vr: currentVr ? t("items.workspace.overview.vrAvailable") : t("items.workspace.overview.noVr"),
                    })}
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
                  onClick={() => setActiveTab("preparation")}
                  {...clickable(() => setActiveTab("preparation"))}
                >
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    {t("items.workspace.overview.prepStepsLabel")}
                  </Typography>
                  <Typography
                    variant="body2"
                    sx={{
                      fontWeight: 600,
                      color: prepConfig?.approvalStatus === "PendingReview" ? "warning.main" : "primary.main"
                    }}
                  >
                    {prepConfig
                      ? t("items.workspace.overview.prepConfigured", {
                          status: prepConfig.approvalStatus === "Approved"
                            ? t("items.workspace.overview.prepApproved")
                            : t("items.workspace.overview.prepPending"),
                        })
                      : t("items.workspace.overview.prepNotConfigured")}
                  </Typography>
                </Box>
              </Stack>
            </Paper>
          </Stack>
        )}

        {/* Tab 1: Assigned Tests */}
        {activeTab === "tests" && (
          <ItemAssignedTestsSection key={item.id} item={item} readOnly={!canManage} onSaved={onItemUpdated} />
        )}

        {/* Tab 2: Specifications */}
        {activeTab === "specifications" && (
          <ItemSpecificationsSection
            item={item}
            readOnly={!canManage}
            onSpecsChanged={() => {
              onItemUpdated?.();
            }}
          />
        )}

        {/* Tab 3: Supporting Records */}
        {activeTab === "records" && (
          <Stack spacing={4}>
            <NotApplicableTestsSection
              itemId={item.id}
              assignedTests={item.assignedTests || []}
              grades={grades}
            />
            <Divider />
            <SupplierAttributesSection
              itemId={item.id}
              grades={grades}
            />
          </Stack>
        )}

        {/* Tab 4: Documents & Attachments */}
        {activeTab === "documents" && (
          <Stack spacing={3}>
            {docError && <Alert severity="error">{docError}</Alert>}

            {/* Effective STM from Document Library */}
            <ItemStmPanel itemId={item.id} />

            <Divider />

            {/* Verification Report Section */}
            <Box>
              <Stack
                direction="row"
                sx={{
                  justifyContent: "space-between",
                  alignItems: "center",
                  mb: 1.5
                }}>
                <Typography variant="subtitle2" sx={{ fontWeight: 700, display: "flex", alignItems: "center", gap: 1 }}>
                  <VerifiedIcon fontSize="small" sx={{ color: "success.main" }} />
                  {t("items.workspace.documents.heading")}
                </Typography>
                {canManage && (
                <Button
                  size="small"
                  variant="outlined"
                  startIcon={<UploadFileIcon fontSize="small" />}
                  onClick={openUploadDialog}
                >
                  {t("items.workspace.documents.uploadButton")}
                </Button>
                )}
              </Stack>

              {currentVr ? (
                <DocumentCard doc={currentVr} formatFileSize={formatFileSize} formatDate={formatDate} formatDateTime={formatDateTime} />
              ) : (
                <Paper sx={{ p: 2, textAlign: "center", border: "1px dashed", borderColor: "divider", bgcolor: "action.hover" }}>
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    {t("items.workspace.documents.empty")}
                  </Typography>
                </Paper>
              )}

              {historicalVrs.length > 0 && (
                <Accordion sx={{ mt: 1, border: "1px solid", borderColor: "divider", boxShadow: "none" }}>
                  <AccordionSummary expandIcon={<ExpandMoreIcon fontSize="small" />}>
                    <Typography variant="caption" sx={{ fontWeight: 600, color: "text.secondary" }}>
                      {t("items.workspace.documents.historicalHeading", { count: historicalVrs.length })}
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

        {/* Tab 5: Preparation Configuration */}
        {activeTab === "preparation" && (
          <ItemPreparationConfigurationSection
            itemId={item.id}
            itemName={item.name}
            onChanged={() => setPrepConfigVersion((v) => v + 1)}
          />
        )}

        {/* Tab 6: Audit History */}
        {activeTab === "audit" && (
          <Stack spacing={2} sx={{
            alignItems: "flex-start"
          }}>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              <Trans
                i18nKey="items.workspace.audit.description"
                ns="labConfig"
                values={{ name: item.name, code: item.code }}
                components={{ bold: <strong /> }}
              />
            </Typography>
            <Button
              variant="contained"
              startIcon={<HistoryIcon />}
              onClick={() => setAuditDialogOpen(true)}
            >
              {t("items.workspace.audit.openButton")}
            </Button>
          </Stack>
        )}
      </Box>

      {/* Upload Dialog */}
      <UploadItemDocumentDialog
        open={uploadDialogOpen}
        itemId={item.id}
        itemName={item.name}
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
  const { t } = useTranslation("labConfig");
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
      <Stack
        direction="row"
        spacing={1}
        sx={{
          justifyContent: "space-between",
          alignItems: "flex-start"
        }}>
        <Box>
          <Stack
            direction="row"
            spacing={1}
            sx={{
              alignItems: "center",
              mb: 0.5
            }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.primary" }}>
              {doc.originalFileName}
            </Typography>
            <Chip
              label={doc.version}
              size="small"
              color={isHistorical ? "default" : "primary"}
              sx={{ fontWeight: 700, fontSize: 12, height: 20 }}
            />
            {isHistorical && (
              <Chip label={t("items.workspace.documents.superseded")} size="small" color="warning" sx={{ fontWeight: 600, fontSize: 12, height: 20 }} />
            )}
          </Stack>

          <Typography variant="caption" component="div" sx={{ color: "text.secondary", mt: 0.5 }}>
            <Trans
              i18nKey="items.workspace.documents.effectiveAndSize"
              ns="labConfig"
              values={{ date: formatDate(doc.effectiveDate), size: formatFileSize(doc.fileSizeBytes) }}
              components={{ bold: <strong /> }}
            />
          </Typography>
          <Typography variant="caption" component="div" sx={{ color: "text.secondary", mt: 0.25 }}>
            <Trans
              i18nKey="items.workspace.documents.uploadedBy"
              ns="labConfig"
              values={{ user: doc.uploadedByUserName, dateTime: formatDateTime(doc.uploadedAt) }}
              components={{ bold: <strong /> }}
            />
          </Typography>
        </Box>

        <ItemDocumentActionButtons doc={doc} buttonSx={{ textTransform: "none" }} />
      </Stack>
    </Paper>
  );
}
