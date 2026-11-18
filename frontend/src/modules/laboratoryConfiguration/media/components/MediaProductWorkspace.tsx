import {
  Paper,
  Box,
  Typography,
  Stack,
  Tabs,
  Tab,
  IconButton,
  Button,
  Chip,
  useTheme,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import KeyIcon from "@mui/icons-material/Key";
import { useTranslation } from "../../../../i18n";
import { monospaceFontFamily } from "../../../../theme/palette";
import {
  MediaConfigurationItem,
  MediaIncubationConditionOption,
  MediaProductOption,
} from "../types/mediaConfigurationTypes";
import { OrganismOption } from "../../../../hooks/useOrganisms";
import { MediaConfigurationsSection } from "./MediaConfigurationsSection";
import { MediaIncubationConditionsSection } from "./MediaIncubationConditionsSection";
import { clickable } from "../../../../utils/clickable";

// Tab order follows setup order: a medium needs an incubation condition
// before its evaluation configuration can pick one.
export const WORKSPACE_TABS = { overview: 0, conditions: 1, configurations: 2 } as const;

interface MediaProductWorkspaceProps {
  product: MediaProductOption;
  conditions: MediaIncubationConditionOption[];
  configurations: MediaConfigurationItem[];
  organisms: OrganismOption[];
  onClose: () => void;
  onUpdated: (successMessage?: string) => void;
  onChangeCode: () => void;
  isManager: boolean;
  isSectionHead: boolean;
  activeTab: number;
  onTabChange: (tab: number) => void;
}

export function MediaProductWorkspace({
  product,
  conditions,
  configurations,
  organisms,
  onClose,
  onUpdated,
  onChangeCode,
  isManager,
  isSectionHead,
  activeTab,
  onTabChange,
}: MediaProductWorkspaceProps) {
  const { t } = useTranslation("labConfig");
  const theme = useTheme();

  const yy = String(new Date().getFullYear()).slice(-2);

  return (
    <Paper
      elevation={0}
      sx={{
        border: "1px solid",
        borderColor: "divider",
        borderRadius: 1.5,
        display: "flex",
        flexDirection: "column",
        height: "100%",
        minHeight: 500,
        bgcolor: "background.paper",
      }}
    >
      {/* Header */}
      <Box
        sx={{
          p: 2,
          borderBottom: "1px solid",
          borderColor: "divider",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start",
          bgcolor: "background.default",
        }}
      >
        <Box>
          <Stack useFlexGap
            direction="row"
            spacing={1}
            sx={{
              alignItems: "center",
              flexWrap: "wrap",
              mb: 0.5,
            }}
          >
            <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16, color: "text.primary" }}>
              {product.name}
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary", fontWeight: 500 }}>
              ({product.code})
            </Typography>
          </Stack>

          <Stack useFlexGap
            direction="row"
            spacing={1}
            sx={{
              alignItems: "center",
              flexWrap: "wrap",
            }}
          >
            <Chip
              label={product.code}
              size="small"
              variant="outlined"
              sx={{ fontFamily: monospaceFontFamily, fontSize: 12, height: 22, fontWeight: 600 }}
            />
            <Chip
              label={t("media.productWorkspace.conditionCount", { count: conditions.length })}
              size="small"
              variant="outlined"
              sx={{ fontSize: 12, height: 22, fontWeight: 500 }}
            />
            <Chip
              label={t("media.productWorkspace.configurationCount", { count: configurations.length })}
              size="small"
              sx={{
                fontSize: 12,
                fontWeight: 600,
                color: theme.custom.status.purple.text,
                bgcolor: theme.custom.status.purple.bg,
                height: 22,
              }}
            />
            <Chip
              label={t("media.productWorkspace.batchCount", { count: product.batchCount })}
              size="small"
              variant="outlined"
              sx={{ fontSize: 12, height: 22, fontWeight: 500 }}
            />
          </Stack>
        </Box>

        <IconButton
          aria-label={t("media.productWorkspace.closeAriaLabel")}
          size="small"
          onClick={onClose}
          title={t("media.productWorkspace.closeTitle")}
        >
          <CloseIcon fontSize="small" />
        </IconButton>
      </Box>

      {/* Tabs */}
      <Box sx={{ borderBottom: 1, borderColor: "divider", px: 2, bgcolor: "background.paper" }}>
        <Tabs
          value={activeTab}
          onChange={(_, val) => onTabChange(val)}
          variant="scrollable"
          scrollButtons="auto"
        >
          <Tab label={t("media.productWorkspace.tabs.overview")} sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }} />
          <Tab
            label={t("media.productWorkspace.tabs.incubationConditions", { count: conditions.length })}
            sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }}
          />
          <Tab
            label={t("media.productWorkspace.tabs.configurations", { count: configurations.length })}
            sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }}
          />
        </Tabs>
      </Box>

      {/* Tab Content */}
      <Box sx={{ p: 2.5, flexGrow: 1, overflowY: "auto" }}>
        {activeTab === WORKSPACE_TABS.overview && (
          <Stack spacing={2.5}>
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                  <Box>
                    <Typography
                      variant="caption"
                      sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}
                    >
                      {t("media.productWorkspace.mediaCodeLabel")}
                    </Typography>
                    <Typography
                      variant="body1"
                      sx={{ fontWeight: 700, mt: 0.5, fontFamily: monospaceFontFamily }}
                    >
                      {product.code}
                    </Typography>
                  </Box>
                  {isSectionHead && (
                    <Button
                      size="small"
                      variant="outlined"
                      startIcon={<KeyIcon fontSize="small" />}
                      onClick={onChangeCode}
                      sx={{ textTransform: "none", fontSize: 12 }}
                    >
                      {t("media.productWorkspace.changeCode")}
                    </Button>
                  )}
                </Box>
              </Paper>

              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Typography
                  variant="caption"
                  sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}
                >
                  {t("media.productWorkspace.lotNumberFormatLabel")}
                </Typography>
                <Typography
                  variant="body1"
                  sx={{ fontWeight: 700, mt: 0.5, fontFamily: monospaceFontFamily }}
                >
                  {`${product.code}/01/${yy}, ${product.code}/02/${yy} ...`}
                </Typography>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 0.5 }}>
                  {t("media.productWorkspace.lotNumberFormatDesc")}
                </Typography>
              </Paper>

              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Typography
                  variant="caption"
                  sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}
                >
                  {t("media.productWorkspace.configurationsLabel")}
                </Typography>
                <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5 }}>
                  {configurations.length}
                </Typography>
              </Paper>

              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Typography
                  variant="caption"
                  sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}
                >
                  {t("media.productWorkspace.stockBatchesLabel")}
                </Typography>
                <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5 }}>
                  {product.batchCount}
                </Typography>
              </Paper>
            </Box>

            <Paper sx={{ p: 2, border: "1px solid", borderColor: "divider" }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                {t("media.productWorkspace.summaryTitle")}
              </Typography>
              <Stack spacing={1}>
                {[
                  {
                    label: t("media.productWorkspace.summaryConditionsLabel"),
                    value: t("media.productWorkspace.summaryConditionsCount", { count: conditions.length }),
                    tab: WORKSPACE_TABS.conditions,
                  },
                  {
                    label: t("media.productWorkspace.summaryConfigurationsLabel"),
                    value: t("media.productWorkspace.summaryConfigurationsCount", { count: configurations.length }),
                    tab: WORKSPACE_TABS.configurations,
                  },
                ].map((row) => (
                  <Box
                    key={row.tab}
                    sx={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                      cursor: "pointer",
                      p: 0.5,
                      borderRadius: 1,
                      "&:hover": { bgcolor: "action.hover" },
                    }}
                    onClick={() => onTabChange(row.tab)}
                    {...clickable(() => onTabChange(row.tab))}
                  >
                    <Typography variant="body2" sx={{ color: "text.secondary" }}>
                      {row.label}
                    </Typography>
                    <Typography variant="body2" sx={{ fontWeight: 600, color: "primary.main" }}>
                      {row.value}
                    </Typography>
                  </Box>
                ))}
              </Stack>
            </Paper>
          </Stack>
        )}

        {activeTab === WORKSPACE_TABS.conditions && (
          <MediaIncubationConditionsSection
            product={product}
            conditions={conditions}
            onUpdated={onUpdated}
            isManager={isManager}
          />
        )}

        {activeTab === WORKSPACE_TABS.configurations && (
          <MediaConfigurationsSection
            product={product}
            conditions={conditions}
            configurations={configurations}
            organisms={organisms}
            onUpdated={onUpdated}
            isManager={isManager}
          />
        )}
      </Box>
    </Paper>
  );
}
