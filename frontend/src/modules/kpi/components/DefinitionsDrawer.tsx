import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import {
  Alert, Box, Divider, Drawer, IconButton, LinearProgress, Stack, Table, TableBody, TableCell,
  TableHead, TableRow, Typography
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { kpiDashboardService } from "../services/kpiDashboardService";
import type { KpiDefinitions } from "../types/kpiTypes";
import { formatUnits } from "../utils/kpiFormat";
import { useTranslation } from "../../../i18n";

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <Box sx={{ mb: 1 }}>
      <Typography sx={{ fontSize: 11.5, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>{label}</Typography>
      <Typography sx={{ fontSize: 13 }}>{children}</Typography>
    </Box>
  );
}

interface DefinitionsDrawerProps {
  open: boolean;
  onClose(): void;
  from: string;
  to: string;
}

// Everything here comes from the backend registry, so the text on screen is the
// text the calculation was written against.
export function DefinitionsDrawer({ open, onClose, from, to }: DefinitionsDrawerProps) {
  const { t } = useTranslation("kpi");
  const [data, setData] = useState<KpiDefinitions | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!open) return;
    let live = true;
    setLoading(true);
    setError(null);
    kpiDashboardService.definitions(from, to)
      .then((d) => { if (live) setData(d); })
      .catch((e: unknown) => {
        if (live) {
          setData(null);
          const err = e as { response?: { data?: { message?: string } } } | undefined;
          setError(err?.response?.data?.message ?? t("definitions.loadError"));
        }
      })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [open, from, to, t]);

  const kpis = [...(data?.kpis ?? [])].sort((a, b) => a.order - b.order);

  return (
    <Drawer anchor="right" open={open} onClose={onClose} slotProps={{ paper: { sx: { width: { xs: "100%", sm: 480 }, p: 2 } } }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
        <Typography component="h2" sx={{ fontSize: 18, fontWeight: 700 }}>{t("definitions.title")}</Typography>
        <IconButton aria-label={t("definitions.close")} onClick={onClose}><CloseIcon /></IconButton>
      </Stack>
      {loading && <LinearProgress sx={{ mb: 1 }} />}
      {error && <Alert severity="error">{error}</Alert>}
      {data && (
        <Box>
          {kpis.map((k) => (
            <Box key={k.key} component="section" sx={{ mb: 2 }}>
              <Typography component="h3" sx={{ fontSize: 15, fontWeight: 700 }}>{k.name}</Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
                {t("definitions.unit", { unit: k.unit, direction: t(`directions.${k.direction}`) })}
              </Typography>
              <Field label={t("definitions.fields.definition")}>{k.definition}</Field>
              <Field label={t("definitions.fields.clockEvents")}>{k.clockEvents}</Field>
              <Field label={t("definitions.fields.population")}>{k.population}</Field>
              <Field label={t("definitions.fields.source")}>{k.source}</Field>
              <Divider />
            </Box>
          ))}

          <Typography component="h3" sx={{ fontSize: 15, fontWeight: 700, mb: 1 }}>
            {t("definitions.inForce", { date: to })}
          </Typography>
          {data.profilesInForce.length === 0 ? (
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              {t("definitions.noProfiles")}
            </Typography>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>{t("definitions.columns.workflowType")}</TableCell>
                  <TableCell>{t("definitions.columns.test")}</TableCell>
                  <TableCell align="right">{t("definitions.columns.workUnits")}</TableCell>
                  <TableCell align="right">{t("definitions.columns.target")}</TableCell>
                  <TableCell>{t("definitions.columns.effectiveFrom")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.profilesInForce.map((p) => (
                  <TableRow key={p.versionId}>
                    <TableCell>{p.workflowType}</TableCell>
                    <TableCell>{p.testCode ?? t("definitions.typeDefault")}</TableCell>
                    <TableCell align="right">{p.workUnits == null ? t("definitions.inherited") : formatUnits(p.workUnits)}</TableCell>
                    <TableCell align="right">{p.targetAnalystDays == null ? t("definitions.inherited") : formatUnits(p.targetAnalystDays)}</TableCell>
                    <TableCell>{p.effectiveFrom}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Box>
      )}
    </Drawer>
  );
}
