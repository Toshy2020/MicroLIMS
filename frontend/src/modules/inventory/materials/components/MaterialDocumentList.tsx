import { useEffect, useState } from "react";
import {
  Box,
  Typography,
  Chip,
  IconButton,
  Tooltip,
  Divider,
  CircularProgress,
  Stack
} from "@mui/material";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import BlockIcon from "@mui/icons-material/Block";
import { MaterialService } from "../services/MaterialService";
import { formatLabDate } from "../../../../utils/formatDate";
import { useAuth } from "../../../../contexts/AuthContext";
import type { MaterialDocument, MaterialDocumentStatus } from "../types/materialTypes";
import { MATERIAL_DOCUMENT_TYPE_LABELS } from "../types/materialTypes";
import { SupersedeMaterialDocumentDialog } from "./SupersedeMaterialDocumentDialog";
import { VoidMaterialDocumentDialog } from "./VoidMaterialDocumentDialog";

interface Props {
  materialId: number;
  isExpired: boolean;
  refreshKey: number;
  onDocumentChanged: () => void;
}

const STATUS_COLORS: Record<MaterialDocumentStatus, "success" | "warning" | "default" | "error"> = {
  Current: "success",
  Superseded: "warning",
  Voided: "error"
};

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function MaterialDocumentList({ materialId, isExpired, refreshKey, onDocumentChanged }: Props) {
  const { role } = useAuth();
  const canSupersede = role === "SectionHead" || role === "SystemAdministrator";

  const [documents, setDocuments] = useState<MaterialDocument[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [supersedeTarget, setSupersedeTarget] = useState<MaterialDocument | null>(null);
  const [voidTarget, setVoidTarget] = useState<MaterialDocument | null>(null);

  useEffect(() => {
    setLoading(true);
    setError(null);
    MaterialService.getDocuments(materialId)
      .then(setDocuments)
      .catch(() => setError("Failed to load documents."))
      .finally(() => setLoading(false));
  }, [materialId, refreshKey]);

  const handleView = async (doc: MaterialDocument) => {
    try {
      const blob = await MaterialService.getDocumentContent(doc.id, materialId);
      const url = URL.createObjectURL(blob);
      window.open(url, "_blank", "noopener,noreferrer");
    } catch {
      alert("Failed to retrieve document. Please try again.");
    }
  };

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 2 }}>
        <CircularProgress size={20} />
      </Box>
    );
  }

  if (error) {
    return <Typography sx={{ fontSize: 12, color: "error.main" }}>{error}</Typography>;
  }

  if (documents.length === 0) {
    return (
      <Typography sx={{ fontSize: 12, color: "text.secondary", fontStyle: "italic" }}>
        No documents have been uploaded for this lot.
      </Typography>
    );
  }

  return (
    <>
      <Stack divider={<Divider />} spacing={0}>
        {documents.map((doc) => (
          <Box key={doc.id} sx={{ py: 1.5 }}>
            <Box sx={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 1 }}>
              {/* Document info */}
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, flexWrap: "wrap", mb: 0.25 }}>
                  <Typography
                    sx={{
                      fontSize: 13,
                      fontWeight: 600,
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                      maxWidth: 260,
                      color: doc.status === "Voided" ? "text.disabled" : "text.primary"
                    }}
                    title={doc.originalFileName}
                  >
                    {doc.originalFileName}
                  </Typography>
                  <Chip
                    id={`doc-status-${doc.id}`}
                    label={doc.status}
                    size="small"
                    color={STATUS_COLORS[doc.status]}
                    variant={doc.status === "Current" ? "filled" : "outlined"}
                    sx={{ fontSize: 10, height: 18 }}
                  />
                </Box>

                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                  {MATERIAL_DOCUMENT_TYPE_LABELS[doc.documentType]} · {formatBytes(doc.fileSizeBytes)}
                </Typography>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                  Uploaded by {doc.uploadedByName} · {formatLabDate(doc.uploadedAt)}
                </Typography>

                {isExpired && doc.status === "Current" && (
                  <Typography sx={{ fontSize: 10, color: "text.secondary", fontStyle: "italic", mt: 0.25 }}>
                    Historical — lot expired
                  </Typography>
                )}

                {doc.status === "Voided" && doc.voidReason && (
                  <Typography sx={{ fontSize: 11, color: "error.main", mt: 0.25 }}>
                    Voided: {doc.voidReason}
                  </Typography>
                )}

                {doc.status === "Superseded" && doc.supersessionReason && (
                  <Typography sx={{ fontSize: 11, color: "warning.dark", mt: 0.25 }}>
                    Superseded: {doc.supersessionReason}
                  </Typography>
                )}
              </Box>

              {/* Action buttons */}
              <Box sx={{ display: "flex", alignItems: "center", flexShrink: 0 }}>
                <Tooltip title="View / Download">
                  <IconButton
                    id={`view-doc-${doc.id}`}
                    size="small"
                    onClick={() => handleView(doc)}
                    sx={{ color: "primary.main" }}
                  >
                    <VisibilityOutlinedIcon fontSize="small" />
                  </IconButton>
                </Tooltip>

                {canSupersede && doc.status === "Current" && (
                  <Tooltip title="Supersede (Replace)">
                    <IconButton
                      id={`supersede-doc-${doc.id}`}
                      size="small"
                      onClick={() => setSupersedeTarget(doc)}
                      sx={{ color: "warning.main" }}
                    >
                      <SwapHorizIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}

                {canSupersede && doc.status !== "Voided" && (
                  <Tooltip title="Void">
                    <IconButton
                      id={`void-doc-${doc.id}`}
                      size="small"
                      onClick={() => setVoidTarget(doc)}
                      sx={{ color: "error.main" }}
                    >
                      <BlockIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}
              </Box>
            </Box>
          </Box>
        ))}
      </Stack>

      {supersedeTarget && (
        <SupersedeMaterialDocumentDialog
          open={supersedeTarget != null}
          document={supersedeTarget}
          materialId={materialId}
          onClose={() => setSupersedeTarget(null)}
          onSuccess={() => { setSupersedeTarget(null); onDocumentChanged(); }}
        />
      )}

      {voidTarget && (
        <VoidMaterialDocumentDialog
          open={voidTarget != null}
          document={voidTarget}
          materialId={materialId}
          onClose={() => setVoidTarget(null)}
          onSuccess={() => { setVoidTarget(null); onDocumentChanged(); }}
        />
      )}
    </>
  );
}
