import { useState, useEffect } from "react";
import { Button, TextField, Stack, Alert } from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { masterDataOptions, MediaProductOption } from "../../../../services/masterDataOptions";

interface RenameMediaProductDialogProps {
  open: boolean;
  product: MediaProductOption | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function RenameMediaProductDialog({ open, product, onClose, onSuccess }: RenameMediaProductDialogProps) {
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
      setError("Product name is required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await masterDataOptions.renameMediaProduct(product.id, name.trim());
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const message = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(message ?? "Failed to rename media product.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <FloatingDialog
      open={open && product != null}
      title={`Rename Media Product: ${product?.name ?? ""}`}
      onClose={onClose}
      maxWidth="xs"
      actions={
        <>
          <Button onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button
            variant="contained"
            onClick={handleSave}
            disabled={!name.trim() || saving}
          >
            {saving ? "Saving..." : "Save"}
          </Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ mt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}
        <TextField
          label="Product Code"
          value={product?.code ?? ""}
          slotProps={{ input: { readOnly: true } }}
          size="small"
          helperText="The code is changed from the Overview tab with an electronic signature."
        />
        <TextField
          label="Product Name"
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
