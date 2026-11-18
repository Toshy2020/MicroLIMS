import { Alert, Box, Typography, Stack, Chip, Divider, useTheme } from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import AccessTimeOutlinedIcon from "@mui/icons-material/AccessTimeOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import { SharedTsbStateDto } from "../types/pathogenSessionTypes";
import { brandColors } from "../../../theme";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { Ltr } from "../../../components/Ltr";

interface Props {
  sharedTsb: SharedTsbStateDto;
}

const formatDate = (d: string | null | undefined) =>
  d
    ? formatDateTimeWithTimeZone(d)
    : "—";

export function SharedTsbStatusCard({ sharedTsb }: Props) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();

  if (!sharedTsb.isStarted) {
    return (
      <Box
        sx={{
          p: 2.5,
          borderRadius: 2,
          border: "1px dashed",
          borderColor: "divider",
          bgcolor: "background.default",
          textAlign: "center"
        }}
      >
        <Typography sx={{ fontSize: 13, color: "text.secondary", fontWeight: 500 }}>
          {t("pathogenSession.tsbStatus.notStarted")}
        </Typography>
      </Box>
    );
  }

  return (
    <Box
      sx={{
        p: 2.5,
        borderRadius: 2,
        border: "1px solid",
        borderColor: "divider",
        bgcolor: theme.custom.status.purple.bg
      }}
    >
      <Stack
        direction="row"
        sx={{
          alignItems: "center",
          justifyContent: "space-between",
          mb: 2
        }}>
        <Stack direction="row" spacing={1.5} sx={{
          alignItems: "center"
        }}>
          <Box
            sx={{
              width: 36,
              height: 36,
              borderRadius: 1.5,
              bgcolor: brandColors.sectionTitle,
              color: "common.white",
              display: "flex",
              alignItems: "center",
              justifyContent: "center"
            }}
          >
            <ScienceOutlinedIcon sx={{ fontSize: 20 }} />
          </Box>
          <Box>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: "text.primary" }}>
              {t("pathogenSession.tsbStatus.title")}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              {t("pathogenSession.tsbStatus.subtitle")}
            </Typography>
          </Box>
        </Stack>

        <Chip
          icon={sharedTsb.isCompleted ? <CheckCircleOutlineIcon /> : <AccessTimeOutlinedIcon />}
          label={
            sharedTsb.isCompleted
              ? t("pathogenSession.tsbStatus.statusCompleted")
              : sharedTsb.windowNotConfigured
              ? t("pathogenSession.tsbStatus.statusWindowNotConfigured")
              : t("pathogenSession.tsbStatus.statusIncubating")
          }
          size="small"
          sx={{
            fontWeight: 700,
            fontSize: 12,
            bgcolor: sharedTsb.isCompleted ? theme.custom.status.notDetected.bg : theme.custom.status.info.bg,
            color: sharedTsb.isCompleted ? theme.custom.status.notDetected.text : theme.custom.status.info.text,
            border: `1px solid ${sharedTsb.isCompleted ? theme.custom.status.notDetected.border : theme.custom.status.info.border}`
          }}
        />
      </Stack>

      <Divider sx={{ my: 1.5, borderColor: theme.custom.status.purple.border }} />

      {sharedTsb.windowNotConfigured && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {t("pathogenSession.tsbStatus.windowNotConfiguredAlert")}
        </Alert>
      )}

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(5, 1fr)" },
          gap: 2
        }}
      >
        <Box>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: theme.custom.status.purple.text, textTransform: "uppercase" }}>
            {t("pathogenSession.tsbStatus.tsbMediaLot")}
          </Typography>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
            <Ltr>{sharedTsb.mediaLotNumber ?? "—"}</Ltr>
          </Typography>
          <Stack
            direction="row"
            spacing={0.5}
            sx={{
              alignItems: "center",
              mt: 0.5
            }}>
            <VerifiedUserOutlinedIcon sx={{ fontSize: 13, color: theme.custom.status.notDetected.text }} />
            <Typography sx={{ fontSize: 12, color: theme.custom.status.notDetected.text, fontWeight: 600 }}>
              {sharedTsb.gptStatus ?? t("pathogenSession.tsbStatus.gptConformFallback")}
            </Typography>
          </Stack>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: theme.custom.status.purple.text, textTransform: "uppercase" }}>
            {t("pathogenSession.tsbStatus.incubatorEquipment")}
          </Typography>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
            <Ltr>{sharedTsb.incubatorCode ?? "—"}</Ltr>
          </Typography>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            <Ltr>{sharedTsb.temperature ?? "—"}</Ltr>
          </Typography>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: theme.custom.status.purple.text, textTransform: "uppercase" }}>
            {t("pathogenSession.tsbStatus.incubationStart")}
          </Typography>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
            <Ltr>{formatDate(sharedTsb.actualStartUtc)}</Ltr>
          </Typography>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            <Trans
              i18nKey="pathogenSession.tsbStatus.byUser"
              ns="testingWorkspace"
              values={{ name: sharedTsb.startedByUserName ?? t("pathogenSession.tsbStatus.analystFallback") }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: theme.custom.status.purple.text, textTransform: "uppercase" }}>
            {t("pathogenSession.tsbStatus.availableFrom")}
          </Typography>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.custom.status.notDetected.text }}>
            <Ltr>{formatDate(sharedTsb.minReadyAt)}</Ltr>
          </Typography>
          <Typography sx={{ fontSize: 12, color: theme.custom.status.notDetected.text, fontWeight: 600 }}>
            <Trans
              i18nKey="pathogenSession.tsbStatus.unlockPoint"
              ns="testingWorkspace"
              values={{ range: sharedTsb.requiredDurationRange ?? t("pathogenSession.tsbStatus.notConfiguredRange") }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: theme.custom.status.purple.text, textTransform: "uppercase" }}>
            {t("pathogenSession.tsbStatus.expectedWindow")}
          </Typography>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
            <Ltr>{formatDate(sharedTsb.expectedCompletionUtc)}</Ltr>
          </Typography>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            {sharedTsb.incubationDurationHours != null ? (
              <Trans
                i18nKey="pathogenSession.tsbStatus.maxDuration"
                ns="testingWorkspace"
                values={{ hours: sharedTsb.incubationDurationHours }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            ) : (
              t("pathogenSession.tsbStatus.maxDurationEmpty")
            )}
          </Typography>
        </Box>
      </Box>

      <Box sx={{ mt: 2, p: 1.5, bgcolor: "background.paper", borderRadius: 1.5, border: "1px solid", borderColor: theme.custom.status.purple.border }}>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          <Trans
            i18nKey="pathogenSession.tsbStatus.scopeFooter"
            ns="testingWorkspace"
            values={{
              locations: sharedTsb.applicableLocationCount,
              tests: sharedTsb.applicableTestCodes.length,
              codes: sharedTsb.applicableTestCodes.join(", ")
            }}
            components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
          />
        </Typography>
      </Box>
    </Box>
  );
}
