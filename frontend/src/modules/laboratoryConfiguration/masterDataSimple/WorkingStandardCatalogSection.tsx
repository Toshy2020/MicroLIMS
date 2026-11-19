import { useEffect, useMemo, useState } from "react";
import { Alert, Button, Chip, Stack, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import { RegisterTable } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import { TestingMaterialCatalogService, TestingMaterialCatalogItem } from "./services/TestingMaterialCatalogService";

const getColumns = (t: ReturnType<typeof useTranslation>["t"]): RegisterColumn<TestingMaterialCatalogItem>[] => [
  { key: "code", label: t("workingStandardCatalog.columns.lot"), sortable: true, render: (r) => <span style={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>{r.code}</span> },
  { key: "name", label: t("workingStandardCatalog.columns.name"), sortable: true },
  { key: "ownerSectionCode", label: t("workingStandardCatalog.columns.ownerLab"), sortable: true, render: (r) => r.ownerSectionCode ? <Chip label={r.ownerSectionCode} size="small" variant="outlined" /> : t("common.dash") },
  { key: "isActive", label: t("workingStandardCatalog.columns.stock"), render: (r) => <Chip label={r.isActive ? t("workingStandardCatalog.inStock") : t("workingStandardCatalog.notInStock")} size="small" variant="outlined" color={r.isActive ? "success" : "default"} /> }
];

// Approved working standards beside the Standards master: read-only here, since a working standard is
// qualified and tracked in its own module.
export function WorkingStandardCatalogSection() {
  const { t } = useTranslation("masterData");
  const columns = useMemo(() => getColumns(t), [t]);
  const [rows, setRows] = useState<TestingMaterialCatalogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    TestingMaterialCatalogService.getAll({ group: "Standards" })
      .then((items) => { if (!cancelled) setRows(items.filter((r) => r.sourceType === "WorkingStandard")); })
      .catch((err: unknown) => {
        const e = err as { response?: { data?: { message?: string } }; message?: string };
        if (!cancelled) setError(e.response?.data?.message ?? e.message ?? t("workingStandardCatalog.loadError"));
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [t]);

  return (
    <Stack spacing={1.5}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
        <div>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>{t("workingStandardCatalog.title")}</Typography>
          <Typography variant="body2" color="text.secondary">{t("workingStandardCatalog.subtitle")}</Typography>
        </div>
        <Button component={RouterLink} to="/working-standards" variant="outlined" sx={{ fontWeight: 600, textTransform: "none" }}>
          {t("workingStandardCatalog.openButton")}
        </Button>
      </Stack>
      {error && <Alert severity="error">{error}</Alert>}
      <RegisterTable
        columns={columns}
        rows={rows}
        getRowId={(r) => r.id}
        loading={loading}
        empty={{ title: t("workingStandardCatalog.emptyTitle"), description: t("workingStandardCatalog.emptyDesc") }}
      />
    </Stack>
  );
}
