import { useState } from "react";
import {
  Autocomplete,
  TextField,
  createFilterOptions,
  Button,
  Stack,
  Alert,
  SxProps,
  Theme,
  useTheme
} from "@mui/material";
import { useTranslation } from "../i18n";
import { FloatingDialog } from "./FloatingDialog";
import { useMediaProducts, MediaProductOption } from "../hooks/useMediaProducts";

export interface MediaProductPickerProps {
  value: number | null;
  onChange: (id: number | null, product: MediaProductOption | null) => void;
  label?: string;
  required?: boolean;
  disabled?: boolean;
  size?: "small" | "medium";
  sx?: SxProps<Theme>;
  error?: boolean;
  helperText?: React.ReactNode;
  allowCreate?: boolean;
  options?: MediaProductOption[];
}

type Option = MediaProductOption | { isNew: true; inputValue?: string };

const filter = createFilterOptions<Option>({
  stringify: (opt) => ("isNew" in opt ? "" : `${opt.code}: ${opt.name} ${opt.code} ${opt.name}`)
});

export function MediaProductPicker({
  value,
  onChange,
  label: labelProp,
  required = false,
  disabled = false,
  size = "small",
  sx,
  error,
  helperText,
  allowCreate = false,
  options: externalOptions
}: MediaProductPickerProps) {
  const theme = useTheme();
  const { t } = useTranslation();
  const label = labelProp ?? t("components.mediaProductPicker.label");
  const { options: hookOptions, loading, create } = useMediaProducts();
  // Inactive products can't be used for new lots; one already chosen (an old lot being edited) stays visible.
  const options = (externalOptions ?? hookOptions).filter((o) => o.isActive !== false || o.id === value);
  const selected = options.find((o) => o.id === value) ?? null;

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogName, setDialogName] = useState("");
  const [dialogCode, setDialogCode] = useState("");
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleCreate = async () => {
    if (!dialogName.trim() || !dialogCode.trim()) {
      setDialogError(t("components.mediaProductPicker.bothRequired"));
      return;
    }

    setSubmitting(true);
    setDialogError(null);
    try {
      const created = await create(dialogName.trim(), dialogCode.trim());
      onChange(created.id, created);
      setDialogOpen(false);
      setDialogName("");
      setDialogCode("");
    } catch (err: unknown) {
      const responseMessage = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setDialogError(responseMessage ?? t("components.mediaProductPicker.createFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Autocomplete<Option, false, false, false>
        size={size}
        disabled={disabled}
        loading={loading}
        sx={sx}
        options={options}
        value={selected}
        getOptionLabel={(o) => {
          if (typeof o === "string") return o;
          if ("isNew" in o) return t("components.mediaProductPicker.addOption");
          return `${o.code}: ${o.name}`;
        }}
        isOptionEqualToValue={(o, v) => {
          if (!v || "isNew" in o || "isNew" in v) return false;
          return o.id === v.id;
        }}
        filterOptions={(opts, params) => {
          const filtered = filter(opts, params);
          if (allowCreate) {
            filtered.push({ isNew: true, inputValue: params.inputValue });
          }
          return filtered;
        }}
        onChange={(_e, newValue) => {
          if (!newValue) {
            onChange(null, null);
            return;
          }
          if ("isNew" in newValue) {
            setDialogName(newValue.inputValue || "");
            setDialogCode("");
            setDialogError(null);
            setDialogOpen(true);
            return;
          }
          onChange(newValue.id, newValue);
        }}
        renderOption={(props, option) => {
          const { key, ...restProps } = props;
          if ("isNew" in option) {
            return (
              <li
                key="add-new-media-product"
                {...restProps}
                style={{ ...restProps.style, color: theme.palette.primary.main, fontWeight: 600 }}
              >
                {t("components.mediaProductPicker.addOption")}
              </li>
            );
          }
          return (
            <li key={key ?? option.id} {...restProps}>
              {`${option.code}: ${option.name}`}
            </li>
          );
        }}
        renderInput={(params) => (
          <TextField
            {...params}
            label={label}
            required={required}
            error={error}
            helperText={helperText}
            placeholder={t("components.mediaProductPicker.placeholder")}
          />
        )}
      />

      {allowCreate && (
        <FloatingDialog
          open={dialogOpen}
          title={t("components.mediaProductPicker.dialogTitle")}
          onClose={() => setDialogOpen(false)}
          maxWidth="xs"
          actions={
            <>
              <Button onClick={() => setDialogOpen(false)} disabled={submitting}>
                {t("components.mediaProductPicker.cancel")}
              </Button>
              <Button
                variant="contained"
                onClick={handleCreate}
                disabled={!dialogName.trim() || !dialogCode.trim() || submitting}
              >
                {submitting ? t("components.mediaProductPicker.adding") : t("components.mediaProductPicker.addProduct")}
              </Button>
            </>
          }
        >
          <Stack spacing={2} sx={{ mt: 1 }}>
            {dialogError && <Alert severity="error">{dialogError}</Alert>}
            <TextField
              label={t("components.mediaProductPicker.productName")}
              required
              value={dialogName}
              onChange={(e) => setDialogName(e.target.value)}
              placeholder={t("components.mediaProductPicker.nameExample")}
              autoFocus
              size="small"
            />
            <TextField
              label={t("components.mediaProductPicker.productCode")}
              required
              value={dialogCode}
              onChange={(e) => setDialogCode(e.target.value)}
              placeholder={t("components.mediaProductPicker.codeExample")}
              helperText={t("components.mediaProductPicker.codeHelper")}
              size="small"
              onKeyDown={(e) => {
                if (e.key === "Enter" && dialogName.trim() && dialogCode.trim() && !submitting) {
                  handleCreate();
                }
              }}
            />
          </Stack>
        </FloatingDialog>
      )}
    </>
  );
}
