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
import NoteAddOutlinedIcon from "@mui/icons-material/NoteAddOutlined";
import { documentControlService } from "../services/documentControlService";
import { useAuth } from "../../../contexts/AuthContext";
import type {
  DocumentTypeDto,
  DocumentDepartmentDto,
  DocumentSectionDto,
  DocumentConfidentiality,
  RegisterDocumentMasterRequest,
  DocumentMasterDto
} from "../types/documentControlTypes";

interface RegisterDocumentDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: (newDocument: DocumentMasterDto) => void;
}

export function RegisterDocumentDialog({
  open,
  onClose,
  onSuccess
}: RegisterDocumentDialogProps) {
  const { t } = useTranslation("documentControl");
  const { userId } = useAuth();

  const [types, setTypes] = useState<DocumentTypeDto[]>([]);
  const [departments, setDepartments] = useState<DocumentDepartmentDto[]>([]);
  const [sections, setSections] = useState<DocumentSectionDto[]>([]);

  const [companyDocumentCode, setCompanyDocumentCode] = useState("");
  const [title, setTitle] = useState("");
  const [documentTypeId, setDocumentTypeId] = useState<number>(0);
  const [departmentId, setDepartmentId] = useState<number>(0);
  const [sectionId, setSectionId] = useState<number>(0);
  const [additionalSectionIds, setAdditionalSectionIds] = useState<number[]>([]);
  const [ownerUserId, setOwnerUserId] = useState<number>(userId || 1);
  const [confidentiality, setConfidentiality] = useState<DocumentConfidentiality>("Internal");
  const [category, setCategory] = useState("");
  const [initialRevisionNumber, setInitialRevisionNumber] = useState("01");
  const [reviewCycleMonths, setReviewCycleMonths] = useState<number>(24);
  const [keywords, setKeywords] = useState<string[]>([]);
  const [keywordInput, setKeywordInput] = useState("");

  const [loadingConfig, setLoadingConfig] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;

    setCompanyDocumentCode("");
    setTitle("");
    setDocumentTypeId(0);
    setDepartmentId(0);
    setSectionId(0);
    setAdditionalSectionIds([]);
    setOwnerUserId(userId || 1);
    setConfidentiality("Internal");
    setCategory("");
    setInitialRevisionNumber("01");
    setReviewCycleMonths(24);
    setKeywords([]);
    setError(null);

    setLoadingConfig(true);
    Promise.all([
      documentControlService.getTypes(),
      documentControlService.getDepartments()
    ])
      .then(([tRes, dRes]) => {
        setTypes(tRes);
        setDepartments(dRes);
        if (tRes.length > 0) {
          setDocumentTypeId(tRes[0].id);
          setReviewCycleMonths(tRes[0].defaultReviewCycleMonths);
        }
        if (dRes.length > 0) {
          setDepartmentId(dRes[0].id);
          const sList = dRes[0].sections || [];
          setSections(sList);
          if (sList.length > 0) setSectionId(sList[0].id);
        }
      })
      .catch((err: unknown) => {
        setError(serverMessage(err) ?? getErrorMessage(err, t("registerDialog.loadError")));
      })
      .finally(() => {
        setLoadingConfig(false);
      });
  }, [open, userId, t]);

  const handleTypeChange = (tId: number) => {
    setDocumentTypeId(tId);
    const selectedType = types.find((t) => t.id === tId);
    if (selectedType) {
      setReviewCycleMonths(selectedType.defaultReviewCycleMonths);
    }
  };

  const handleDepartmentChange = (dId: number) => {
    setDepartmentId(dId);
    const dept = departments.find((d) => d.id === dId);
    const sList = dept?.sections || [];
    setSections(sList);
    if (sList.length > 0) {
      setSectionId(sList[0].id);
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

  const handleRegister = async () => {
    if (!companyDocumentCode.trim() || !title.trim()) {
      setError(t("registerDialog.codeTitleRequired"));
      return;
    }
    if (!documentTypeId || !departmentId || !sectionId || !ownerUserId) {
      setError(t("registerDialog.fieldsRequired"));
      return;
    }

    setSaving(true);
    setError(null);

    const request: RegisterDocumentMasterRequest = {
      companyDocumentCode: companyDocumentCode.trim(),
      title: title.trim(),
      documentTypeId,
      departmentId,
      sectionId,
      sectionIds: additionalSectionIds,
      documentOwnerUserId: ownerUserId,
      confidentiality,
      category: category.trim() || undefined,
      keywords,
      initialRevisionNumber: initialRevisionNumber.trim() || "01",
      reviewCycleMonths
    };

    try {
      const created = await documentControlService.registerDocument(request);
      onSuccess(created);
      onClose();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("registerDialog.registerError")));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ pb: 1, display: "flex", alignItems: "center", gap: 1 }}>
        <NoteAddOutlinedIcon color="primary" />
        <Box>
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            {t("registerDialog.title")}
          </Typography>
          <Typography variant="caption" sx={{
            color: "text.secondary"
          }}>
            {t("registerDialog.subtitle")}
          </Typography>
        </Box>
      </DialogTitle>

      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        <Alert severity="info">
          <Trans
            i18nKey="registerDialog.info"
            ns="documentControl"
            components={{ strong: <strong />, code: <code /> }}
          />
        </Alert>

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
              placeholder={t("registerDialog.companyCodePlaceholder")}
              helperText={t("registerDialog.companyCodeHelper")}
            />

            <TextField
              label={t("registerDialog.titleLabel")}
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              disabled={saving}
              placeholder={t("registerDialog.titlePlaceholder")}
            />

            <TextField
              select
              label={t("registerDialog.typeLabel")}
              value={documentTypeId}
              onChange={(e) => handleTypeChange(Number(e.target.value))}
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
              options={sections.filter((s) => s.isActive !== false && s.id !== sectionId)}
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
              label={t("registerDialog.initialRevLabel")}
              value={initialRevisionNumber}
              onChange={(e) => setInitialRevisionNumber(e.target.value)}
              disabled={saving}
              helperText={t("registerDialog.initialRevHelper")}
            />

            <TextField
              label={t("registerDialog.reviewCycleLabel")}
              type="number"
              value={reviewCycleMonths}
              onChange={(e) => setReviewCycleMonths(Number(e.target.value))}
              disabled={saving}
              helperText={t("registerDialog.reviewCycleHelper")}
            />

            <TextField
              label={t("registerDialog.categoryLabel")}
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              disabled={saving}
              placeholder={t("registerDialog.categoryPlaceholder")}
            />

            <TextField
              label={t("registerDialog.ownerIdLabel")}
              type="number"
              value={ownerUserId}
              onChange={(e) => setOwnerUserId(Number(e.target.value))}
              disabled={saving}
              helperText={t("registerDialog.ownerIdHelper")}
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
                  {t("registerDialog.addKeyword")}
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
          onClick={handleRegister}
          variant="contained"
          disabled={saving || loadingConfig}
          startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <NoteAddOutlinedIcon />}
        >
          {saving ? t("registerDialog.registering") : t("registerDialog.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
