import { useState, useEffect } from "react";
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
  Chip
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
  const { userId } = useAuth();

  const [types, setTypes] = useState<DocumentTypeDto[]>([]);
  const [departments, setDepartments] = useState<DocumentDepartmentDto[]>([]);
  const [sections, setSections] = useState<DocumentSectionDto[]>([]);

  const [companyDocumentCode, setCompanyDocumentCode] = useState("");
  const [title, setTitle] = useState("");
  const [documentTypeId, setDocumentTypeId] = useState<number>(0);
  const [departmentId, setDepartmentId] = useState<number>(0);
  const [sectionId, setSectionId] = useState<number>(0);
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
      .catch((err) => {
        setError(err.message || "Failed to load master configuration.");
      })
      .finally(() => {
        setLoadingConfig(false);
      });
  }, [open, userId]);

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
      setError("Company Document Code and Title are required.");
      return;
    }
    if (!documentTypeId || !departmentId || !sectionId || !ownerUserId) {
      setError("Please complete all mandatory selection fields.");
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
    } catch (err: any) {
      const msg = err.response?.data?.message || err.message || "Failed to register document.";
      setError(msg);
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
            Register New Document Master
          </Typography>
          <Typography variant="caption" color="text.secondary">
            Initial Draft Registration | Permanent MicroLIMS Document ID will be sequence-assigned
          </Typography>
        </Box>
      </DialogTitle>

      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        <Alert severity="info">
          The permanent <strong>MicroLIMS Document ID</strong> (e.g. <code>DOC-0000001</code>) is automatically generated by the database sequence upon registration and cannot be modified. Initial status will be <strong>Draft</strong>.
        </Alert>

        {error && <Alert severity="error">{error}</Alert>}

        {loadingConfig ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
            <CircularProgress size={32} />
          </Box>
        ) : (
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              label="Company Document Code *"
              value={companyDocumentCode}
              onChange={(e) => setCompanyDocumentCode(e.target.value)}
              disabled={saving}
              placeholder="e.g. SOP-QC-001"
              helperText="Must be unique among active documents"
            />

            <TextField
              label="Document Title *"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              disabled={saving}
              placeholder="e.g. Microbial Examination of Non-Sterile Products"
            />

            <TextField
              select
              label="Document Type *"
              value={documentTypeId}
              onChange={(e) => handleTypeChange(Number(e.target.value))}
              disabled={saving}
            >
              {types.map((t) => (
                <MenuItem key={t.id} value={t.id}>
                  {t.code} — {t.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Confidentiality *"
              value={confidentiality}
              onChange={(e) => setConfidentiality(e.target.value as DocumentConfidentiality)}
              disabled={saving}
            >
              <MenuItem value="Public">Public</MenuItem>
              <MenuItem value="Internal">Internal</MenuItem>
              <MenuItem value="Restricted">Restricted</MenuItem>
              <MenuItem value="Confidential">Confidential</MenuItem>
            </TextField>

            <TextField
              select
              label="Department *"
              value={departmentId}
              onChange={(e) => handleDepartmentChange(Number(e.target.value))}
              disabled={saving}
            >
              {departments.map((d) => (
                <MenuItem key={d.id} value={d.id}>
                  {d.code} — {d.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Section *"
              value={sectionId}
              onChange={(e) => setSectionId(Number(e.target.value))}
              disabled={saving || sections.length === 0}
            >
              {sections.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              label="Initial Revision Number"
              value={initialRevisionNumber}
              onChange={(e) => setInitialRevisionNumber(e.target.value)}
              disabled={saving}
              helperText="Default: 01"
            />

            <TextField
              label="Review Cycle (Months)"
              type="number"
              value={reviewCycleMonths}
              onChange={(e) => setReviewCycleMonths(Number(e.target.value))}
              disabled={saving}
              helperText="Inherited from Document Type"
            />

            <TextField
              label="Category"
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              disabled={saving}
              placeholder="e.g. Validation, Media Prep, Environmental Monitoring"
            />

            <TextField
              label="Document Owner User ID *"
              type="number"
              value={ownerUserId}
              onChange={(e) => setOwnerUserId(Number(e.target.value))}
              disabled={saving}
              helperText="Assigned primary owner"
            />

            <Box sx={{ gridColumn: { xs: "1fr", sm: "1 / -1" } }}>
              <Typography variant="caption" sx={{ fontWeight: 700, mb: 1, display: "block" }}>
                Keywords & Tags
              </Typography>
              <Box sx={{ display: "flex", gap: 1, mb: 1.5 }}>
                <TextField
                  size="small"
                  placeholder="Enter keyword..."
                  value={keywordInput}
                  onChange={(e) => setKeywordInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      e.preventDefault();
                      handleAddKeyword();
                    }
                  }}
                  disabled={saving}
                />
                <Button size="small" variant="outlined" onClick={handleAddKeyword} disabled={saving || !keywordInput.trim()}>
                  Add Keyword
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
          Cancel
        </Button>
        <Button
          onClick={handleRegister}
          variant="contained"
          disabled={saving || loadingConfig}
          startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <NoteAddOutlinedIcon />}
        >
          {saving ? "Registering..." : "Register Document Master"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
