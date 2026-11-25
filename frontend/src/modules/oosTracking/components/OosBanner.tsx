import { Alert, Box, Button, Link as MuiLink, Stack, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import type { OosInvestigationListItemDto } from "../../../services/oosInvestigationService";

export interface OosBannerProps {
  investigations: OosInvestigationListItemDto[];
  blockReason?: string | null;
  canOpenManually?: boolean;
  openingInvestigation?: boolean;
  openError?: string | null;
  onOpenInvestigation?: () => void;
}

/**
 * Displays open/related OOS investigations with direct links, any server-side
 * approval or certificate block reason, and an optional "Open investigation"
 * action when approval is blocked without an existing investigation.
 */
export function OosBanner({
  investigations,
  blockReason,
  canOpenManually = false,
  openingInvestigation = false,
  openError,
  onOpenInvestigation
}: OosBannerProps) {
  const { t } = useTranslation("oosTracking");

  const hasInvestigations = investigations.length > 0;
  const showManualOpen = !hasInvestigations && canOpenManually && Boolean(onOpenInvestigation);
  if (!hasInvestigations && !blockReason && !showManualOpen) {
    return null;
  }

  const hasOpen = investigations.some((inv) => inv.status !== "Concluded");
  const severity = blockReason || hasOpen ? "warning" : "info";

  return (
    <Stack spacing={1} data-testid="oos-banner">
      <Alert severity={severity}>
        <Stack spacing={0.75}>
          {blockReason && (
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {blockReason}
            </Typography>
          )}
          {hasInvestigations && (
            <Stack spacing={0.5}>
              {investigations.map((inv) => (
                <Box
                  key={inv.id}
                  sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}
                >
                  <MuiLink
                    component={RouterLink}
                    to={`/oos-tracking/${inv.id}`}
                    sx={{ fontFamily: monospaceFontFamily, fontWeight: 700, fontSize: 13 }}
                  >
                    {inv.number}
                  </MuiLink>
                  <Typography variant="caption" color="text.secondary">
                    {inv.sectionName} · {inv.status}
                  </Typography>
                </Box>
              ))}
            </Stack>
          )}
          {!hasInvestigations && canOpenManually && onOpenInvestigation && (
            <Box>
              <Button
                variant="outlined"
                color="warning"
                size="small"
                disabled={openingInvestigation}
                onClick={onOpenInvestigation}
              >
                {openingInvestigation
                  ? t("banner.opening", { defaultValue: "Opening..." })
                  : t("banner.openInvestigation", { defaultValue: "Open investigation" })}
              </Button>
            </Box>
          )}
        </Stack>
      </Alert>
      {openError && (
        <Alert severity="error" role="alert">
          {openError}
        </Alert>
      )}
    </Stack>
  );
}
