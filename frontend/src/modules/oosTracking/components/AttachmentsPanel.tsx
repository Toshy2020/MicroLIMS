import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  LinearProgress,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import { useTranslation } from "../../../i18n";
import { FileDropZone } from "../../../components/FileDropZone";
import { FormDialog } from "../../../components/lab/FormDialog";
import { ToneBadge } from "../../../components/StatusBadge";
import { tableHeadSx } from "../../../theme";
import { getErrorMessage } from "../../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { PanelFrame } from "../../quality/components/PanelFrame";
import { formatFileSize, saveBlob } from "../../quality/saveBlob";
import { OosTrackingService, type OosInvestigationDocument } from "../services/OosTrackingService";

// The server's allowlist (MaterialDocumentFileValidator) decides what is accepted, by extension and by file
// signature; this only lets the browser's file picker offer the same types.
const ACCEPT = ".pdf,.jpg,.jpeg,.png,.webp,.tiff";
const REASON_MAX = 4000;

// The API sends the status as a name; a number is the enum's position (Current, Superseded, Voided).
type DocumentState = "Current" | "Superseded" | "Voided";
function stateOf(doc: OosInvestigationDocument): DocumentState {
  if (doc.status === "Current" || doc.status === 0) return "Current";
  if (doc.status === "Superseded" || doc.status === 1) return "Superseded";
  return "Voided";
}

const STATE_TONE = { Current: "notDetected", Superseded: "pending", Voided: "detected" } as const;

interface AttachmentsPanelProps {
  // The investigation number: the OOS group code the documents are filed under.
  oosGroupCode: string;
  // Oos.Manage: upload, supersede and void. Without it the documents can still be read and downloaded.
  // The server enforces the permission and the section scope either way.
  canManage: boolean;
}

// Attachments and evidence of one OOS investigation (spec 5.4, 5.10), kept as controlled documents: a
// replaced document stays listed as Superseded and a voided one keeps its file and the reason.
export function AttachmentsPanel({ oosGroupCode, canManage }: AttachmentsPanelProps) {
  const { t } = useTranslation("oosTracking");

  const [docs, setDocs] = useState<OosInvestigationDocument[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [panelError, setPanelError] = useState<string | null>(null);

  const [supersedeTarget, setSupersedeTarget] = useState<OosInvestigationDocument | null>(null);
  const [supersedeFile, setSupersedeFile] = useState<File | null>(null);
  const [supersedeReason, setSupersedeReason] = useState("");
  const [superseding, setSuperseding] = useState(false);
  const [supersedeError, setSupersedeError] = useState<string | null>(null);

  const [voidTarget, setVoidTarget] = useState<OosInvestigationDocument | null>(null);
  const [voidReason, setVoidReason] = useState("");
  const [voiding, setVoiding] = useState(false);
  const [voidError, setVoidError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setLoadError(null);
    OosTrackingService.getDocuments(oosGroupCode, controller.signal)
      .then((res) => {
        setDocs(res);
        setLoading(false);
      })
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        setLoadError(getErrorMessage(e, t("attachments.loadFailed")));
        setLoading(false);
      });
    return () => controller.abort();
  }, [oosGroupCode, attempt, t]);

  const reload = useCallback(() => setAttempt((n) => n + 1), []);

  const upload = async () => {
    if (!file) return;
    setUploading(true);
    setPanelError(null);
    try {
      await OosTrackingService.uploadDocument(oosGroupCode, file);
      setFile(null);
      reload();
    } catch (e: unknown) {
      setPanelError(getErrorMessage(e, t("attachments.uploadFailed")));
    } finally {
      setUploading(false);
    }
  };

  const download = async (doc: OosInvestigationDocument) => {
    setPanelError(null);
    try {
      saveBlob(await OosTrackingService.downloadDocument(doc.id, oosGroupCode), doc.originalFileName);
    } catch (e: unknown) {
      setPanelError(getErrorMessage(e, t("attachments.downloadFailed")));
    }
  };

  const closeSupersede = () => {
    setSupersedeTarget(null);
    setSupersedeFile(null);
    setSupersedeReason("");
    setSupersedeError(null);
  };

  const submitSupersede = async () => {
    if (!supersedeTarget || !supersedeFile) return;
    setSuperseding(true);
    setSupersedeError(null);
    try {
      await OosTrackingService.supersedeDocument(supersedeTarget.id, oosGroupCode, supersedeFile, supersedeReason.trim());
      closeSupersede();
      reload();
    } catch (e: unknown) {
      setSupersedeError(getErrorMessage(e, t("attachments.supersedeFailed")));
    } finally {
      setSuperseding(false);
    }
  };

  const closeVoid = () => {
    setVoidTarget(null);
    setVoidReason("");
    setVoidError(null);
  };

  const submitVoid = async () => {
    if (!voidTarget) return;
    setVoiding(true);
    setVoidError(null);
    try {
      await OosTrackingService.voidDocument(voidTarget.id, oosGroupCode, voidReason.trim());
      closeVoid();
      reload();
    } catch (e: unknown) {
      setVoidError(getErrorMessage(e, t("attachments.voidFailed")));
    } finally {
      setVoiding(false);
    }
  };

  const columnLabels = [
    t("docs.columns.status"),
    t("docs.columns.filename"),
    t("docs.columns.size"),
    t("docs.columns.uploadedBy"),
    t("docs.columns.date"),
    t("docs.columns.actions")
  ];

  return (
    <PanelFrame title={t("attachments.title")}>
      <Stack spacing={1.5}>
        {panelError && (
          <Alert severity="error" role="alert">
            {panelError}
          </Alert>
        )}

        {loading && <LinearProgress aria-label={t("attachments.loading")} />}

        {loadError ? (
          <Alert
            severity="error"
            role="alert"
            action={
              <Button color="inherit" size="small" onClick={reload}>
                {t("attachments.retry")}
              </Button>
            }
          >
            {loadError}
          </Alert>
        ) : docs && docs.length === 0 ? (
          <Typography variant="body2" color="text.secondary" role="status">
            {t("attachments.empty")}
          </Typography>
        ) : (
          docs && (
            <TableContainer sx={{ border: 1, borderColor: "divider", borderRadius: 1 }}>
              <Table size="small">
                <TableHead sx={tableHeadSx}>
                  <TableRow>
                    {columnLabels.map((label) => (
                      <TableCell key={label}>{label}</TableCell>
                    ))}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {docs.map((doc) => {
                    const state = stateOf(doc);
                    return (
                      <TableRow key={doc.id}>
                        <TableCell>
                          <ToneBadge tone={STATE_TONE[state]} label={t(`docs.statuses.${state.toLowerCase()}`)} />
                        </TableCell>
                        <TableCell>
                          <Typography variant="body2" sx={{ fontWeight: 600, wordBreak: "break-all" }}>
                            {doc.originalFileName}
                          </Typography>
                          {doc.supersessionReason && (
                            <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                              {t("docs.supersededReason", { reason: doc.supersessionReason })}
                            </Typography>
                          )}
                          {doc.voidReason && (
                            <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                              {t("docs.voidedReason", { reason: doc.voidReason })}
                            </Typography>
                          )}
                        </TableCell>
                        <TableCell sx={{ fontVariantNumeric: "tabular-nums" }}>{formatFileSize(doc.fileSizeBytes)}</TableCell>
                        <TableCell>{doc.uploadedByName}</TableCell>
                        <TableCell sx={{ fontVariantNumeric: "tabular-nums" }}>{formatDateTimeWithTimeZone(doc.uploadedAt)}</TableCell>
                        <TableCell>
                          <Stack direction="row" spacing={0.5} sx={{ flexWrap: "wrap" }}>
                            <Button
                              size="small"
                              variant="outlined"
                              aria-label={t("attachments.downloadAria", { name: doc.originalFileName })}
                              onClick={() => void download(doc)}
                            >
                              {t("attachments.download")}
                            </Button>
                            {canManage && state === "Current" && (
                              <Button
                                size="small"
                                aria-label={t("attachments.supersedeAria", { name: doc.originalFileName })}
                                onClick={() => setSupersedeTarget(doc)}
                              >
                                {t("attachments.supersede")}
                              </Button>
                            )}
                            {canManage && state !== "Voided" && (
                              <Button
                                size="small"
                                color="error"
                                aria-label={t("attachments.voidAria", { name: doc.originalFileName })}
                                onClick={() => setVoidTarget(doc)}
                              >
                                {t("attachments.void")}
                              </Button>
                            )}
                          </Stack>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </TableContainer>
          )
        )}

        {canManage ? (
          <Box>
            <FileDropZone
              file={file}
              onFile={(next) => {
                setFile(next);
                setPanelError(null);
              }}
              accept={ACCEPT}
              formatsHint={t("docs.formatsHint")}
              note={t("docs.note")}
              disabled={uploading}
            />
            <Button sx={{ mt: 1 }} variant="contained" disabled={!file || uploading} onClick={() => void upload()}>
              {uploading ? t("attachments.uploading") : t("attachments.upload")}
            </Button>
          </Box>
        ) : (
          <Typography variant="body2" color="text.secondary">
            {t("attachments.readOnly")}
          </Typography>
        )}
      </Stack>

      <FormDialog
        open={supersedeTarget !== null}
        title={t("attachments.supersedeTitle")}
        onClose={closeSupersede}
        onSubmit={submitSupersede}
        submitLabel={t("attachments.supersede")}
        submitting={superseding}
        submitDisabled={!supersedeFile || !supersedeReason.trim()}
        error={supersedeError}
      >
        <Typography variant="body2" sx={{ fontWeight: 600, wordBreak: "break-all" }}>
          {supersedeTarget?.originalFileName}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {t("attachments.supersedeIntro")}
        </Typography>
        <TextField
          required
          multiline
          minRows={2}
          label={t("attachments.supersedeReason")}
          value={supersedeReason}
          onChange={(e) => setSupersedeReason(e.target.value)}
          slotProps={{ htmlInput: { maxLength: REASON_MAX } }}
        />
        <FileDropZone
          file={supersedeFile}
          onFile={setSupersedeFile}
          accept={ACCEPT}
          formatsHint={t("docs.formatsHint")}
          note={t("docs.note")}
          disabled={superseding}
        />
      </FormDialog>

      <FormDialog
        open={voidTarget !== null}
        title={t("attachments.voidTitle")}
        onClose={closeVoid}
        onSubmit={submitVoid}
        submitLabel={t("attachments.voidSubmit")}
        submitting={voiding}
        submitDisabled={!voidReason.trim()}
        error={voidError}
      >
        <Typography variant="body2" sx={{ fontWeight: 600, wordBreak: "break-all" }}>
          {voidTarget?.originalFileName}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {t("attachments.voidIntro")}
        </Typography>
        <TextField
          required
          multiline
          minRows={2}
          label={t("attachments.voidReason")}
          value={voidReason}
          onChange={(e) => setVoidReason(e.target.value)}
          slotProps={{ htmlInput: { maxLength: REASON_MAX } }}
        />
      </FormDialog>
    </PanelFrame>
  );
}
