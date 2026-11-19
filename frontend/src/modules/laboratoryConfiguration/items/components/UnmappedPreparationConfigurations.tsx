import { useEffect, useState } from "react";
import { Alert, Box, Button, Collapse, Typography } from "@mui/material";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";
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
  const { t } = useTranslation("labConfig");
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
          {open ? t("items.unmappedPrep.hide") : t("items.unmappedPrep.show")}
        </Button>
      }
    >
      <Trans
        i18nKey="items.unmappedPrep.banner"
        ns="labConfig"
        count={rows.length}
        components={{ bold: <strong /> }}
      />
      <Collapse in={open} unmountOnExit>
        <Box sx={{ mt: 1, display: "grid", gap: 0.5 }}>
          {rows.map((r) => (
            <Box key={r.id} sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
              <Button size="small" variant="text" onClick={() => onOpenItem(r.itemId)} sx={{ textTransform: "none", fontWeight: 700 }}>
                {r.itemName ?? t("items.unmappedPrep.itemFallback", { id: r.itemId })}
              </Button>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {t("items.unmappedPrep.details", { diluent: r.diluent || "—", neutralizer: r.neutralizer || "—" })}
              </Typography>
            </Box>
          ))}
        </Box>
      </Collapse>
    </Alert>
  );
}
