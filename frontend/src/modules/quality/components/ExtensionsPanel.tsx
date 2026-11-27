import { Box, Button, List, ListItem, Stack, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { MetaBadge, ToneBadge } from "../../../components/StatusBadge";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import type { QualityActionDetailDto, QualityExtensionDto } from "../../../services/qualityService";
import { GatedButton } from "../steps/stepKit";
import { PanelFrame } from "./PanelFrame";

interface ExtensionsPanelProps {
  // null when the requests could not be loaded (the page shows the partial-outage banner).
  extensions: QualityExtensionDto[] | null;
  actions: QualityActionDetailDto[];
  // The owner of an open record may ask for a later due date of the record.
  canRequest: boolean;
  // Quality.Approve: decide a pending request with a signature.
  canDecide: boolean;
  currentUserId?: number | null;
  raisedByUserId?: number | null;
  ownerUserId?: number | null;
  onRequest: () => void;
  onDecide: (extension: QualityExtensionDto) => void;
}

export function ExtensionsPanel({
  extensions,
  actions,
  canRequest,
  canDecide,
  currentUserId = null,
  raisedByUserId = null,
  ownerUserId = null,
  onRequest,
  onDecide
}: ExtensionsPanelProps) {
  const { t } = useTranslation("quality");

  const scopeOf = (extension: QualityExtensionDto): string => {
    if (extension.actionId === null) return t("extensions.forRecord");
    const action = actions.find((a) => a.id === extension.actionId);
    return action
      ? t("extensions.forAction", { description: action.description })
      : t("extensions.forActionId", { id: extension.actionId });
  };

  return (
    <PanelFrame
      title={t("extensions.title")}
      action={
        canRequest ? (
          <Button size="small" variant="outlined" onClick={onRequest}>
            {t("extensions.request")}
          </Button>
        ) : undefined
      }
    >
      {extensions === null ? (
        <Typography variant="body2" color="text.secondary">
          {t("extensions.unavailable")}
        </Typography>
      ) : extensions.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          {t("extensions.empty")}
        </Typography>
      ) : (
        <List disablePadding>
          {extensions.map((extension) => {
            const decided = extension.decision !== null;
            return (
              <ListItem key={extension.id} divider sx={{ display: "block", px: 0 }}>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", mb: 0.5 }}>
                  {decided ? (
                    <ToneBadge
                      tone={extension.decision === "Approved" ? "notDetected" : "detected"}
                      label={t(`extensions.${extension.decision as "Approved" | "Refused"}`)}
                    />
                  ) : (
                    <ToneBadge tone="action" label={t("extensions.pending")} />
                  )}
                  <MetaBadge label={scopeOf(extension)} />
                  <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                    {t("extensions.requestedFor", { date: extension.requestedDueDate })}
                  </Typography>
                </Stack>
                <Typography variant="body2">{extension.reason}</Typography>
                <Typography variant="caption" color="text.secondary" sx={{ display: "block", fontVariantNumeric: "tabular-nums" }}>
                  {t("extensions.requestedAt", {
                    name: extension.requestedByName,
                    date: formatDateTimeWithTimeZone(extension.requestedAtUtc)
                  })}
                </Typography>
                {decided && extension.decidedAtUtc && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block", fontVariantNumeric: "tabular-nums" }}>
                    {extension.decidedByName
                      ? t("extensions.decidedLine", {
                          decision: t(`extensions.${extension.decision as "Approved" | "Refused"}`),
                          name: extension.decidedByName,
                          date: formatDateTimeWithTimeZone(extension.decidedAtUtc)
                        })
                      : t("extensions.decidedLineNoName", {
                          decision: t(`extensions.${extension.decision as "Approved" | "Refused"}`),
                          date: formatDateTimeWithTimeZone(extension.decidedAtUtc)
                        })}
                  </Typography>
                )}
                {extension.decisionComment && (
                  <Typography variant="body2" sx={{ mt: 0.25 }}>
                    {extension.decisionComment}
                  </Typography>
                )}
                {!decided && canDecide && (
                  <Box sx={{ mt: 1 }}>
                    <GatedButton
                      size="small"
                      variant="contained"
                      label={t("extensions.decide")}
                      aria-label={t("extensions.decideAria", { date: extension.requestedDueDate })}
                      lockedReason={
                        currentUserId !== null && extension.requestedByUserId === currentUserId
                          ? t("extensions.selfDecision")
                          : currentUserId !== null && (raisedByUserId === currentUserId || ownerUserId === currentUserId)
                            ? t("steps.common.raiserOrOwner")
                            : null
                      }
                      onClick={() => onDecide(extension)}
                    />
                  </Box>
                )}
              </ListItem>
            );
          })}
        </List>
      )}
    </PanelFrame>
  );
}
