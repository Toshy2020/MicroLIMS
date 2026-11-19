import { useEffect, useState } from "react";
import { Box, Button, Checkbox, FormControlLabel, Stack, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";

export interface RecoveryCodesPanelProps {
  codes: readonly string[];
  onDone: () => void;
  submitting?: boolean;
  introText?: string;
}

/**
 * Displays single-use MFA recovery codes once, with Copy, Print, and mandatory acknowledgement.
 */
export function RecoveryCodesPanel({
  codes,
  onDone,
  submitting = false,
  introText
}: RecoveryCodesPanelProps) {
  const { t } = useTranslation("account");
  const [savedConfirmed, setSavedConfirmed] = useState(false);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    setSavedConfirmed(false);
    setCopied(false);
  }, [codes]);

  const handleCopy = async () => {
    try {
      await navigator.clipboard?.writeText(codes.join("\n"));
      setCopied(true);
    } catch {
      // Clipboard API may be unavailable in restricted contexts.
    }
  };

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
      <Box>
        <Typography component="h2" variant="h6">
          {t("recoveryCodes.title")}
        </Typography>
        <Typography sx={{ mt: 0.5, fontSize: 14, color: "text.secondary" }}>
          {introText ?? t("recoveryCodes.intro")}
        </Typography>
      </Box>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
          gap: 1,
          p: 2,
          borderRadius: 1,
          border: 1,
          borderColor: "divider",
          bgcolor: "background.default",
          "@media print": {
            bgcolor: "#ffffff",
            color: "#111827",
            borderColor: "#d1d5db"
          }
        }}
      >
        {codes.map((code) => (
          <Typography
            key={code}
            sx={{
              fontFamily: monospaceFontFamily,
              fontVariantNumeric: "tabular-nums",
              fontSize: 14,
              fontWeight: 600,
              "@media print": { color: "#111827" }
            }}
          >
            {code}
          </Typography>
        ))}
      </Box>

      <Stack direction="row" spacing={1} className="no-print">
        <Button type="button" variant="outlined" size="small" onClick={handleCopy}>
          {copied ? t("recoveryCodes.copied") : t("recoveryCodes.copyCodes")}
        </Button>
        <Button type="button" variant="outlined" size="small" onClick={() => window.print()}>
          {t("recoveryCodes.print")}
        </Button>
      </Stack>

      <FormControlLabel
        className="no-print"
        control={
          <Checkbox
            checked={savedConfirmed}
            onChange={(e) => setSavedConfirmed(e.target.checked)}
            disabled={submitting}
          />
        }
        label={t("recoveryCodes.savedLabel")}
      />

      <Button
        type="button"
        className="no-print"
        variant="contained"
        size="large"
        disabled={!savedConfirmed || submitting}
        onClick={onDone}
      >
        {submitting ? t("recoveryCodes.continuing") : t("recoveryCodes.continue")}
      </Button>
    </Box>
  );
}
