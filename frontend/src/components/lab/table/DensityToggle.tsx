import { ToggleButton, ToggleButtonGroup, Tooltip } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { useTranslation } from "../../../i18n";
import { useBenchMode } from "../../../theme/BenchModeContext";

export interface DensityToggleProps {
  density: "comfortable" | "compact";
  onChange: (next: "comfortable" | "compact") => void;
  benchMode?: boolean;
}

export function DensityToggle({
  density,
  onChange,
  benchMode: benchOverride,
}: DensityToggleProps) {
  const { t } = useTranslation();
  const theme = useTheme();
  const { benchMode: ctxBench } = useBenchMode();
  const isBench = benchOverride ?? (ctxBench || Boolean(theme.custom?.bench));
  const value = isBench ? "comfortable" : density;

  const compactButton = (
    <ToggleButton
      value="compact"
      disabled={isBench}
      aria-label={
        isBench
          ? t("components.densityToggle.compactDisabledAria")
          : t("components.densityToggle.compactAria")
      }
    >
      {t("components.densityToggle.compact")}
    </ToggleButton>
  );

  return (
    <ToggleButtonGroup
      size="small"
      exclusive
      value={value}
      aria-label={t("components.densityToggle.rowDensityAria")}
      onChange={(_, next: "comfortable" | "compact" | null) => {
        if (!next) return;
        if (isBench && next === "compact") return;
        onChange(next);
      }}
    >
      <ToggleButton value="comfortable" aria-label={t("components.densityToggle.comfortableAria")}>
        {t("components.densityToggle.comfortable")}
      </ToggleButton>
      {isBench ? (
        <Tooltip title={t("components.densityToggle.compactDisabledTooltip")}>
          <span>{compactButton}</span>
        </Tooltip>
      ) : (
        compactButton
      )}
    </ToggleButtonGroup>
  );
}
