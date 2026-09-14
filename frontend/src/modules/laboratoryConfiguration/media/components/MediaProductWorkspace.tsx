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
import { monospaceFontFamily } from "../../../../theme/palette";
import {
  MediaConfigurationItem,
  MediaProductOption,
} from "../types/mediaConfigurationTypes";
import { OrganismOption } from "../../../../hooks/useOrganisms";
import { MediaConfigurationsSection } from "./MediaConfigurationsSection";

interface MediaProductWorkspaceProps {
  product: MediaProductOption;
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
          <Stack
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

          <Stack
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
              sx={{ fontFamily: monospaceFontFamily, fontSize: 11, height: 22, fontWeight: 600 }}
            />
            <Chip
              label={`${configurations.length} ${configurations.length === 1 ? "Configuration" : "Configurations"}`}
              size="small"
              sx={{
                fontSize: 11,
                fontWeight: 600,
                color: theme.custom.status.purple.text,
                bgcolor: theme.custom.status.purple.bg,
                height: 22,
              }}
            />
            <Chip
              label={`${product.batchCount} ${product.batchCount === 1 ? "Batch" : "Batches"}`}
              size="small"
              variant="outlined"
              sx={{ fontSize: 11, height: 22, fontWeight: 500 }}
            />
          </Stack>
        </Box>

        <IconButton size="small" onClick={onClose} title="Close Workspace">
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
          <Tab label="Overview" sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }} />
          <Tab
            label={`Configurations (${configurations.length})`}
            sx={{ textTransform: "none", fontWeight: 600, fontSize: 13 }}
          />
        </Tabs>
      </Box>

      {/* Tab Content */}
      <Box sx={{ p: 2.5, flexGrow: 1, overflowY: "auto" }}>
        {/* Tab 0: Overview */}
        {activeTab === 0 && (
          <Stack spacing={2.5}>
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                  <Box>
                    <Typography
                      variant="caption"
                      sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}
                    >
                      Media code
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
                      Change code
                    </Button>
                  )}
                </Box>
              </Paper>

              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Typography
                  variant="caption"
                  sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}
                >
                  Lot number format
                </Typography>
                <Typography
                  variant="body1"
                  sx={{ fontWeight: 700, mt: 0.5, fontFamily: monospaceFontFamily }}
                >
                  {`${product.code}/01/${yy}, ${product.code}/02/${yy} ...`}
                </Typography>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 0.5 }}>
                  Every lot prepared from this medium is numbered with its code.
                </Typography>
              </Paper>

              <Paper sx={{ p: 2, bgcolor: "background.default", border: "1px solid", borderColor: "divider" }}>
                <Typography
                  variant="caption"
                  sx={{ color: "text.secondary", fontWeight: 600, textTransform: "uppercase" }}
                >
                  Configurations
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
                  Stock batches
                </Typography>
                <Typography variant="body1" sx={{ fontWeight: 700, mt: 0.5 }}>
                  {product.batchCount}
                </Typography>
              </Paper>
            </Box>

            <Paper sx={{ p: 2, border: "1px solid", borderColor: "divider" }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                Configuration Summary
              </Typography>
              <Stack spacing={1}>
                <Box
                  sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    cursor: "pointer",
                    p: 0.5,
                    borderRadius: 1,
                    "&:hover": { bgcolor: "action.hover" },
                  }}
                  onClick={() => onTabChange(1)}
                >
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    Configurations:
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600, color: "primary.main" }}>
                    {configurations.length} {configurations.length === 1 ? "configuration" : "configurations"} defined →
                  </Typography>
                </Box>
              </Stack>
            </Paper>
          </Stack>
        )}

        {/* Tab 1: Configurations */}
        {activeTab === 1 && (
          <MediaConfigurationsSection
            product={product}
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
