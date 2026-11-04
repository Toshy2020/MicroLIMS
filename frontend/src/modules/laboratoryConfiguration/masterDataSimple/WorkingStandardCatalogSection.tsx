import { useEffect, useState } from "react";
import { Alert, Button, Chip, Stack, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import { RegisterTable } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { monospaceFontFamily } from "../../../theme/palette";
import { TestingMaterialCatalogService, TestingMaterialCatalogItem } from "./services/TestingMaterialCatalogService";

const columns: RegisterColumn<TestingMaterialCatalogItem>[] = [
  { key: "code", label: "Lot", sortable: true, render: (r) => <span style={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>{r.code}</span> },
  { key: "name", label: "Name", sortable: true },
  { key: "ownerSectionCode", label: "Owner lab", sortable: true, render: (r) => r.ownerSectionCode ? <Chip label={r.ownerSectionCode} size="small" variant="outlined" /> : "—" },
  { key: "isActive", label: "Stock", render: (r) => <Chip label={r.isActive ? "In stock" : "Not in stock"} size="small" variant="outlined" color={r.isActive ? "success" : "default"} /> }
];

// Approved working standards beside the Standards master: read-only here, since a working standard is
// qualified and tracked in its own module.
export function WorkingStandardCatalogSection() {
  const [rows, setRows] = useState<TestingMaterialCatalogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    TestingMaterialCatalogService.getAll({ group: "Standards" })
      .then((items) => { if (!cancelled) setRows(items.filter((r) => r.sourceType === "WorkingStandard")); })
      .catch((err: unknown) => {
        const e = err as { response?: { data?: { message?: string } }; message?: string };
        if (!cancelled) setError(e.response?.data?.message ?? e.message ?? "Could not load working standards.");
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  return (
    <Stack spacing={1.5}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
        <div>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>Approved working standards</Typography>
          <Typography variant="body2" color="text.secondary">Read-only here; qualified and managed in the Working Standards module.</Typography>
        </div>
        <Button component={RouterLink} to="/working-standards" variant="outlined" sx={{ fontWeight: 600, textTransform: "none" }}>
          Open Working Standards
        </Button>
      </Stack>
      {error && <Alert severity="error">{error}</Alert>}
      <RegisterTable
        columns={columns}
        rows={rows}
        getRowId={(r) => r.id}
        loading={loading}
        empty={{ title: "No working standards", description: "Approved working standards appear here once they are in stock." }}
      />
    </Stack>
  );
}
