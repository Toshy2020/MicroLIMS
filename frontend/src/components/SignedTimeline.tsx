import { Box, Paper, Stack, Typography } from "@mui/material";
import { useTranslation } from "../i18n";
import { SectionTitle } from "./SectionTitle";

// What every signed step shows (21 CFR Part 11 section 11.50): the signer's printed full name, the time of
// signing with its time zone, and the meaning of the signature. The server builds all of it; both the OOS and
// the quality timeline DTOs satisfy this shape.
export interface SignedTimelineEntry {
  signerFullName: string;
  role: string;
  signedAtUtc: string;
  signedAtUtcText: string;
  meaning: string;
  meaningText: string;
  comment: string | null;
}

interface SignedTimelineProps {
  entries: SignedTimelineEntry[];
  // Defaults to "Signed Timeline".
  title?: string;
  // Defaults to "No electronic signatures recorded yet."
  emptyText?: string;
}

export function SignedTimeline({ entries, title, emptyText }: SignedTimelineProps) {
  const { t } = useTranslation("common");

  return (
    <Paper variant="outlined" component="section" aria-label={title ?? t("components.signedTimeline.title")} sx={{ p: 2.5, borderRadius: 1.5 }}>
      <Box sx={{ mb: 1.5 }}>
        <SectionTitle>{title ?? t("components.signedTimeline.title")}</SectionTitle>
      </Box>

      {entries.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          {emptyText ?? t("components.signedTimeline.empty")}
        </Typography>
      ) : (
        <Stack spacing={1.5} component="ol" sx={{ listStyle: "none", m: 0, p: 0 }}>
          {entries.map((entry, idx) => (
            <Box
              component="li"
              key={`${entry.meaning}-${entry.signedAtUtc}-${idx}`}
              sx={{
                p: 1.5,
                borderLeft: 3,
                borderColor: "primary.main",
                bgcolor: "background.default",
                borderRadius: 1
              }}
            >
              <Typography variant="body2" sx={{ fontWeight: 700 }}>
                {entry.meaningText}
              </Typography>
              <Typography
                variant="caption"
                color="text.secondary"
                sx={{ display: "block", fontVariantNumeric: "tabular-nums" }}
              >
                {t("components.signedTimeline.entryMeta", {
                  name: entry.signerFullName,
                  role: entry.role,
                  date: entry.signedAtUtcText
                })}
              </Typography>
              {entry.comment && (
                <Typography variant="body2" sx={{ mt: 0.5 }}>
                  {entry.comment}
                </Typography>
              )}
            </Box>
          ))}
        </Stack>
      )}
    </Paper>
  );
}
