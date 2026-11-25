import { Box, Paper, Stack, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { SectionTitle } from "../../../components/SectionTitle";
import type { OosSignedTimelineEntryDto } from "../../../services/oosInvestigationService";

interface SignedTimelineProps {
  entries: OosSignedTimelineEntryDto[];
}

export function SignedTimeline({ entries }: SignedTimelineProps) {
  const { t } = useTranslation("oosTracking");

  return (
    <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 1.5 }}>
      <Box sx={{ mb: 1.5 }}>
        <SectionTitle>{t("timeline.title")}</SectionTitle>
      </Box>

      {entries.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          {t("timeline.empty")}
        </Typography>
      ) : (
        <Stack spacing={1.5}>
          {entries.map((entry, idx) => (
            <Box
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
                {t("timeline.entryMeta", {
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
