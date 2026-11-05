import { useEffect, useState } from "react";
import { Alert, Box, Button, Collapse, Typography } from "@mui/material";
import {
  ItemPreparationConfigurationService,
  type ItemPreparationConfiguration
} from "../../../testPreparation/services/ItemPreparationConfigurationService";

interface Props {
  onOpenItem: (itemId: number) => void;
}

// Section Head worklist: preparation configurations whose diluent or
// neutralizer is still legacy text, so no lot can be recorded for them.
// Renders nothing when there are none (or the list cannot be read).
export function UnmappedPreparationConfigurations({ onOpenItem }: Props) {
  const [rows, setRows] = useState<ItemPreparationConfiguration[]>([]);
  const [open, setOpen] = useState(false);

  useEffect(() => {
    let cancelled = false;
    ItemPreparationConfigurationService.getUnmapped()
      .then((r) => { if (!cancelled) setRows(Array.isArray(r) ? r : []); })
      .catch(() => { if (!cancelled) setRows([]); });
    return () => { cancelled = true; };
  }, []);

  if (rows.length === 0) return null;

  return (
    <Alert
      severity="warning"
      sx={{ mb: 2 }}
      action={
        <Button color="inherit" size="small" onClick={() => setOpen((o) => !o)}>
          {open ? "Hide" : "Show"}
        </Button>
      }
    >
      <strong>Not linked to master data:</strong> {rows.length} item{rows.length === 1 ? "" : "s"} still use a
      typed diluent or neutralizer, so no lot can be recorded for them.
      <Collapse in={open} unmountOnExit>
        <Box sx={{ mt: 1, display: "grid", gap: 0.5 }}>
          {rows.map((r) => (
            <Box key={r.id} sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
              <Button size="small" variant="text" onClick={() => onOpenItem(r.itemId)} sx={{ textTransform: "none", fontWeight: 700 }}>
                {r.itemName ?? `Item #${r.itemId}`}
              </Button>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Diluent: {r.diluent || "—"} · Neutralizer: {r.neutralizer || "—"}
              </Typography>
            </Box>
          ))}
        </Box>
      </Collapse>
    </Alert>
  );
}
