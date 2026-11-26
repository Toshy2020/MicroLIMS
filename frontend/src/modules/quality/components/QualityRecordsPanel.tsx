import { useEffect, useState } from "react";
import { Alert, Box, Button, Chip, Paper, Skeleton, Stack, Typography } from "@mui/material";
import { Link as RouterLink, useInRouterContext } from "react-router-dom";
import { Ltr } from "../../../components/Ltr";
import { SectionTitle } from "../../../components/SectionTitle";
import { useOptionalAuth } from "../../../contexts/AuthContext";
import { useTranslation } from "../../../i18n";
import { PERMISSIONS, APP_ROUTES } from "../../../routes/routes";
import {
  qualityEntryService,
  type QualityRecordRefDto
} from "../../../services/qualityEntryService";
import type { QualityLinkTargetType } from "../../../services/qualityService";
import { monospaceFontFamily } from "../../../theme/palette";
import { serverMessage } from "../../../utils/errorMessage";

export interface QualityRecordsPanelProps {
  targetType: QualityLinkTargetType;
  targetId: number;
  refreshKey?: number;
  compact?: boolean;
}

export function QualityRecordsPanel({
  targetType,
  targetId,
  refreshKey = 0,
  compact = false
}: QualityRecordsPanelProps) {
  const { t } = useTranslation("qualityConfig");
  const auth = useOptionalAuth();
  const canView = auth?.permissions?.includes(PERMISSIONS.QUALITY_VIEW) ?? false;
  const inRouter = useInRouterContext();

  const [records, setRecords] = useState<QualityRecordRefDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (!canView) return;
    const controller = new AbortController();
    setLoading(true);
    setError(null);

    qualityEntryService
      .forTarget(targetType, targetId, 0, 25, controller.signal)
      .then((data) => {
        if (controller.signal.aborted) return;
        setRecords(data);
        setLoading(false);
      })
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        setError(serverMessage(e) ?? t("panel.loadError"));
        setLoading(false);
      });

    return () => controller.abort();
  }, [canView, targetType, targetId, refreshKey, attempt, t]);

  if (!canView) return null;

  return (
    <Paper variant="outlined" sx={{ p: compact ? 1.5 : 2, borderRadius: 1.5 }}>
      <Box sx={{ mb: 1 }}>
        <SectionTitle>{t("panel.title")}</SectionTitle>
      </Box>

      {loading ? (
        <Stack spacing={1}>
          <Skeleton variant="rounded" height={28} />
          <Skeleton variant="rounded" height={28} />
        </Stack>
      ) : error ? (
        <Alert
          severity="error"
          action={
            <Button size="small" color="inherit" onClick={() => setAttempt((a) => a + 1)}>
              {t("panel.retry")}
            </Button>
          }
        >
          {error}
        </Alert>
      ) : records.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          {t("panel.empty")}
        </Typography>
      ) : (
        <Stack spacing={1}>
          {records.map((rec) => {
            const restricted = Boolean(rec.isRestricted) || !rec.title?.trim();
            return (
              <Box
                key={rec.id}
                sx={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  gap: 1.5,
                  py: 0.75,
                  px: 1,
                  borderRadius: 1,
                  border: 1,
                  borderColor: "divider",
                  bgcolor: "background.default"
                }}
              >
                <Box sx={{ display: "flex", alignItems: "center", gap: 1, minWidth: 0, flex: 1 }}>
                  {inRouter ? (
                    <Button
                      component={RouterLink}
                      to={APP_ROUTES.QUALITY_DETAIL(rec.id)}
                      size="small"
                      variant="text"
                      aria-label={t("panel.openAria", { number: rec.number })}
                      sx={{
                        fontFamily: monospaceFontFamily,
                        fontWeight: 700,
                        minWidth: "auto",
                        p: 0.25,
                        textTransform: "none"
                      }}
                    >
                      <Ltr>{rec.number}</Ltr>
                    </Button>
                  ) : (
                    <Button
                      component="a"
                      href={APP_ROUTES.QUALITY_DETAIL(rec.id)}
                      size="small"
                      variant="text"
                      aria-label={t("panel.openAria", { number: rec.number })}
                      sx={{
                        fontFamily: monospaceFontFamily,
                        fontWeight: 700,
                        minWidth: "auto",
                        p: 0.25,
                        textTransform: "none"
                      }}
                    >
                      <Ltr>{rec.number}</Ltr>
                    </Button>
                  )}
                  {restricted ? (
                    <Chip size="small" label={t("panel.restricted")} variant="outlined" />
                  ) : (
                    <Typography variant="body2" noWrap sx={{ minWidth: 0 }}>
                      {rec.title}
                    </Typography>
                  )}
                </Box>
                <Chip
                  size="small"
                  label={t(`config.recordTypes.${rec.type}`)}
                  variant="outlined"
                />
              </Box>
            );
          })}
        </Stack>
      )}
    </Paper>
  );
}
