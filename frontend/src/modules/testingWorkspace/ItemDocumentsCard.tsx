import { useState, useEffect } from "react";
import { Paper, Box, Typography, Stack, Button, Chip, useTheme } from "@mui/material";
import DescriptionIcon from "@mui/icons-material/Description";
import VerifiedIcon from "@mui/icons-material/Verified";
import DownloadIcon from "@mui/icons-material/Download";
import VisibilityIcon from "@mui/icons-material/Visibility";
import {
  ItemDocumentService,
  ItemDocumentDto,
  ItemDocumentType,
  MaterialDocumentStatus,
} from "../laboratoryConfiguration/items/services/ItemDocumentService";

interface ItemDocumentsCardProps {
  itemId: number;
  itemName: string;
}

export function ItemDocumentsCard({ itemId, itemName }: ItemDocumentsCardProps) {
  const theme = useTheme();
  const [documents, setDocuments] = useState<ItemDocumentDto[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (itemId) {
      setLoading(true);
      ItemDocumentService.getDocumentsForItem(itemId)
        .then(setDocuments)
        .catch(() => setDocuments([]))
        .finally(() => setLoading(false));
    }
  }, [itemId]);

  const currentSop = documents.find((d) => d.documentType === ItemDocumentType.Sop && d.status === MaterialDocumentStatus.Current);
  const currentVr = documents.find(
    (d) => d.documentType === ItemDocumentType.VerificationReport && d.status === MaterialDocumentStatus.Current
  );

  if (loading) return null;

  return (
    <Paper
      elevation={0}
      sx={{
        p: 2,
        borderRadius: 2,
        bgcolor: "background.default",
        border: "1px solid",
        borderColor: "divider",
      }}
    >
      <Typography
        variant="subtitle2"
        sx={{
          fontWeight: 700,
          fontSize: 13,
          color: "text.primary",
          display: "flex",
          alignItems: "center",
          gap: 1,
          mb: 1.5,
        }}
      >
        <DescriptionIcon fontSize="small" sx={{ color: theme.custom.status.purple.text }} />
        Item Controlled Documents
      </Typography>

      {!currentSop && !currentVr ? (
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          No controlled SOP or Verification Report attached to this item.
        </Typography>
      ) : (
        <Stack spacing={1.25}>
          {currentSop && (
            <ItemDocRow
              title="SOP"
              doc={currentSop}
              icon={<DescriptionIcon fontSize="small" sx={{ color: theme.custom.status.purple.text }} />}
            />
          )}

          {currentVr && (
            <ItemDocRow
              title="Verification Report"
              doc={currentVr}
              icon={<VerifiedIcon fontSize="small" sx={{ color: "success.main" }} />}
            />
          )}
        </Stack>
      )}
    </Paper>
  );
}

function ItemDocRow({
  title,
  doc,
  icon,
}: {
  title: string;
  doc: ItemDocumentDto;
  icon: React.ReactNode;
}) {
  return (
    <Paper
      sx={{
        p: 1.25,
        border: "1px solid",
        borderColor: "divider",
        borderRadius: 1.25,
        bgcolor: "background.paper",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 1,
      }}
    >
      <Box sx={{ minWidth: 0, flex: 1 }}>
        <Stack direction="row" spacing={0.75} alignItems="center">
          {icon}
          <Typography variant="body2" sx={{ fontWeight: 700, fontSize: 12, color: "text.primary" }}>
            {title}
          </Typography>
          <Chip label={doc.version} size="small" color="primary" sx={{ fontWeight: 700, fontSize: 10, height: 18 }} />
        </Stack>
        <Typography variant="caption" noWrap component="div" sx={{ color: "text.secondary", fontSize: 11, mt: 0.25 }}>
          {doc.originalFileName}
        </Typography>
      </Box>

      <Stack direction="row" spacing={0.75}>
        <Button
          size="small"
          variant="outlined"
          startIcon={<VisibilityIcon sx={{ fontSize: 14 }} />}
          href={ItemDocumentService.getContentUrl(doc.id)}
          target="_blank"
          rel="noopener noreferrer"
          sx={{ textTransform: "none", fontSize: 11, px: 1, py: 0.25 }}
        >
          View
        </Button>
        <Button
          size="small"
          variant="contained"
          color="primary"
          startIcon={<DownloadIcon sx={{ fontSize: 14 }} />}
          href={ItemDocumentService.getContentUrl(doc.id, true)}
          sx={{ textTransform: "none", fontSize: 11, px: 1, py: 0.25 }}
        >
          Download
        </Button>
      </Stack>
    </Paper>
  );
}
