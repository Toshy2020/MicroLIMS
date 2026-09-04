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
  const [types, setTypes] = useState<DocumentTypeDto[]>([]);
  const [departments, setDepartments] = useState<DocumentDepartmentDto[]>([]);
  const [sections, setSections] = useState<DocumentSectionDto[]>([]);
  const [users, setUsers] = useState<Array<{ id: number; fullName: string; roleName: string }>>([]);

  const [companyDocumentCode, setCompanyDocumentCode] = useState(document.companyDocumentCode);
  const [title, setTitle] = useState(document.title);
  const [documentTypeId, setDocumentTypeId] = useState<number>(document.documentTypeId);
  const [departmentId, setDepartmentId] = useState<number>(document.departmentId);
  const [sectionId, setSectionId] = useState<number>(document.sectionId);
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
      .catch((err) => {
        setError(err.message || "Failed to load master configuration.");
      })
      .finally(() => {
        setLoadingConfig(false);
      });
  }, [open, document]);

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
      setError("Company Document Code and Title are required.");
      return;
    }
    if (!documentTypeId || !departmentId || !sectionId || !ownerUserId) {
      setError("Please complete all mandatory selection fields.");
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
      documentOwnerUserId: ownerUserId,
      confidentiality,
      category: category.trim() || undefined,
      keywords
    };

    try {
      const updated = await documentControlService.updateDraftMetadata(document.id, request);
      onSuccess(updated);
      onClose();
    } catch (err: any) {
      const msg = err.response?.data?.message || err.message || "Failed to update draft metadata.";
      setError(msg);
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
            Edit Draft Metadata
          </Typography>
          <Typography variant="caption" color="text.secondary">
            MicroLIMS ID: <strong>{document.microLimsDocumentId}</strong> | Revision: <strong>{document.currentRevisionNumber || "01"} (Draft)</strong>
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
              label="Company Document Code *"
              value={companyDocumentCode}
              onChange={(e) => setCompanyDocumentCode(e.target.value)}
              disabled={saving}
              helperText="E.g. SOP-QC-001"
            />

            <TextField
              label="Document Title *"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              disabled={saving}
            />

            <TextField
              select
              label="Document Type *"
              value={documentTypeId}
              onChange={(e) => setDocumentTypeId(Number(e.target.value))}
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
              label="Category"
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              disabled={saving}
              placeholder="E.g. Testing Procedures, Cleaning, Validation"
            />

            <TextField
              label="Document Owner User ID *"
              type="number"
              value={ownerUserId}
              onChange={(e) => setOwnerUserId(Number(e.target.value))}
              disabled={saving}
              helperText={`Current: ${document.documentOwnerUserName}`}
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
                  Add Tag
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
          onClick={handleSave}
          variant="contained"
          disabled={saving || loadingConfig}
          startIcon={saving ? <CircularProgress size={16} color="inherit" /> : null}
        >
          {saving ? "Saving Changes..." : "Save Draft Metadata"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
