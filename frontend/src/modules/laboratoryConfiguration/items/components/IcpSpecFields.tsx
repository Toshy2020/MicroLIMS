import {
  Box,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Typography
} from "@mui/material";
import { useTranslation } from "../../../../i18n";

export interface IcpSpecElementOption {
  id: number;
  symbol: string;
  targetPpm?: number | null;
}

export interface IcpSpecFieldsProps {
  // What the test reports, from the Test Master.
  mode: "Assay" | "Impurity" | null;
  elements: IcpSpecElementOption[];
  selectedElementId: number | "";
  onElementChange: (id: number, element: IcpSpecElementOption | undefined) => void;
}

// The ICP part of an item specification: pick one of the test's elements. The test already defines the
// target concentration (assay) and the basis, so the item only needs the element and its limits.
export function IcpSpecFields({ mode, elements, selectedElementId, onElementChange }: IcpSpecFieldsProps) {
  const { t } = useTranslation("labConfig");
  const isImpurity = mode === "Impurity";

  return (
    <Box
      sx={{
        border: "1px solid",
        borderColor: "primary.main",
        borderRadius: 1,
        p: 2,
        bgcolor: "action.hover"
      }}
    >
      <Typography
        variant="caption"
        sx={{
          fontWeight: 700,
          letterSpacing: "0.5px",
          color: "primary.main",
          textTransform: "uppercase",
          display: "block",
          mb: 1.5
        }}
      >
        {isImpurity ? t("items.icpSpecFields.impurityHeading") : t("items.icpSpecFields.assayHeading")}
      </Typography>

      <FormControl size="small" fullWidth required>
        <InputLabel id="icp-spec-element-label">{t("items.icpSpecFields.elementLabel")}</InputLabel>
        <Select
          labelId="icp-spec-element-label"
          label={t("items.icpSpecFields.elementLabel")}
          value={selectedElementId}
          onChange={(e) => {
            const id = Number(e.target.value);
            onElementChange(id, elements.find((el) => el.id === id));
          }}
          inputProps={{ "aria-label": t("items.icpSpecFields.elementAriaLabel") }}
        >
          {elements.length === 0 ? (
            <MenuItem disabled value="">
              <em>{t("items.icpSpecFields.noElements")}</em>
            </MenuItem>
          ) : (
            elements.map((el) => (
              <MenuItem key={el.id} value={el.id}>
                {el.symbol}
                {!isImpurity && el.targetPpm != null ? t("items.icpSpecFields.targetPpm", { ppm: el.targetPpm }) : ""}
              </MenuItem>
            ))
          )}
        </Select>
        <FormHelperText>
          {isImpurity ? t("items.icpSpecFields.impurityHelper") : t("items.icpSpecFields.assayHelper")}
        </FormHelperText>
      </FormControl>
    </Box>
  );
}
