import { ToggleButton, ToggleButtonGroup, Tooltip } from "@mui/material";
import { useTheme } from "@mui/material/styles";
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
          ? "Compact density (disabled in bench mode)"
          : "Compact density"
      }
    >
      Compact
    </ToggleButton>
  );

  return (
    <ToggleButtonGroup
      size="small"
      exclusive
      value={value}
      aria-label="Row density"
      onChange={(_, next: "comfortable" | "compact" | null) => {
        if (!next) return;
        if (isBench && next === "compact") return;
        onChange(next);
      }}
    >
      <ToggleButton value="comfortable" aria-label="Comfortable density">
        Comfortable
      </ToggleButton>
      {isBench ? (
        <Tooltip title="Bench mode requires comfortable row spacing">
          <span>{compactButton}</span>
        </Tooltip>
      ) : (
        compactButton
      )}
    </ToggleButtonGroup>
  );
}
