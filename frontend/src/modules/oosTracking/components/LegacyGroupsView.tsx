import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  Collapse,
  Divider,
  IconButton,
  LinearProgress,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography
} from "@mui/material";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import FileUploadOutlinedIcon from "@mui/icons-material/FileUploadOutlined";
import DownloadOutlinedIcon from "@mui/icons-material/DownloadOutlined";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import AttachFileIcon from "@mui/icons-material/AttachFile";
import { Link } from "react-router-dom";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { serverMessage } from "../../../utils/errorMessage";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge, CategoryBadge, ToneBadge } from "../../../components/StatusBadge";
import { DialogBodySkeleton } from "../../../components/DialogBodySkeleton";
import { FloatingDialog } from "../../../components/FloatingDialog";
import {
  OosTrackingService,
  type OosGroup,
  type OosInvestigationDocument
} from "../services/OosTrackingService";
import { tableHeadSx } from "../../../theme";
import { monospaceFontFamily } from "../../../theme/palette";
import { FileDropZone } from "../../../components/FileDropZone";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

const INVESTIGATION_ACCEPT = ".pdf,.jpg,.jpeg,.png,.webp,.tiff";

const formatBytes = (bytes: number) => {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
};

export function LegacyGroupsView() {
  const { t } = useTranslation("oosTracking");
  const [groups, setGroups] = useState<OosGroup[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set());
  const [activeDocGroup, setActiveDocGroup] = useState<OosGroup | null>(null);

  const loadGroups = useCallback(() => {
    setError(null);
    OosTrackingService.getOosGroups()
      .then((data) => {
        setGroups(data);
        setExpandedGroups(new Set(data.map((g) => g.oosGroupCode)));
      })
      .catch((e: unknown) => setError(serverMessage(e) ?? t("page.loadError")));
  }, [t]);

  useEffect(() => {
    loadGroups();
  }, [loadGroups]);

  const toggleGroup = (code: string) => {
    setExpandedGroups((prev) => {
      const next = new Set(prev);
      if (next.has(code)) next.delete(code);
      else next.add(code);
      return next;
    });
  };

  const totalRetestCount =
    groups?.reduce((acc, g) => acc + g.retestSamples.length, 0) ?? 0;
  const retestTypeLabels: Record<string, string> = {
    RetestRetainedSample: t("retests.types.RetestRetainedSample"),
    NewSampleRequest: t("retests.types.NewSampleRequest")
  };

  return (
    <Box>
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Box sx={{ display: "flex", alignItems: "baseline", gap: 1.5, mb: 2 }}>
        <SectionTitle>{t("page.sectionTitle")}</SectionTitle>
        {groups && (
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
            {t("summary.groupsAndRetests", {
              groups: t("summary.groupCount", { count: groups.length }),
              retests: t("summary.retestCount", { count: totalRetestCount })
            })}
          </Typography>
        )}
      </Box>

      {!groups && !error ? (
        <DialogBodySkeleton blocks={3} blockHeight={120} />
      ) : groups && groups.length === 0 ? (
        <Paper
          elevation={0}
          sx={{
            p: 6,
            textAlign: "center",
            border: "1px solid",
            borderColor: "divider",
            borderRadius: 1.5,
            bgcolor: "background.paper"
          }}
        >
          <Typography sx={{ color: "text.secondary", fontSize: 14 }}>
            {t("page.noGroups")}
          </Typography>
        </Paper>
      ) : (
        <Stack spacing={2}>
          {groups?.map((group) => {
            const isExpanded = expandedGroups.has(group.oosGroupCode);

            return (
              <Paper
                key={group.oosGroupCode}
                elevation={0}
                sx={{
                  border: "1px solid",
                  borderColor: "divider",
                  borderRadius: 1.5,
                  overflow: "hidden",
                  bgcolor: "background.paper"
                }}
              >
                <Box
                  sx={{
                    p: 2,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    flexWrap: "wrap",
                    gap: 2,
                    bgcolor: "background.default",
                    borderBottom: isExpanded ? "1px solid" : "none",
                    borderColor: "divider",
                    cursor: "pointer"
                  }}
                  onClick={() => toggleGroup(group.oosGroupCode)}
                >
                  <Box
                    sx={{
                      display: "flex",
                      alignItems: "center",
                      gap: 1.5,
                      flexWrap: "wrap"
                    }}
                  >
                    <IconButton
                      aria-label={
                        isExpanded ? t("group.collapse") : t("group.expand")
                      }
                      aria-expanded={Boolean(isExpanded)}
                      size="small"
                      onClick={(e) => {
                        e.stopPropagation();
                        toggleGroup(group.oosGroupCode);
                      }}
                    >
                      {isExpanded ? (
                        <KeyboardArrowUpIcon />
                      ) : (
                        <KeyboardArrowDownIcon />
                      )}
                    </IconButton>

                    <Box>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                        <Typography
                          sx={{
                            fontFamily: monospaceFontFamily,
                            fontWeight: 700,
                            fontSize: 15,
                            color: "primary.main"
                          }}
                        >
                          {group.oosGroupCode}
                        </Typography>
                        <CategoryBadge category={group.category} />
                      </Box>
                      <Typography
                        sx={{ fontSize: 12, color: "text.secondary", mt: 0.25 }}
                      >
                        <Trans
                          i18nKey="group.opened"
                          ns="oosTracking"
                          values={{
                            date: formatDateTimeWithTimeZone(group.openedAt)
                          }}
                          components={{ ltr: <Ltr>{null}</Ltr> }}
                        />
                      </Typography>
                    </Box>

                    <Divider
                      orientation="vertical"
                      flexItem
                      sx={{ mx: 0.5, height: 28, my: "auto" }}
                    />

                    <Box>
                      <Typography
                        sx={{
                          fontSize: 12,
                          color: "text.secondary",
                          textTransform: "uppercase",
                          fontWeight: 600
                        }}
                      >
                        {t("group.rootOrigin")}
                      </Typography>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                        <Typography
                          sx={{
                            fontSize: 13,
                            fontWeight: 700,
                            fontFamily: monospaceFontFamily
                          }}
                        >
                          {group.originReferenceNumber}
                        </Typography>
                        <StatusBadge status={group.originSampleStatus} />
                      </Box>
                    </Box>

                    <Box sx={{ ml: 1 }}>
                      <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                        {group.displayName}
                      </Typography>
                      {group.batchNumber && (
                        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                          <Trans
                            i18nKey="group.batch"
                            ns="oosTracking"
                            values={{ batch: group.batchNumber }}
                            components={{ ltr: <Ltr>{null}</Ltr> }}
                          />
                        </Typography>
                      )}
                    </Box>
                  </Box>

                  <Box
                    sx={{ display: "flex", alignItems: "center", gap: 1.5 }}
                    onClick={(e) => e.stopPropagation()}
                  >
                    {group.hasInvestigationDocument ? (
                      <Chip
                        icon={
                          <AttachFileIcon sx={{ fontSize: "16px !important" }} />
                        }
                        label={t("group.reportAttached")}
                        color="success"
                        variant="outlined"
                        size="small"
                        sx={{ fontWeight: 600, fontSize: 12 }}
                      />
                    ) : (
                      <Chip
                        label={t("group.noReport")}
                        variant="outlined"
                        size="small"
                        sx={{
                          color: "text.secondary",
                          borderColor: "divider",
                          fontSize: 12
                        }}
                      />
                    )}

                    <Button
                      size="small"
                      variant="outlined"
                      startIcon={<DescriptionOutlinedIcon fontSize="small" />}
                      onClick={() => setActiveDocGroup(group)}
                      sx={{ fontSize: 12, textTransform: "none", fontWeight: 600 }}
                    >
                      {group.hasInvestigationDocument
                        ? t("group.manageInvestigation")
                        : t("group.uploadInvestigation")}
                    </Button>
                  </Box>
                </Box>

                <Collapse in={isExpanded} timeout="auto" unmountOnExit>
                  <TableContainer>
                    <Table size="small">
                      <TableHead sx={{ bgcolor: "background.paper" }}>
                        <TableRow>
                          {[
                            t("retests.columns.retestSample"),
                            t("retests.columns.parentOrigin"),
                            t("retests.columns.itemPoint"),
                            t("retests.columns.retestType"),
                            t("retests.columns.tests"),
                            t("retests.columns.analysts"),
                            t("retests.columns.retestStatus"),
                            t("retests.columns.opened"),
                            t("retests.columns.actions")
                          ].map((h) => (
                            <TableCell
                              key={h}
                              sx={{
                                fontWeight: 700,
                                fontSize: 12,
                                color: "text.secondary"
                              }}
                            >
                              {h}
                            </TableCell>
                          ))}
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {group.retestSamples.length === 0 ? (
                          <TableRow>
                            <TableCell colSpan={9} align="center" sx={{ py: 3 }}>
                              <Typography
                                sx={{ color: "text.secondary", fontSize: 13 }}
                              >
                                {t("retests.noRetests")}
                              </Typography>
                            </TableCell>
                          </TableRow>
                        ) : (
                          group.retestSamples.map((retest) => (
                            <TableRow
                              key={retest.newSampleId}
                              hover
                              sx={{
                                "&:last-child td, &:last-child th": { border: 0 }
                              }}
                            >
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography
                                  sx={{
                                    fontSize: 13,
                                    fontWeight: 700,
                                    fontFamily: monospaceFontFamily
                                  }}
                                >
                                  {retest.newReferenceNumber}
                                </Typography>
                              </TableCell>
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography
                                  sx={{
                                    fontSize: 12,
                                    fontFamily: monospaceFontFamily,
                                    fontWeight: 600
                                  }}
                                >
                                  {retest.originReferenceNumber}
                                </Typography>
                                <StatusBadge status={retest.originSampleStatus} />
                              </TableCell>
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography
                                  sx={{ fontSize: 12, color: "text.primary" }}
                                >
                                  {retest.displayName || "—"}
                                </Typography>
                                {retest.batchNumber && (
                                  <Typography
                                    sx={{ fontSize: 12, color: "text.secondary" }}
                                  >
                                    <Trans
                                      i18nKey="retests.batch"
                                      ns="oosTracking"
                                      values={{ batch: retest.batchNumber }}
                                      components={{ ltr: <Ltr>{null}</Ltr> }}
                                    />
                                  </Typography>
                                )}
                              </TableCell>
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12 }}>
                                  {retestTypeLabels[retest.retestType] ??
                                    retest.retestType}
                                </Typography>
                              </TableCell>
                              <TableCell sx={{ py: 1.25 }}>
                                {retest.testCodes.map((code) => (
                                  <Chip
                                    key={code}
                                    size="small"
                                    label={code}
                                    sx={{
                                      mr: 0.5,
                                      mb: 0.5,
                                      height: 20,
                                      fontSize: 12
                                    }}
                                  />
                                ))}
                              </TableCell>
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12 }}>
                                  {retest.analystNames.length > 0
                                    ? retest.analystNames.join(", ")
                                    : "—"}
                                </Typography>
                              </TableCell>
                              <TableCell sx={{ py: 1.25 }}>
                                <StatusBadge status={retest.newSampleStatus} />
                              </TableCell>
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography
                                  sx={{ fontSize: 12, color: "text.secondary" }}
                                >
                                  {formatDateTimeWithTimeZone(retest.openedAt)}
                                </Typography>
                              </TableCell>
                              <TableCell sx={{ py: 1.25 }}>
                                <Tooltip title={t("retests.recordTooltip")}>
                                  <Button
                                    component={Link}
                                    to={`/samples/${retest.newSampleId}/report`}
                                    target="_blank"
                                    rel="noopener"
                                    size="small"
                                    variant="outlined"
                                    startIcon={
                                      <DescriptionOutlinedIcon fontSize="small" />
                                    }
                                    sx={{
                                      px: 1,
                                      py: 0.25,
                                      fontSize: 12,
                                      borderColor: "divider",
                                      color: "text.secondary"
                                    }}
                                  >
                                    {t("retests.record")}
                                  </Button>
                                </Tooltip>
                              </TableCell>
                            </TableRow>
                          ))
                        )}
                      </TableBody>
                    </Table>
                  </TableContainer>
                </Collapse>
              </Paper>
            );
          })}
        </Stack>
      )}

      {activeDocGroup && (
        <OosInvestigationDocumentsDialog
          open={Boolean(activeDocGroup)}
          group={activeDocGroup}
          onClose={() => setActiveDocGroup(null)}
          onChanged={loadGroups}
        />
      )}
    </Box>
  );
}

interface DocDialogProps {
  open: boolean;
  group: OosGroup;
  onClose: () => void;
  onChanged: () => void;
}

function OosInvestigationDocumentsDialog({
  open,
  group,
  onClose,
  onChanged
}: DocDialogProps) {
  const { t } = useTranslation("oosTracking");
  const formatsHint = t("docs.formatsHint");
  const [docs, setDocs] = useState<OosInvestigationDocument[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [supersedingDoc, setSupersedingDoc] =
    useState<OosInvestigationDocument | null>(null);
  const [supersedeFile, setSupersedeFile] = useState<File | null>(null);
  const [supersedeReason, setSupersedeReason] = useState("");

  const loadDocuments = useCallback(() => {
    setLoading(true);
    setError(null);
    OosTrackingService.getDocuments(group.oosGroupCode)
      .then((res) => {
        setDocs(res);
        setLoading(false);
      })
      .catch((e: unknown) => {
        setError(serverMessage(e) ?? t("errors.loadDocuments"));
        setLoading(false);
      });
  }, [group.oosGroupCode, t]);

  useEffect(() => {
    if (open) loadDocuments();
  }, [open, loadDocuments]);

  const handleUpload = async () => {
    if (!selectedFile) return;
    setSubmitting(true);
    setError(null);
    try {
      await OosTrackingService.uploadDocument(group.oosGroupCode, selectedFile);
      setSelectedFile(null);
      loadDocuments();
      onChanged();
    } catch (e: unknown) {
      setError(serverMessage(e) ?? t("errors.upload"));
    } finally {
      setSubmitting(false);
    }
  };

  const handleDownload = async (doc: OosInvestigationDocument) => {
    try {
      const blob = await OosTrackingService.downloadDocument(
        doc.id,
        group.oosGroupCode
      );
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = doc.originalFileName;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (e: unknown) {
      setError(serverMessage(e) ?? t("errors.download"));
    }
  };

  const handleSupersede = async () => {
    if (!supersedingDoc || !supersedeFile || !supersedeReason.trim()) return;
    setSubmitting(true);
    setError(null);
    try {
      await OosTrackingService.supersedeDocument(
        supersedingDoc.id,
        group.oosGroupCode,
        supersedeFile,
        supersedeReason.trim()
      );
      setSupersedingDoc(null);
      setSupersedeFile(null);
      setSupersedeReason("");
      loadDocuments();
      onChanged();
    } catch (e: unknown) {
      setError(serverMessage(e) ?? t("errors.supersede"));
    } finally {
      setSubmitting(false);
    }
  };

  const currentDoc = docs?.find((d) => d.status === "Current" || d.status === 0);

  return (
    <FloatingDialog
      open={open}
      title={t("docs.title", { code: group.oosGroupCode })}
      onClose={onClose}
      actions={
        <Button onClick={onClose} disabled={submitting}>
          {t("docs.close")}
        </Button>
      }
    >
      {submitting && <LinearProgress sx={{ mb: 2 }} />}
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Box sx={{ mb: 2.5 }}>
        <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
          <Trans
            i18nKey="docs.intro"
            ns="oosTracking"
            values={{
              code: group.oosGroupCode,
              reference: group.originReferenceNumber
            }}
            components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
          />
        </Typography>
      </Box>

      {!currentDoc && !loading && (
        <Box sx={{ mb: 3 }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1 }}>
            {t("docs.uploadInitialTitle")}
          </Typography>
          <FileDropZone
            file={selectedFile}
            onFile={setSelectedFile}
            accept={INVESTIGATION_ACCEPT}
            formatsHint={formatsHint}
            note={t("docs.note")}
            disabled={submitting}
          />
          {selectedFile && (
            <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 1.5 }}>
              <Button
                variant="contained"
                size="small"
                startIcon={<FileUploadOutlinedIcon />}
                onClick={handleUpload}
                disabled={submitting}
              >
                {t("docs.upload")}
              </Button>
            </Box>
          )}
        </Box>
      )}

      {supersedingDoc && (
        <Paper
          elevation={0}
          sx={{
            p: 2,
            mb: 3,
            border: "1px solid",
            borderColor: "warning.main",
            borderRadius: 1.5,
            bgcolor: "background.paper"
          }}
        >
          <Typography
            sx={{ fontSize: 13, fontWeight: 700, color: "warning.dark", mb: 1 }}
          >
            {t("supersede.title", { file: supersedingDoc.originalFileName })}
          </Typography>
          <Alert severity="info" sx={{ mb: 1.5, fontSize: 12 }}>
            <Trans
              i18nKey="supersede.info"
              ns="oosTracking"
              components={{ strong: <strong /> }}
            />
          </Alert>
          <TextField
            label={t("supersede.reasonLabel")}
            size="small"
            fullWidth
            multiline
            rows={2}
            value={supersedeReason}
            onChange={(e) => setSupersedeReason(e.target.value)}
            sx={{ mb: 1.5 }}
          />
          <FileDropZone
            file={supersedeFile}
            onFile={setSupersedeFile}
            accept={INVESTIGATION_ACCEPT}
            formatsHint={formatsHint}
            note={t("docs.note")}
            disabled={submitting}
          />
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mt: 1.5 }}>
            <Box sx={{ ml: "auto", display: "flex", gap: 1 }}>
              <Button
                size="small"
                onClick={() => setSupersedingDoc(null)}
                disabled={submitting}
              >
                {t("supersede.cancel")}
              </Button>
              <Button
                variant="contained"
                color="warning"
                size="small"
                disabled={
                  !supersedeFile || !supersedeReason.trim() || submitting
                }
                onClick={handleSupersede}
              >
                {t("supersede.action")}
              </Button>
            </Box>
          </Box>
        </Paper>
      )}

      <TableContainer
        sx={{
          border: "1px solid",
          borderColor: "divider",
          borderRadius: 1
        }}
      >
        <Table size="small">
          <TableHead sx={tableHeadSx}>
            <TableRow>
              {[
                t("docs.columns.status"),
                t("docs.columns.filename"),
                t("docs.columns.size"),
                t("docs.columns.uploadedBy"),
                t("docs.columns.date"),
                t("docs.columns.actions")
              ].map((h) => (
                <TableCell
                  key={h}
                  sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary" }}
                >
                  {h}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={6} sx={{ py: 2 }}>
                  <DialogBodySkeleton blocks={2} blockHeight={24} />
                </TableCell>
              </TableRow>
            ) : docs && docs.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} align="center" sx={{ py: 4 }}>
                  <Typography sx={{ color: "text.secondary", fontSize: 13 }}>
                    {t("docs.empty")}
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              docs?.map((doc) => {
                const isCurrent = doc.status === "Current" || doc.status === 0;
                const isSuperseded =
                  doc.status === "Superseded" || doc.status === 1;

                return (
                  <TableRow
                    key={doc.id}
                    hover
                    sx={{ "&:last-child td, &:last-child th": { border: 0 } }}
                  >
                    <TableCell>
                      {isCurrent && (
                        <ToneBadge
                          tone="notDetected"
                          label={t("docs.statuses.current")}
                        />
                      )}
                      {isSuperseded && (
                        <ToneBadge
                          tone="pending"
                          label={t("docs.statuses.superseded")}
                        />
                      )}
                      {!isCurrent && !isSuperseded && (
                        <ToneBadge
                          tone="detected"
                          label={t("docs.statuses.voided")}
                        />
                      )}
                    </TableCell>
                    <TableCell>
                      <Typography sx={{ fontSize: 12, fontWeight: 600 }}>
                        {doc.originalFileName}
                      </Typography>
                      {doc.supersessionReason && (
                        <Typography
                          sx={{ fontSize: 12, color: "text.secondary" }}
                        >
                          {t("docs.supersededReason", {
                            reason: doc.supersessionReason
                          })}
                        </Typography>
                      )}
                      {doc.voidReason && (
                        <Typography sx={{ fontSize: 12, color: "error.main" }}>
                          {t("docs.voidedReason", { reason: doc.voidReason })}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                      {formatBytes(doc.fileSizeBytes)}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>
                      {doc.uploadedByName}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                      {formatDateTimeWithTimeZone(doc.uploadedAt)}
                    </TableCell>
                    <TableCell>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                        <Tooltip title={t("docs.download")}>
                          <IconButton
                            size="small"
                            aria-label={t("docs.download")}
                            onClick={() => handleDownload(doc)}
                          >
                            <DownloadOutlinedIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                        {isCurrent && !supersedingDoc && (
                          <Tooltip title={t("docs.supersedeTooltip")}>
                            <IconButton
                              size="small"
                              color="warning"
                              aria-label={t("docs.supersedeTooltip")}
                              onClick={() => setSupersedingDoc(doc)}
                            >
                              <SwapHorizIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                      </Box>
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </FloatingDialog>
  );
}
