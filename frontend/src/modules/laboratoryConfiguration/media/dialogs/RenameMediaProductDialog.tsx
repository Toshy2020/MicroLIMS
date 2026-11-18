import { useState, useEffect } from "react";
import { Button, TextField, Stack, Alert } from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { masterDataOptions, MediaProductOption } from "../../../../services/masterDataOptions";
import { useTranslation } from "../../../../i18n";

interface RenameMediaProductDialogProps {
  open: boolean;
  product: MediaProductOption | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function RenameMediaProductDialog({ open, product, onClose, onSuccess }: RenameMediaProductDialogProps) {
  const { t } = useTranslation("labConfig");
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (product) {
      setName(product.name);
      setError(null);
      setSaving(false);
    }
  }, [product, open]);

  const handleSave = async () => {
    if (!product || !name.trim()) {
      setError(t("media.renameProductDialog.requiredError"));
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await masterDataOptions.renameMediaProduct(product.id, name.trim(), product.version);
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const message = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(message ?? t("media.renameProductDialog.renameError"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <FloatingDialog
      open={open && product != null}
      title={t("media.renameProductDialog.title", { name: product?.name ?? "" })}
      onClose={onClose}
      maxWidth="xs"
      actions={
        <>
          <Button onClick={onClose} disabled={saving}>
            {t("media.renameProductDialog.cancel")}
          </Button>
          <Button
            variant="contained"
            onClick={handleSave}
            disabled={!name.trim() || saving}
          >
            {saving ? t("media.renameProductDialog.saving") : t("media.renameProductDialog.save")}
          </Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ mt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}
        <TextField
          label={t("media.renameProductDialog.codeLabel")}
          value={product?.code ?? ""}
          slotProps={{ input: { readOnly: true } }}
          size="small"
          helperText={t("media.renameProductDialog.codeHelper")}
        />
        <TextField
          label={t("media.renameProductDialog.nameLabel")}
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
          autoFocus
          size="small"
          onKeyDown={(e) => {
            if (e.key === "Enter" && name.trim() && !saving) {
              handleSave();
            }
          }}
        />
      </Stack>
    </FloatingDialog>
  );
}
