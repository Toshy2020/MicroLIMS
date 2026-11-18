import { useState, useEffect } from "react";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  TextField,
  MenuItem,
  Alert,
  CircularProgress,
  Chip,
  Autocomplete
} from "@mui/material";
import EditNoteOutlinedIcon from "@mui/icons-material/EditNoteOutlined";
import { documentControlService } from "../services/documentControlService";
import type {
  DocumentMasterDto,
  DocumentTypeDto,
  DocumentDepartmentDto,
  DocumentSectionDto,
  DocumentConfidentiality,
  UpdateDocumentMasterDraftRequest
} from "../types/documentControlTypes";

interface DraftMetadataDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: (updated: DocumentMasterDto) => void;
  document: DocumentMasterDto;
}

export function DraftMetadataDialog({
  open,
  onClose,
  onSuccess,
  document
}: DraftMetadataDialogProps) {
  const { t } = useTranslation("documentControl");
  const [types, setTypes] = useState<DocumentTypeDto[]>([]);
  const [departments, setDepartments] = useState<DocumentDepartmentDto[]>([]);
  const [sections, setSections] = useState<DocumentSectionDto[]>([]);
  const [users, setUsers] = useState<Array<{ id: number; fullName: string; roleName: string }>>([]);

  const [companyDocumentCode, setCompanyDocumentCode] = useState(document.companyDocumentCode);
  const [title, setTitle] = useState(document.title);
  const [documentTypeId, setDocumentTypeId] = useState<number>(document.documentTypeId);
  const [departmentId, setDepartmentId] = useState<number>(document.departmentId);
  const [sectionId, setSectionId] = useState<number>(document.sectionId);
  const [additionalSectionIds, setAdditionalSectionIds] = useState<number[]>(() =>
    (document.sectionIds || []).filter((id) => id !== document.sectionId)
  );
  const [ownerUserId, setOwnerUserId] = useState<number>(document.documentOwnerUserId);
  const [confidentiality, setConfidentiality] = useState<DocumentConfidentiality>(document.confidentiality);
  const [category, setCategory] = useState(document.category || "");
  const [keywords, setKeywords] = useState<string[]>(document.keywords || []);
  const [keywordInput, setKeywordInput] = useState("");

  const [loadingConfig, setLoadingConfig] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;

    setCompanyDocumentCode(document.companyDocumentCode);
    setTitle(document.title);
    setDocumentTypeId(document.documentTypeId);
    setDepartmentId(document.departmentId);
    setSectionId(document.sectionId);
    setAdditionalSectionIds(
      (document.sectionIds || []).filter((id) => id !== document.sectionId)
    );
    setOwnerUserId(document.documentOwnerUserId);
    setConfidentiality(document.confidentiality);
    setCategory(document.category || "");
    setKeywords(document.keywords || []);
    setError(null);

    setLoadingConfig(true);
    Promise.all([
      documentControlService.getTypes(),
      documentControlService.getDepartments()
    ])
      .then(([tRes, dRes]) => {
        setTypes(tRes);
        setDepartments(dRes);

        const currentDept = dRes.find((d) => d.id === document.departmentId);
        if (currentDept) {
          setSections(currentDept.sections || []);
        }
      })
      .catch((err: unknown) => {
        setError(serverMessage(err) ?? getErrorMessage(err, t("draftMetadata.loadError")));
      })
      .finally(() => {
        setLoadingConfig(false);
      });
  }, [open, document, t]);

  const handleDepartmentChange = (deptId: number) => {
    setDepartmentId(deptId);
    const dept = departments.find((d) => d.id === deptId);
    const secList = dept?.sections || [];
    setSections(secList);
    if (secList.length > 0) {
      setSectionId(secList[0].id);
    } else {
      setSectionId(0);
    }
    setAdditionalSectionIds([]);
  };

  const handlePrimarySectionChange = (newPrimaryId: number) => {
    setSectionId(newPrimaryId);
    setAdditionalSectionIds((prev) => prev.filter((id) => id !== newPrimaryId));
  };

  const handleAddKeyword = () => {
    if (keywordInput.trim() && !keywords.includes(keywordInput.trim())) {
      setKeywords([...keywords, keywordInput.trim()]);
      setKeywordInput("");
    }
  };

  const handleRemoveKeyword = (kw: string) => {
    setKeywords(keywords.filter((k) => k !== kw));
  };

  const handleSave = async () => {
    if (!companyDocumentCode.trim() || !title.trim()) {
      setError(t("draftMetadata.codeTitleRequired"));
      return;
    }
    if (!documentTypeId || !departmentId || !sectionId || !ownerUserId) {
      setError(t("draftMetadata.fieldsRequired"));
      return;
    }

    setSaving(true);
    setError(null);

    const request: UpdateDocumentMasterDraftRequest = {
      companyDocumentCode: companyDocumentCode.trim(),
      title: title.trim(),
      documentTypeId,
      departmentId,
      sectionId,
      sectionIds: additionalSectionIds,
      documentOwnerUserId: ownerUserId,
      confidentiality,
      category: category.trim() || undefined,
      keywords
    };

    try {
      const updated = await documentControlService.updateDraftMetadata(document.id, request, document.version);
      onSuccess(updated);
      onClose();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("draftMetadata.updateError")));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ pb: 1, display: "flex", alignItems: "center", gap: 1 }}>
        <EditNoteOutlinedIcon color="primary" />
        <Box>
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            {t("draftMetadata.title")}
          </Typography>
          <Typography variant="caption" sx={{
            color: "text.secondary"
          }}>
            <Trans
              i18nKey="draftMetadata.subtitle"
              ns="documentControl"
              values={{ id: document.microLimsDocumentId, rev: document.currentRevisionNumber || "01" }}
              components={{ strong: <strong /> }}
            />
          </Typography>
        </Box>
      </DialogTitle>

      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        {error && <Alert severity="error">{error}</Alert>}

        {loadingConfig ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
            <CircularProgress size={32} />
          </Box>
        ) : (
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              label={t("registerDialog.companyCodeLabel")}
              value={companyDocumentCode}
              onChange={(e) => setCompanyDocumentCode(e.target.value)}
              disabled={saving}
              helperText={t("draftMetadata.codeHelper")}
            />

            <TextField
              label={t("registerDialog.titleLabel")}
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              disabled={saving}
            />

            <TextField
              select
              label={t("registerDialog.typeLabel")}
              value={documentTypeId}
              onChange={(e) => setDocumentTypeId(Number(e.target.value))}
              disabled={saving}
            >
              {types.map((docType) => (
                <MenuItem key={docType.id} value={docType.id}>
                  {docType.code}: {docType.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label={t("registerDialog.confidentialityLabel")}
              value={confidentiality}
              onChange={(e) => setConfidentiality(e.target.value as DocumentConfidentiality)}
              disabled={saving}
            >
              <MenuItem value="Public">{t("registerDialog.confidentialities.Public")}</MenuItem>
              <MenuItem value="Internal">{t("registerDialog.confidentialities.Internal")}</MenuItem>
              <MenuItem value="Restricted">{t("registerDialog.confidentialities.Restricted")}</MenuItem>
              <MenuItem value="Confidential">{t("registerDialog.confidentialities.Confidential")}</MenuItem>
            </TextField>

            <TextField
              select
              label={t("registerDialog.departmentLabel")}
              value={departmentId}
              onChange={(e) => handleDepartmentChange(Number(e.target.value))}
              disabled={saving}
            >
              {departments.map((d) => (
                <MenuItem key={d.id} value={d.id}>
                  {d.code}: {d.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label={t("registerDialog.primarySectionLabel")}
              value={sectionId}
              onChange={(e) => handlePrimarySectionChange(Number(e.target.value))}
              disabled={saving || sections.length === 0}
            >
              {sections.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name}
                </MenuItem>
              ))}
            </TextField>

            <Autocomplete
              multiple
              options={sections.filter((s) => (s.isActive !== false || additionalSectionIds.includes(s.id)) && s.id !== sectionId)}
              getOptionLabel={(option) => option.name}
              isOptionEqualToValue={(option, value) => option.id === value.id}
              value={sections.filter((s) => additionalSectionIds.includes(s.id) && s.id !== sectionId)}
              onChange={(_event, newValue) => {
                setAdditionalSectionIds(newValue.map((s) => s.id));
              }}
              disabled={saving || !departmentId || sections.length === 0}
              renderInput={(params) => (
                <TextField
                  {...params}
                  label={t("registerDialog.additionalSectionsLabel")}
                  placeholder={additionalSectionIds.length === 0 ? t("registerDialog.additionalSectionsPlaceholder") : undefined}
                  helperText={t("registerDialog.additionalSectionsHelper")}
                />
              )}
              sx={{ gridColumn: { xs: "1fr", sm: "1 / -1" } }}
            />

            <TextField
              label={t("registerDialog.categoryLabel")}
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              disabled={saving}
              placeholder={t("draftMetadata.categoryPlaceholder")}
            />

            <TextField
              label={t("registerDialog.ownerIdLabel")}
              type="number"
              value={ownerUserId}
              onChange={(e) => setOwnerUserId(Number(e.target.value))}
              disabled={saving}
              helperText={t("draftMetadata.currentOwner", { name: document.documentOwnerUserName })}
            />

            <Box sx={{ gridColumn: { xs: "1fr", sm: "1 / -1" } }}>
              <Typography variant="caption" sx={{ fontWeight: 700, mb: 1, display: "block" }}>
                {t("registerDialog.keywordsTitle")}
              </Typography>
              <Box sx={{ display: "flex", gap: 1, mb: 1.5 }}>
                <TextField
                  size="small"
                  placeholder={t("registerDialog.keywordPlaceholder")}
                  value={keywordInput}
                  onChange={(e) => setKeywordInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      e.preventDefault();
                      handleAddKeyword();
                    }
                  }}
                  disabled={saving}
                  slotProps={{ htmlInput: { "aria-label": t("registerDialog.keywordAria") } }}
                />
                <Button size="small" variant="outlined" onClick={handleAddKeyword} disabled={saving || !keywordInput.trim()}>
                  {t("draftMetadata.addTag")}
                </Button>
              </Box>
              <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                {keywords.map((kw) => (
                  <Chip key={kw} label={kw} size="small" onDelete={() => handleRemoveKeyword(kw)} disabled={saving} />
                ))}
              </Box>
            </Box>
          </Box>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={saving} color="inherit">
          {t("common.cancel")}
        </Button>
        <Button
          onClick={handleSave}
          variant="contained"
          disabled={saving || loadingConfig}
          startIcon={saving ? <CircularProgress size={16} color="inherit" /> : null}
        >
          {saving ? t("draftMetadata.saving") : t("draftMetadata.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
