import { useState } from "react";
import { Alert, Button, CircularProgress, Snackbar, Stack, SxProps, Theme } from "@mui/material";
import VisibilityIcon from "@mui/icons-material/Visibility";
import DownloadIcon from "@mui/icons-material/Download";
import { ItemDocumentService, ItemDocumentDto } from "../modules/laboratoryConfiguration/items/services/ItemDocumentService";

interface Props {
  doc: ItemDocumentDto;
  // Each host card sizes its own buttons; the behaviour behind them is shared
  // so the three of them cannot drift apart again.
  buttonSx?: SxProps<Theme>;
  iconSize?: number;
  spacing?: number;
}

// View/Download for a controlled item document.
//
// These used to be plain <a href> links straight at the content endpoint.
// That endpoint is [Authorize]d and the JWT is attached by the apiClient
// request interceptor as an Authorization header - which a browser
// navigation cannot carry, so every click arrived unauthenticated and was
// rejected. The bytes are fetched through apiClient and handed to the
// browser as a blob instead, the same way material, equipment and
// document-control downloads already work.
export function ItemDocumentActionButtons({ doc, buttonSx, iconSize, spacing = 1 }: Props) {
  const [busy, setBusy] = useState<"view" | "download" | null>(null);
  const [error, setError] = useState<string | null>(null);

  const iconProps = iconSize ? { sx: { fontSize: iconSize } } : { fontSize: "small" as const };

  const run = async (mode: "view" | "download", action: () => Promise<void>) => {
    setBusy(mode);
    setError(null);
    try {
      await action();
    } catch (e: any) {
      setError(
        e?.response?.status === 404
          ? "This document is no longer available."
          : e?.response?.data?.message || "Could not retrieve the document. Please try again."
      );
    } finally {
      setBusy(null);
    }
  };

  return (
    <>
      <Stack direction="row" spacing={spacing}>
        <Button
          size="small"
          variant="outlined"
          disabled={busy !== null}
          startIcon={busy === "view" ? <CircularProgress size={12} /> : <VisibilityIcon {...iconProps} />}
          onClick={() => run("view", () => ItemDocumentService.openDocument(doc.id))}
          sx={buttonSx}
        >
          View
        </Button>
        <Button
          size="small"
          variant="contained"
          color="primary"
          disabled={busy !== null}
          startIcon={busy === "download" ? <CircularProgress size={12} /> : <DownloadIcon {...iconProps} />}
          onClick={() => run("download", () => ItemDocumentService.downloadDocument(doc.id, doc.originalFileName))}
          sx={buttonSx}
        >
          Download
        </Button>
      </Stack>

      <Snackbar
        open={Boolean(error)}
        autoHideDuration={5000}
        onClose={() => setError(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
      >
        <Alert severity="error" onClose={() => setError(null)} sx={{ borderRadius: 1.5 }}>
          {error}
        </Alert>
      </Snackbar>
    </>
  );
}
