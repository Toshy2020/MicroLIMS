import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  FormControl,
  FormHelperText,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  TextField,
  Tooltip
} from "@mui/material";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import axios from "axios";
import { toast } from "sonner";
import { FormDialog, LabPage, RegisterTable, type RegisterColumn } from "../../../components/lab";
import { LoadErrorAlert } from "../../../components/LoadErrorAlert";
import { ENTITY_TYPE_LABELS } from "../../../components/labels/entityTypeLabels";
import {
  labelService,
  type LabelFormat,
  type LabelLayout
} from "../../../services/labelService";
import { getErrorMessage } from "../../../utils/errorMessage";

type FieldKey = "widthMm" | "heightMm" | "layout" | "defaultCopies";
type FieldErrors = Partial<Record<FieldKey, string>>;

interface EditFormState {
  widthMm: string;
  heightMm: string;
  layout: LabelLayout;
  defaultCopies: string;
}

const LAYOUT_OPTIONS: readonly LabelLayout[] = ["Standard", "Small"];

const FIELD_NAME_MAP: Record<string, FieldKey> = {
  widthmm: "widthMm",
  width: "widthMm",
  heightmm: "heightMm",
  height: "heightMm",
  layout: "layout",
  defaultcopies: "defaultCopies",
  copies: "defaultCopies"
};

function labelForEntityType(entityType: LabelFormat["entityType"]): string {
  return ENTITY_TYPE_LABELS[entityType] ?? entityType;
}

function mapMessageToField(text: string): FieldKey | null {
  if (/\bwidth\b/i.test(text)) return "widthMm";
  if (/\bheight\b/i.test(text)) return "heightMm";
  if (/\blayout\b/i.test(text)) return "layout";
  if (/\bcopies\b/i.test(text)) return "defaultCopies";
  return null;
}

function extractFieldErrors(err: unknown): FieldErrors {
  if (!axios.isAxiosError(err)) return {};
  const data = err.response?.data as { message?: unknown; errors?: unknown } | undefined;
  const result: FieldErrors = {};

  if (data?.errors && typeof data.errors === "object" && !Array.isArray(data.errors)) {
    for (const [rawKey, rawVal] of Object.entries(data.errors as Record<string, unknown>)) {
      const mappedKey = FIELD_NAME_MAP[rawKey.trim().toLowerCase()];
      if (!mappedKey) continue;
      if (typeof rawVal === "string" && rawVal.trim()) {
        result[mappedKey] = rawVal.trim();
      } else if (Array.isArray(rawVal) && typeof rawVal[0] === "string" && rawVal[0].trim()) {
        result[mappedKey] = rawVal[0].trim();
      }
    }
    if (Object.keys(result).length > 0) return result;
  }

  if (Array.isArray(data?.errors)) {
    for (const item of data.errors) {
      if (typeof item !== "string" || !item.trim()) continue;
      const field = mapMessageToField(item);
      if (field && !result[field]) result[field] = item.trim();
    }
    if (Object.keys(result).length > 0) return result;
  }

  const status = err.response?.status;
  if ((status === 400 || status === 422) && typeof data?.message === "string" && data.message.trim()) {
    const field = mapMessageToField(data.message);
    if (field) result[field] = data.message.trim();
  }

  return result;
}

function parseFiniteNumber(raw: string): number | null {
  const trimmed = raw.trim();
  if (!trimmed) return null;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}

export function LabelFormatsPage() {
  const [formats, setFormats] = useState<LabelFormat[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [editing, setEditing] = useState<LabelFormat | null>(null);
  const [form, setForm] = useState<EditFormState>({
    widthMm: "",
    heightMm: "",
    layout: "Standard",
    defaultCopies: ""
  });
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const abortRef = useRef<AbortController | null>(null);
  const requestSeqRef = useRef(0);

  const loadFormats = useCallback(async () => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    const seq = ++requestSeqRef.current;

    setLoading(true);
    setLoadError(null);
    try {
      const data = await labelService.getFormats(controller.signal);
      if (seq !== requestSeqRef.current || controller.signal.aborted) return;
      setFormats(data);
    } catch (err) {
      if (seq !== requestSeqRef.current || controller.signal.aborted || axios.isCancel(err)) return;
      setLoadError(getErrorMessage(err, "Failed to load label formats."));
    } finally {
      if (seq === requestSeqRef.current && !controller.signal.aborted) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    void loadFormats();
    return () => {
      abortRef.current?.abort();
    };
  }, [loadFormats]);

  const openEdit = useCallback((row: LabelFormat) => {
    setEditing(row);
    setForm({
      widthMm: String(row.widthMm),
      heightMm: String(row.heightMm),
      layout: row.layout,
      defaultCopies: String(row.defaultCopies)
    });
    setFieldErrors({});
    setDialogError(null);
  }, []);

  const handleClose = () => {
    if (saving) return;
    setEditing(null);
  };

  const handleSave = async () => {
    if (!editing) return;
    const nextErrors: FieldErrors = {};
    const widthMm = parseFiniteNumber(form.widthMm);
    const heightMm = parseFiniteNumber(form.heightMm);
    const defaultCopies = parseFiniteNumber(form.defaultCopies);

    if (widthMm === null) nextErrors.widthMm = "Enter a valid number.";
    if (heightMm === null) nextErrors.heightMm = "Enter a valid number.";
    if (defaultCopies === null || !Number.isInteger(defaultCopies)) {
      nextErrors.defaultCopies = "Enter a valid whole number.";
    }

    setFieldErrors(nextErrors);
    setDialogError(null);
    if (Object.keys(nextErrors).length > 0 || widthMm === null || heightMm === null || defaultCopies === null) {
      return;
    }

    setSaving(true);
    try {
      await labelService.updateFormat(editing.id, editing.version, {
        widthMm,
        heightMm,
        layout: form.layout,
        defaultCopies
      });
      toast.success(`${labelForEntityType(editing.entityType)} label format updated.`);
      setEditing(null);
      await loadFormats();
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 409) {
        setDialogError("Someone else changed this format. Reload and try again.");
      } else {
        const serverFieldErrors = extractFieldErrors(err);
        if (Object.keys(serverFieldErrors).length > 0) {
          setFieldErrors(serverFieldErrors);
        } else {
          toast.error(getErrorMessage(err, "Failed to update label format."));
        }
      }
    } finally {
      setSaving(false);
    }
  };

  const columns = useMemo<RegisterColumn<LabelFormat>[]>(() => [
    {
      key: "entityType",
      label: "Label Type",
      render: (row) => labelForEntityType(row.entityType)
    },
    {
      key: "widthMm",
      label: "Width",
      numeric: true,
      render: (row) => `${row.widthMm} mm`
    },
    {
      key: "heightMm",
      label: "Height",
      numeric: true,
      render: (row) => `${row.heightMm} mm`
    },
    {
      key: "layout",
      label: "Layout",
      render: (row) => row.layout
    },
    {
      key: "defaultCopies",
      label: "Default Labels",
      numeric: true,
      render: (row) => row.defaultCopies
    },
    {
      key: "actions",
      label: "Actions",
      align: "right",
      width: 80,
      render: (row) => {
        const typeName = labelForEntityType(row.entityType);
        return (
          <Tooltip title={`Edit ${typeName} label format`}>
            <IconButton
              size="small"
              aria-label={`Edit ${typeName} label format`}
              onClick={(e) => {
                e.stopPropagation();
                openEdit(row);
              }}
            >
              <EditOutlinedIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        );
      }
    }
  ], [openEdit]);

  return (
    <LabPage
      title="Label Formats"
      subtitle="Configure label dimensions, layouts, and default number of labels for each record type."
    >
      {!loading && loadError && (
        <LoadErrorAlert message={loadError} onRetry={() => void loadFormats()} />
      )}

      {(!loadError || loading || formats.length > 0) && (
        <RegisterTable
          columns={columns}
          rows={formats}
          getRowId={(row) => row.id}
          loading={loading}
          empty={{
            title: "No label formats configured",
            description: "No label formats were returned by the server."
          }}
        />
      )}

      <FormDialog
        open={editing !== null}
        title={editing ? `Edit ${labelForEntityType(editing.entityType)} Label Format` : "Edit Label Format"}
        onClose={handleClose}
        onSubmit={() => void handleSave()}
        submitLabel="Save"
        submitting={saving}
        error={dialogError}
      >
        <TextField
          label="Width (mm)"
          type="number"
          size="small"
          fullWidth
          value={form.widthMm}
          onChange={(e) => {
            setForm((prev) => ({ ...prev, widthMm: e.target.value }));
            setFieldErrors((prev) => ({ ...prev, widthMm: undefined }));
          }}
          error={!!fieldErrors.widthMm}
          helperText={fieldErrors.widthMm}
          slotProps={{ htmlInput: { step: "any" } }}
        />

        <TextField
          label="Height (mm)"
          type="number"
          size="small"
          fullWidth
          value={form.heightMm}
          onChange={(e) => {
            setForm((prev) => ({ ...prev, heightMm: e.target.value }));
            setFieldErrors((prev) => ({ ...prev, heightMm: undefined }));
          }}
          error={!!fieldErrors.heightMm}
          helperText={fieldErrors.heightMm}
          slotProps={{ htmlInput: { step: "any" } }}
        />

        <FormControl fullWidth size="small" error={!!fieldErrors.layout}>
          <InputLabel id="label-format-layout-label">Layout</InputLabel>
          <Select
            labelId="label-format-layout-label"
            label="Layout"
            value={form.layout}
            onChange={(e) => {
              setForm((prev) => ({ ...prev, layout: e.target.value as LabelLayout }));
              setFieldErrors((prev) => ({ ...prev, layout: undefined }));
            }}
          >
            {LAYOUT_OPTIONS.map((option) => (
              <MenuItem key={option} value={option}>
                {option}
              </MenuItem>
            ))}
          </Select>
          {fieldErrors.layout && <FormHelperText>{fieldErrors.layout}</FormHelperText>}
        </FormControl>

        <TextField
          label="Default number of labels"
          type="number"
          size="small"
          fullWidth
          value={form.defaultCopies}
          onChange={(e) => {
            setForm((prev) => ({ ...prev, defaultCopies: e.target.value }));
            setFieldErrors((prev) => ({ ...prev, defaultCopies: undefined }));
          }}
          error={!!fieldErrors.defaultCopies}
          helperText={fieldErrors.defaultCopies}
        />
      </FormDialog>
    </LabPage>
  );
}
