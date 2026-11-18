import React, { useState, useRef } from "react";
import {
  Card,
  CardContent,
  Typography,
  Box,
  Button,
  Stack,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  List,
  ListItem,
  ListItemText,
  ListItemIcon,
  IconButton,
  Chip,
  Tooltip
} from "@mui/material";
import AttachFileIcon from "@mui/icons-material/AttachFile";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { Trans } from "react-i18next";
import { toast } from "sonner";
import { Ltr } from "../../../../components/Ltr";
import { useTranslation } from "../../../../i18n";
import { formatDateTimeWithTimeZone } from "../../../../utils/formatDate";
import { WorkingStandardService } from "../../services/WorkingStandardService";
import type {
  WorkingStandardDocumentDto,
  WorkingStandardDocumentKind
} from "../../types";

interface QualificationDocumentsSectionProps {
  qualificationId: number;
  documents: WorkingStandardDocumentDto[];
  isDraft: boolean;
  canQualify: boolean;
  onDocumentUploaded: (doc: WorkingStandardDocumentDto) => void;
}

export const QualificationDocumentsSection: React.FC<QualificationDocumentsSectionProps> = ({
  qualificationId,
  documents,
  isDraft,
  canQualify,
  onDocumentUploaded
}) => {
  const { t } = useTranslation("workingStandards");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [docKind, setDocKind] = useState<WorkingStandardDocumentKind>("SourceReport");
  const [uploading, setUploading] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setSelectedFile(e.target.files[0]);
    }
  };

  const handleUpload = async () => {
    if (!selectedFile) return;
    setUploading(true);
    try {
      const doc = await WorkingStandardService.uploadDocument(qualificationId, selectedFile, docKind);
      toast.success(t("documents.uploadSuccess", { fileName: selectedFile.name }));
      setSelectedFile(null);
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
      onDocumentUploaded(doc);
    } catch {
      toast.error(t("documents.uploadError"));
    } finally {
      setUploading(false);
    }
  };

  const handleOpenDoc = async (id: number) => {
    try {
      await WorkingStandardService.openDocumentInNewTab(id);
    } catch {
      toast.error(t("documents.openError"));
    }
  };

  return (
    <Card variant="outlined" sx={{ borderRadius: 2 }}>
      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.secondary" }}>
          {t("documents.heading")}
        </Typography>

        {documents.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ fontStyle: "italic", mb: 1.5 }}>
            {t("documents.empty")}
          </Typography>
        ) : (
          <List dense disablePadding sx={{ mb: 1.5 }}>
            {documents.map((doc) => {
              const dateText = formatDateTimeWithTimeZone(doc.uploadedAt);
              return (
                <ListItem
                  key={doc.id}
                  disableGutters
                  sx={{ py: 0.75, borderBottom: 1, borderColor: "divider" }}
                  secondaryAction={
                    <Tooltip title={t("documents.viewDocument")}>
                      <IconButton
                        size="small"
                        aria-label={t("documents.viewDocument")}
                        onClick={() => handleOpenDoc(doc.id)}
                      >
                        <OpenInNewIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  }
                >
                  <ListItemIcon sx={{ minWidth: 32 }}>
                    <AttachFileIcon fontSize="small" color="action" />
                  </ListItemIcon>
                  <ListItemText
                    primary={
                      <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                          <Ltr>{doc.fileName}</Ltr>
                        </Typography>
                        <Chip
                          size="small"
                          label={t(`documentKinds.${doc.kind}`, { defaultValue: doc.kind })}
                          sx={{ fontSize: 12, height: 18 }}
                        />
                      </Stack>
                    }
                    secondary={
                      <Typography variant="caption" color="text.secondary">
                        {doc.uploadedByUserName ? (
                          <Trans
                            i18nKey="documents.uploadedOnBy"
                            ns="workingStandards"
                            values={{ date: dateText, user: doc.uploadedByUserName }}
                            components={{ ltr: <Ltr>{null}</Ltr> }}
                          />
                        ) : (
                          <Trans
                            i18nKey="documents.uploadedOn"
                            ns="workingStandards"
                            values={{ date: dateText }}
                            components={{ ltr: <Ltr>{null}</Ltr> }}
                          />
                        )}
                      </Typography>
                    }
                  />
                </ListItem>
              );
            })}
          </List>
        )}

        {isDraft && canQualify && (
          <Box sx={{ mt: 1.5, pt: 1.5, borderTop: 1, borderColor: "divider" }}>
            <Typography variant="caption" sx={{ fontWeight: 600, display: "block", mb: 1 }}>
              {t("documents.uploadSectionTitle")}
            </Typography>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: "center" }}>
              <Button
                variant="outlined"
                component="label"
                size="small"
                startIcon={<AttachFileIcon fontSize="small" />}
                sx={{ whiteSpace: "nowrap", textOverflow: "ellipsis", overflow: "hidden", maxWidth: 180 }}
              >
                {selectedFile ? selectedFile.name : t("actions.chooseFile")}
                <input ref={fileInputRef} type="file" hidden onChange={handleFileChange} />
              </Button>

              <FormControl size="small" sx={{ minWidth: 150 }}>
                <InputLabel id="doc-kind-label">{t("documents.typeLabel")}</InputLabel>
                <Select
                  labelId="doc-kind-label"
                  label={t("documents.typeLabel")}
                  value={docKind}
                  onChange={(e) => setDocKind(e.target.value as WorkingStandardDocumentKind)}
                >
                  <MenuItem value="SourceReport">{t("documentKinds.SourceReport")}</MenuItem>
                  <MenuItem value="MoistureReport">{t("documentKinds.MoistureReport")}</MenuItem>
                </Select>
              </FormControl>

              <Button
                variant="contained"
                size="small"
                startIcon={<CloudUploadIcon fontSize="small" />}
                disabled={!selectedFile || uploading}
                onClick={handleUpload}
              >
                {uploading ? t("actions.uploading") : t("actions.upload")}
              </Button>
            </Stack>
          </Box>
        )}
      </CardContent>
    </Card>
  );
};
