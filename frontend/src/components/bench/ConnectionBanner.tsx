import { useState } from "react";
import { Box, Button, Typography, useTheme } from "@mui/material";
import WifiOffIcon from "@mui/icons-material/WifiOff";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";

interface ConnectionBannerProps {
  online: boolean;
  // Unsaved entries kept on this tablet for the signed-in user.
  unsavedCount: number;
}

export const OFFLINE_MESSAGE =
  "No connection. Your entries are not saved to MicroLIMS. They are kept on this tablet until you can save.";

// Tells the analyst, in words and an icon (never colour alone), that nothing is
// being saved to the server. After a drop it announces the reconnection and the
// number of unsaved entries; it never sends or saves anything itself.
export function ConnectionBanner({ online, unsavedCount }: ConnectionBannerProps) {
  const theme = useTheme();
  const [sawOffline, setSawOffline] = useState(!online);
  const [dismissed, setDismissed] = useState(false);
  if (!online && !sawOffline) {
    setSawOffline(true);
    setDismissed(false);
  }

  const restored = online && sawOffline && !dismissed;
  const warn = theme.custom.status.inconclusive;
  const ok = theme.custom.status.notDetected;

  return (
    <>
      {!online && (
        <Box
          role="alert"
          className="no-print"
          sx={{
            display: "flex",
            alignItems: "center",
            gap: 1.5,
            px: 2,
            py: 1,
            bgcolor: warn.bg,
            color: warn.text,
            borderBottom: `1px solid ${warn.border}`
          }}
        >
          <WifiOffIcon fontSize="small" aria-hidden />
          <Typography component="span" variant="body2" sx={{ fontWeight: 600 }}>
            {OFFLINE_MESSAGE}
          </Typography>
        </Box>
      )}
      <Box aria-live="polite" className="no-print" sx={restored ? undefined : { position: "absolute", width: 1, height: 1, overflow: "hidden", clip: "rect(0 0 0 0)" }}>
        {restored && (
          <Box
            role="status"
            sx={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              gap: 1.5,
              px: 2,
              py: 1,
              bgcolor: ok.bg,
              color: ok.text,
              borderBottom: `1px solid ${ok.border}`
            }}
          >
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
              <CheckCircleOutlinedIcon fontSize="small" aria-hidden />
              <Typography component="span" variant="body2" sx={{ fontWeight: 600 }}>
                {unsavedCount > 0
                  ? `Connection restored. You have ${unsavedCount} unsaved ${unsavedCount === 1 ? "entry" : "entries"}.`
                  : "Connection restored."}
              </Typography>
            </Box>
            <Button size="small" color="inherit" onClick={() => setDismissed(true)}>
              Dismiss
            </Button>
          </Box>
        )}
      </Box>
    </>
  );
}
