import { useState } from "react";
import {
  Autocomplete,
  TextField,
  createFilterOptions,
  Button,
  Stack,
  Alert,
  SxProps,
  Theme
} from "@mui/material";
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
  stringify: (opt) => ("isNew" in opt ? "" : `${opt.code} — ${opt.name} ${opt.code} ${opt.name}`)
});

export function MediaProductPicker({
  value,
  onChange,
  label = "Media Product",
  required = false,
  disabled = false,
  size = "small",
  sx,
  error,
  helperText,
  allowCreate = false,
  options: externalOptions
}: MediaProductPickerProps) {
  const { options: hookOptions, loading, create } = useMediaProducts();
  const options = externalOptions ?? hookOptions;
  const selected = options.find((o) => o.id === value) ?? null;

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogName, setDialogName] = useState("");
  const [dialogCode, setDialogCode] = useState("");
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleCreate = async () => {
    if (!dialogName.trim() || !dialogCode.trim()) {
      setDialogError("Both product name and code are required.");
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
      setDialogError(responseMessage ?? "Failed to create media product.");
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
          if ("isNew" in o) return "+ Add media product";
          return `${o.code} — ${o.name}`;
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
                style={{ ...restProps.style, color: "#1976d2", fontWeight: 600 }}
              >
                + Add media product
              </li>
            );
          }
          return (
            <li key={key ?? option.id} {...restProps}>
              {`${option.code} — ${option.name}`}
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
            placeholder="Select media product"
          />
        )}
      />

      {allowCreate && (
        <FloatingDialog
          open={dialogOpen}
          title="Add Media Product"
          onClose={() => setDialogOpen(false)}
          maxWidth="xs"
          actions={
            <>
              <Button onClick={() => setDialogOpen(false)} disabled={submitting}>
                Cancel
              </Button>
              <Button
                variant="contained"
                onClick={handleCreate}
                disabled={!dialogName.trim() || !dialogCode.trim() || submitting}
              >
                {submitting ? "Adding..." : "Add Product"}
              </Button>
            </>
          }
        >
          <Stack spacing={2} sx={{ mt: 1 }}>
            {dialogError && <Alert severity="error">{dialogError}</Alert>}
            <TextField
              label="Product Name"
              required
              value={dialogName}
              onChange={(e) => setDialogName(e.target.value)}
              placeholder="e.g. Tryptic Soy Agar"
              autoFocus
              size="small"
            />
            <TextField
              label="Product Code"
              required
              value={dialogCode}
              onChange={(e) => setDialogCode(e.target.value)}
              placeholder="e.g. TSA"
              helperText="2-10 characters (letters, digits, dot, hyphen)"
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
