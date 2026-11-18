import { useState, useEffect } from "react";
import { Button, TextField, Stack, Alert, Typography } from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { masterDataOptions, MediaProductOption } from "../../../../services/masterDataOptions";
import { monospaceFontFamily } from "../../../../theme/palette";
import { FieldHint, describedByWithHint } from "../../../../components/FieldHint";
import { getMediaHints } from "../hints";
import { useTranslation } from "../../../../i18n";

interface AddMediaProductDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: (createdProduct: MediaProductOption) => void;
}

export function AddMediaProductDialog({ open, onClose, onSuccess }: AddMediaProductDialogProps) {
  const { t } = useTranslation("labConfig");
  const hints = getMediaHints(t);
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open) {
      setName("");
      setCode("");
      setError(null);
      setSubmitting(false);
    }
  }, [open]);

  const yy = String(new Date().getFullYear()).slice(-2);
  const displayCode = code.trim() ? code.trim() : "{CODE}";

  const handleAdd = async () => {
    if (!name.trim() || !code.trim()) {
      setError(t("media.addProductDialog.requiredError"));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const created = await masterDataOptions.createMediaProduct(name.trim(), code.trim());
      onSuccess(created);
      onClose();
    } catch (err: unknown) {
      const message = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(message ?? t("media.addProductDialog.createError"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      title={t("media.addProductDialog.title")}
      onClose={onClose}
      maxWidth="xs"
      actions={
        <>
          <Button onClick={onClose} disabled={submitting}>
            {t("media.addProductDialog.cancel")}
          </Button>
          <Button
            variant="contained"
            onClick={handleAdd}
            disabled={!name.trim() || !code.trim() || submitting}
          >
            {submitting ? t("media.addProductDialog.adding") : t("media.addProductDialog.add")}
          </Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ mt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}
        <TextField
          label={t("media.addProductDialog.nameLabel")}
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder={t("media.addProductDialog.namePlaceholder")}
          autoFocus
          size="small"
        />
        <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start" }}>
        <TextField
          label={t("media.addProductDialog.codeLabel")}
          required
          value={code}
          onChange={(e) => setCode(e.target.value)}
          placeholder={t("media.addProductDialog.codePlaceholder")}
          helperText={t("media.addProductDialog.codeHelper")}
          size="small"
          fullWidth
          id="media-product-code"
          slotProps={{ input: { "aria-describedby": describedByWithHint("media-product-code", "media-product-code") } }}
          onKeyDown={(e) => {
            if (e.key === "Enter" && name.trim() && code.trim() && !submitting) {
              handleAdd();
            }
          }}
        />
          <FieldHint id="media-product-code" label={t("media.addProductDialog.codeLabel")} hint={hints.productCode} />
        </Stack>
        <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily, color: "text.secondary", fontSize: 13 }}>
          {t("media.addProductDialog.lotsNumberedPreview", { code: displayCode, yy })}
        </Typography>
      </Stack>
    </FloatingDialog>
  );
}
