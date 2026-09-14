import { useState, useEffect } from "react";
import { Button, TextField, Stack, Alert, Typography } from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { masterDataOptions, MediaProductOption } from "../../../../services/masterDataOptions";
import { monospaceFontFamily } from "../../../../theme/palette";

interface AddMediaProductDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: (createdProduct: MediaProductOption) => void;
}

export function AddMediaProductDialog({ open, onClose, onSuccess }: AddMediaProductDialogProps) {
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
      setError("Both product name and code are required.");
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
      setError(message ?? "Failed to create media product.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      title="Add Media Product"
      onClose={onClose}
      maxWidth="xs"
      actions={
        <>
          <Button onClick={onClose} disabled={submitting}>
            Cancel
          </Button>
          <Button
            variant="contained"
            onClick={handleAdd}
            disabled={!name.trim() || !code.trim() || submitting}
          >
            {submitting ? "Adding..." : "Add Product"}
          </Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ mt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}
        <TextField
          label="Product Name"
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g. Tryptic Soy Agar"
          autoFocus
          size="small"
        />
        <TextField
          label="Product Code"
          required
          value={code}
          onChange={(e) => setCode(e.target.value)}
          placeholder="e.g. TSA"
          helperText="2-10 letters, digits, dot or hyphen"
          size="small"
          onKeyDown={(e) => {
            if (e.key === "Enter" && name.trim() && code.trim() && !submitting) {
              handleAdd();
            }
          }}
        />
        <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily, color: "text.secondary", fontSize: 13 }}>
          Lots will be numbered {displayCode}/01/{yy}
        </Typography>
      </Stack>
    </FloatingDialog>
  );
}
