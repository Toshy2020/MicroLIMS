import React, { useEffect, useState, useRef } from "react";
import {
  Box,
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TableContainer,
  Typography,
  Chip,
  Alert,
  Tooltip,
  Button,
  IconButton,
  Collapse,
  Stack,
  TextField,
  LinearProgress,
  Divider,
  useTheme
} from "@mui/material";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import FileUploadOutlinedIcon from "@mui/icons-material/FileUploadOutlined";
import DownloadOutlinedIcon from "@mui/icons-material/DownloadOutlined";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import BlockIcon from "@mui/icons-material/Block";
import AttachFileIcon from "@mui/icons-material/AttachFile";
import { Link } from "react-router-dom";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { StatusBadge, CategoryBadge } from "../../components/StatusBadge";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { FloatingDialog } from "../../components/FloatingDialog";
import {
  OosTrackingService,
  OosGroup,
  OosInvestigationDocument
} from "./services/OosTrackingService";

const RETEST_TYPE_LABELS: Record<string, string> = {
  RetestRetainedSample: "Retest Retained Sample",
  NewSampleRequest: "New Sample Request"
};

const formatDate = (d: string) =>
  new Date(d).toLocaleString("en-GB", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });

const formatBytes = (bytes: number) => {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
};

export function OosTrackingPage() {
  const theme = useTheme();
  const [groups, setGroups] = useState<OosGroup[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set());
  const [activeDocGroup, setActiveDocGroup] = useState<OosGroup | null>(null);

  const loadGroups = () => {
    OosTrackingService.getOosGroups()
      .then((data) => {
        setGroups(data);
        // Expand all groups by default
        setExpandedGroups(new Set(data.map((g) => g.oosGroupCode)));
      })
      .catch((e) =>
        setError(e?.response?.data?.message ?? "Failed to load OOS tracking items.")
      );
  };

  useEffect(() => {
    loadGroups();
  }, []);

  const toggleGroup = (code: string) => {
    setExpandedGroups((prev) => {
      const next = new Set(prev);
      if (next.has(code)) {
        next.delete(code);
      } else {
        next.add(code);
      }
      return next;
    });
  };

  const totalRetestCount = groups?.reduce((acc, g) => acc + g.retestSamples.length, 0) ?? 0;

  return (
    <Box>
      <PageHeader
        title="Out-of-Specification Tracking"
        subtitle="Every out-of-specification investigation chain grouped by OOS number, tracing all retest spin-offs back to the original sample."
      />

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Box sx={{ display: "flex", alignItems: "baseline", gap: 1.5, mb: 2 }}>
        <SectionTitle>OOS Investigation Groups</SectionTitle>
        {groups && (
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
            {groups.length === 1 ? "1 OOS Group" : `${groups.length} OOS Groups`} ({totalRetestCount} retest sample{totalRetestCount === 1 ? "" : "s"})
          </Typography>
        )}
      </Box>

      {!groups && !error ? (
        <Box sx={{ py: 8, display: "flex", justifyContent: "center" }}>
          <LoadingSpinner />
        </Box>
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
            No active OOS investigation chains found.
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
                {/* Group Summary Header */}
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
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap" }}>
                    <IconButton size="small" onClick={(e) => { e.stopPropagation(); toggleGroup(group.oosGroupCode); }}>
                      {isExpanded ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
                    </IconButton>

                    <Box>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                        <Typography
                          sx={{
                            fontFamily: "monospace",
                            fontWeight: 700,
                            fontSize: 15,
                            color: "primary.main"
                          }}
                        >
                          {group.oosGroupCode}
                        </Typography>
                        <CategoryBadge category={group.category} />
                      </Box>
                      <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.25 }}>
                        Opened: {formatDate(group.openedAt)}
                      </Typography>
                    </Box>

                    <Divider orientation="vertical" flexItem sx={{ mx: 0.5, height: 28, my: "auto" }} />

                    <Box>
                      <Typography sx={{ fontSize: 11, color: "text.secondary", textTransform: "uppercase", fontWeight: 600 }}>
                        Root / Origin Sample
                      </Typography>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                        <Typography sx={{ fontSize: 13, fontWeight: 700, fontFamily: "monospace" }}>
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
                        <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                          Batch: {group.batchNumber}
                        </Typography>
                      )}
                    </Box>
                  </Box>

                  {/* Investigation Document & Count Actions */}
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }} onClick={(e) => e.stopPropagation()}>
                    {group.hasInvestigationDocument ? (
                      <Chip
                        icon={<AttachFileIcon sx={{ fontSize: "16px !important" }} />}
                        label="Lab Investigation Report Attached"
                        color="success"
                        variant="outlined"
                        size="small"
                        sx={{ fontWeight: 600, fontSize: 11 }}
                      />
                    ) : (
                      <Chip
                        label="No Report Attached"
                        variant="outlined"
                        size="small"
                        sx={{ color: "text.secondary", borderColor: "divider", fontSize: 11 }}
                      />
                    )}

                    <Button
                      size="small"
                      variant="outlined"
                      startIcon={<DescriptionOutlinedIcon fontSize="small" />}
                      onClick={() => setActiveDocGroup(group)}
                      sx={{ fontSize: 12, textTransform: "none", fontWeight: 600 }}
                    >
                      {group.hasInvestigationDocument ? "Manage Investigation" : "Upload Investigation"}
                    </Button>
                  </Box>
                </Box>

                {/* Retest Samples Table */}
                <Collapse in={isExpanded} timeout="auto" unmountOnExit>
                  <TableContainer>
                    <Table size="small">
                      <TableHead sx={{ bgcolor: "background.paper" }}>
                        <TableRow>
                          {["Retest Sample", "Parent / Origin", "Item / Point", "Retest Type", "Test(s)", "Analyst(s)", "Retest Status", "Opened", "Actions"].map((h) => (
                            <TableCell key={h} sx={{ fontWeight: 700, fontSize: 11, color: "text.secondary" }}>
                              {h}
                            </TableCell>
                          ))}
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {group.retestSamples.length === 0 ? (
                          <TableRow>
                            <TableCell colSpan={9} align="center" sx={{ py: 3 }}>
                              <Typography sx={{ color: "text.secondary", fontSize: 13 }}>
                                No retest samples have been spun off yet for this OOS chain.
                              </Typography>
                            </TableCell>
                          </TableRow>
                        ) : (
                          group.retestSamples.map((retest) => (
                            <TableRow key={retest.newSampleId} hover sx={{ "&:last-child td, &:last-child th": { border: 0 } }}>
                              {/* Retest Sample */}
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 13, fontWeight: 700, fontFamily: "monospace" }}>
                                  {retest.newReferenceNumber}
                                </Typography>
                              </TableCell>

                              {/* Parent / Origin */}
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12, fontFamily: "monospace", fontWeight: 600 }}>
                                  {retest.originReferenceNumber}
                                </Typography>
                                <StatusBadge status={retest.originSampleStatus} />
                              </TableCell>

                              {/* Item / Point */}
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12, color: "text.primary" }}>
                                  {retest.displayName || "—"}
                                </Typography>
                                {retest.batchNumber && (
                                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                                    Batch: {retest.batchNumber}
                                  </Typography>
                                )}
                              </TableCell>

                              {/* Retest Type */}
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12 }}>
                                  {RETEST_TYPE_LABELS[retest.retestType] ?? retest.retestType}
                                </Typography>
                              </TableCell>

                              {/* Test(s) */}
                              <TableCell sx={{ py: 1.25 }}>
                                {retest.testCodes.map((code) => (
                                  <Chip key={code} size="small" label={code} sx={{ mr: 0.5, mb: 0.5, height: 20, fontSize: 11 }} />
                                ))}
                              </TableCell>

                              {/* Analyst(s) */}
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12 }}>
                                  {retest.analystNames.length > 0 ? retest.analystNames.join(", ") : "—"}
                                </Typography>
                              </TableCell>

                              {/* Retest Status */}
                              <TableCell sx={{ py: 1.25 }}>
                                <StatusBadge status={retest.newSampleStatus} />
                              </TableCell>

                              {/* Opened */}
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                                  {formatDate(retest.openedAt)}
                                </Typography>
                              </TableCell>

                              {/* Actions */}
                              <TableCell sx={{ py: 1.25 }}>
                                <Tooltip title="Open the retest sample's printable report">
                                  <Button
                                    component={Link}
                                    to={`/samples/${retest.newSampleId}/report`}
                                    target="_blank"
                                    rel="noopener"
                                    size="small"
                                    variant="outlined"
                                    startIcon={<DescriptionOutlinedIcon fontSize="small" />}
                                    sx={{ px: 1, py: 0.25, fontSize: 11, borderColor: "divider", color: "text.secondary" }}
                                  >
                                    Record
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

      {/* Investigation Documents Dialog */}
      {activeDocGroup && (
        <OosInvestigationDocumentsDialog
          open={Boolean(activeDocGroup)}
          group={activeDocGroup}
          onClose={() => setActiveDocGroup(null)}
          onChanged={() => {
            loadGroups();
          }}
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

function OosInvestigationDocumentsDialog({ open, group, onClose, onChanged }: DocDialogProps) {
  const [docs, setDocs] = useState<OosInvestigationDocument[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Upload state
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);

  // Supersede state
  const [supersedingDoc, setSupersedingDoc] = useState<OosInvestigationDocument | null>(null);
  const supersedeFileRef = useRef<HTMLInputElement>(null);
  const [supersedeFile, setSupersedeFile] = useState<File | null>(null);
  const [supersedeReason, setSupersedeReason] = useState("");

  const loadDocuments = () => {
    setLoading(true);
    setError(null);
    OosTrackingService.getDocuments(group.oosGroupCode)
      .then((res) => {
        setDocs(res);
        setLoading(false);
      })
      .catch((e) => {
        setError(e?.response?.data?.message ?? "Failed to load investigation documents.");
        setLoading(false);
      });
  };

  useEffect(() => {
    if (open) {
      loadDocuments();
    }
  }, [open, group.oosGroupCode]);

  const handleUpload = async () => {
    if (!selectedFile) return;
    setSubmitting(true);
    setError(null);
    try {
      await OosTrackingService.uploadDocument(group.oosGroupCode, selectedFile);
      setSelectedFile(null);
      if (fileInputRef.current) fileInputRef.current.value = "";
      loadDocuments();
      onChanged();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Failed to upload document.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleDownload = async (doc: OosInvestigationDocument) => {
    try {
      const blob = await OosTrackingService.downloadDocument(doc.id, group.oosGroupCode);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = doc.originalFileName;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Failed to download document content.");
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
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Failed to supersede document.");
    } finally {
      setSubmitting(false);
    }
  };

  const currentDoc = docs?.find((d) => d.status === "Current" || d.status === 0);

  return (
    <FloatingDialog
      open={open}
      title={`Lab Investigation Documents — ${group.oosGroupCode}`}
      onClose={onClose}
      actions={
        <Button onClick={onClose} disabled={submitting}>
          Close
        </Button>
      }
    >
      {submitting && <LinearProgress sx={{ mb: 2 }} />}
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Box sx={{ mb: 2.5 }}>
        <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
          Controlled lab investigation reports attached to OOS group <strong>{group.oosGroupCode}</strong> (Origin Sample: <strong>{group.originReferenceNumber}</strong>).
        </Typography>
      </Box>

      {/* Upload new document section if no Current document exists */}
      {!currentDoc && !loading && (
        <Paper elevation={0} sx={{ p: 2, mb: 3, border: "1px dashed", borderColor: "primary.main", borderRadius: 1.5, bgcolor: "action.hover" }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1 }}>
            Upload Initial Investigation Report
          </Typography>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
            <input
              ref={fileInputRef}
              type="file"
              accept=".pdf,.jpg,.jpeg,.png,.webp,.tiff"
              style={{ display: "none" }}
              onChange={(e) => setSelectedFile(e.target.files?.[0] ?? null)}
            />
            <Button
              variant="outlined"
              size="small"
              startIcon={<FileUploadOutlinedIcon />}
              onClick={() => fileInputRef.current?.click()}
            >
              Select File
            </Button>
            {selectedFile && (
              <Typography sx={{ fontSize: 12, fontWeight: 600 }}>
                {selectedFile.name} ({formatBytes(selectedFile.size)})
              </Typography>
            )}
            {selectedFile && (
              <Button
                variant="contained"
                size="small"
                onClick={handleUpload}
                disabled={submitting}
              >
                Upload
              </Button>
            )}
          </Box>
        </Paper>
      )}

      {/* Supersede Panel */}
      {supersedingDoc && (
        <Paper elevation={0} sx={{ p: 2, mb: 3, border: "1px solid", borderColor: "warning.main", borderRadius: 1.5, bgcolor: "background.paper" }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "warning.dark", mb: 1 }}>
            Supersede Document ({supersedingDoc.originalFileName})
          </Typography>
          <Alert severity="info" sx={{ mb: 1.5, fontSize: 12 }}>
            The current document will be marked <strong>Superseded</strong>. A new active version will take its place.
          </Alert>

          <TextField
            label="Reason for Supersession *"
            size="small"
            fullWidth
            multiline
            rows={2}
            value={supersedeReason}
            onChange={(e) => setSupersedeReason(e.target.value)}
            sx={{ mb: 1.5 }}
          />

          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
            <input
              ref={supersedeFileRef}
              type="file"
              accept=".pdf,.jpg,.jpeg,.png,.webp,.tiff"
              style={{ display: "none" }}
              onChange={(e) => setSupersedeFile(e.target.files?.[0] ?? null)}
            />
            <Button
              variant="outlined"
              size="small"
              startIcon={<FileUploadOutlinedIcon />}
              onClick={() => supersedeFileRef.current?.click()}
            >
              Choose Replacement File
            </Button>
            {supersedeFile && (
              <Typography sx={{ fontSize: 12, fontWeight: 600 }}>
                {supersedeFile.name} ({formatBytes(supersedeFile.size)})
              </Typography>
            )}
            <Box sx={{ ml: "auto", display: "flex", gap: 1 }}>
              <Button size="small" onClick={() => setSupersedingDoc(null)} disabled={submitting}>
                Cancel
              </Button>
              <Button
                variant="contained"
                color="warning"
                size="small"
                disabled={!supersedeFile || !supersedeReason.trim() || submitting}
                onClick={handleSupersede}
              >
                Supersede
              </Button>
            </Box>
          </Box>
        </Paper>
      )}

      {/* Documents Table */}
      <TableContainer sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
        <Table size="small">
          <TableHead sx={{ bgcolor: "background.default" }}>
            <TableRow>
              {["Status", "Filename", "Size", "Uploaded By", "Date", "Actions"].map((h) => (
                <TableCell key={h} sx={{ fontWeight: 700, fontSize: 11, color: "text.secondary" }}>
                  {h}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={6} align="center" sx={{ py: 4 }}>
                  <LoadingSpinner />
                </TableCell>
              </TableRow>
            ) : docs && docs.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} align="center" sx={{ py: 4 }}>
                  <Typography sx={{ color: "text.secondary", fontSize: 13 }}>
                    No investigation documents uploaded for this OOS group yet.
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              docs?.map((doc) => {
                const isCurrent = doc.status === "Current" || doc.status === 0;
                const isSuperseded = doc.status === "Superseded" || doc.status === 1;

                return (
                  <TableRow key={doc.id} hover sx={{ "&:last-child td, &:last-child th": { border: 0 } }}>
                    <TableCell>
                      {isCurrent && (
                        <Chip size="small" label="Current" color="success" sx={{ height: 20, fontSize: 10, fontWeight: 700 }} />
                      )}
                      {isSuperseded && (
                        <Chip size="small" label="Superseded" sx={{ height: 20, fontSize: 10, bgcolor: "action.selected" }} />
                      )}
                      {(!isCurrent && !isSuperseded) && (
                        <Chip size="small" label="Voided" color="error" variant="outlined" sx={{ height: 20, fontSize: 10 }} />
                      )}
                    </TableCell>
                    <TableCell>
                      <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{doc.originalFileName}</Typography>
                      {doc.supersessionReason && (
                        <Typography sx={{ fontSize: 10.5, color: "text.secondary" }}>
                          Superseded: {doc.supersessionReason}
                        </Typography>
                      )}
                      {doc.voidReason && (
                        <Typography sx={{ fontSize: 10.5, color: "error.main" }}>
                          Voided: {doc.voidReason}
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
                      {formatDate(doc.uploadedAt)}
                    </TableCell>
                    <TableCell>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                        <Tooltip title="Download file">
                          <IconButton size="small" onClick={() => handleDownload(doc)}>
                            <DownloadOutlinedIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                        {isCurrent && !supersedingDoc && (
                          <Tooltip title="Supersede with replacement report">
                            <IconButton size="small" color="warning" onClick={() => setSupersedingDoc(doc)}>
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

