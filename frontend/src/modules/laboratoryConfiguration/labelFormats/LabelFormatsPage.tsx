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
import { i18n, useTranslation } from "../../../i18n";
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
  const { t } = useTranslation("labConfig");
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
      setLoadError(getErrorMessage(err, i18n.t("labelFormats.loadError", { ns: "labConfig" })));
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

    if (widthMm === null) nextErrors.widthMm = t("labelFormats.invalidNumber");
    if (heightMm === null) nextErrors.heightMm = t("labelFormats.invalidNumber");
    if (defaultCopies === null || !Number.isInteger(defaultCopies)) {
      nextErrors.defaultCopies = t("labelFormats.invalidWhole");
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
      toast.success(t("labelFormats.updated", { type: labelForEntityType(editing.entityType) }));
      setEditing(null);
      await loadFormats();
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 409) {
        setDialogError(t("labelFormats.conflict"));
      } else {
        const serverFieldErrors = extractFieldErrors(err);
        if (Object.keys(serverFieldErrors).length > 0) {
          setFieldErrors(serverFieldErrors);
        } else {
          toast.error(getErrorMessage(err, t("labelFormats.updateError")));
        }
      }
    } finally {
      setSaving(false);
    }
  };

  const columns = useMemo<RegisterColumn<LabelFormat>[]>(() => [
    {
      key: "entityType",
      label: t("labelFormats.columns.type"),
      render: (row) => labelForEntityType(row.entityType)
    },
    {
      key: "widthMm",
      label: t("labelFormats.columns.width"),
      numeric: true,
      render: (row) => t("labelFormats.mm", { value: row.widthMm })
    },
    {
      key: "heightMm",
      label: t("labelFormats.columns.height"),
      numeric: true,
      render: (row) => t("labelFormats.mm", { value: row.heightMm })
    },
    {
      key: "layout",
      label: t("labelFormats.columns.layout"),
      render: (row) => t(`labelFormats.layouts.${row.layout}`, { defaultValue: row.layout })
    },
    {
      key: "defaultCopies",
      label: t("labelFormats.columns.defaultLabels"),
      numeric: true,
      render: (row) => row.defaultCopies
    },
    {
      key: "actions",
      label: t("labelFormats.columns.actions"),
      align: "right",
      width: 80,
      render: (row) => {
        const typeName = labelForEntityType(row.entityType);
        return (
          <Tooltip title={t("labelFormats.editLabel", { type: typeName })}>
            <IconButton
              size="small"
              aria-label={t("labelFormats.editLabel", { type: typeName })}
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
  ], [openEdit, t]);

  return (
    <LabPage
      title={t("labelFormats.title")}
      subtitle={t("labelFormats.subtitle")}
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
            title: t("labelFormats.emptyTitle"),
            description: t("labelFormats.emptyDesc")
          }}
        />
      )}

      <FormDialog
        open={editing !== null}
        title={editing ? t("labelFormats.dialogTitle", { type: labelForEntityType(editing.entityType) }) : t("labelFormats.dialogTitleGeneric")}
        onClose={handleClose}
        onSubmit={() => void handleSave()}
        submitLabel={t("labelFormats.save")}
        submitting={saving}
        error={dialogError}
      >
        <TextField
          label={t("labelFormats.widthLabel")}
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
          label={t("labelFormats.heightLabel")}
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
          <InputLabel id="label-format-layout-label">{t("labelFormats.layoutLabel")}</InputLabel>
          <Select
            labelId="label-format-layout-label"
            label={t("labelFormats.layoutLabel")}
            value={form.layout}
            onChange={(e) => {
              setForm((prev) => ({ ...prev, layout: e.target.value as LabelLayout }));
              setFieldErrors((prev) => ({ ...prev, layout: undefined }));
            }}
          >
            {LAYOUT_OPTIONS.map((option) => (
              <MenuItem key={option} value={option}>
                {t(`labelFormats.layouts.${option}`, { defaultValue: option })}
              </MenuItem>
            ))}
          </Select>
          {fieldErrors.layout && <FormHelperText>{fieldErrors.layout}</FormHelperText>}
        </FormControl>

        <TextField
          label={t("labelFormats.copiesLabel")}
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
