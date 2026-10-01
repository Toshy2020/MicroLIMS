import { useEffect, useMemo, useState } from "react";
import { Alert, Box, Chip, Stack, Typography } from "@mui/material";
import FunctionsIcon from "@mui/icons-material/Functions";
import { LabPage, FilterBar, RegisterTable, RegisterColumn } from "../../../components/lab";
import { monospaceFontFamily } from "../../../theme/palette";
import { masterDataOptions, EquationTypeDto } from "../../../services/masterDataOptions";

const columns: RegisterColumn<EquationTypeDto>[] = [
  {
    key: "name",
    label: "Type / Name",
    sortable: true,
    width: 200,
    render: (type) => (
      <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
        <FunctionsIcon fontSize="small" color="primary" />
        <Typography sx={{ fontWeight: 600, fontSize: "0.875rem" }}>{type.name}</Typography>
      </Stack>
    )
  },
  {
    key: "code",
    label: "Code",
    sortable: true,
    width: 140,
    render: (type) => <Box component="span" sx={{ fontFamily: monospaceFontFamily, fontSize: "0.8rem" }}>{type.code}</Box>
  },
  {
    key: "formulaText",
    label: "Formula Text",
    render: (type) =>
      type.formulaText ? (
        <Box
          sx={{
            fontFamily: monospaceFontFamily,
            fontSize: "0.8rem",
            bgcolor: "action.hover",
            p: 1,
            borderRadius: 1,
            wordBreak: "break-word",
            lineHeight: 1.4
          }}
        >
          {type.formulaText}
        </Box>
      ) : (
        <Typography variant="body2" sx={{ color: "text.secondary", fontStyle: "italic" }}>None</Typography>
      )
  },
  {
    key: "requiredInputs",
    label: "Required Inputs",
    width: 280,
    render: (type) =>
      type.requiredInputs && type.requiredInputs.length > 0 ? (
        <Stack useFlexGap direction="row" spacing={0.75} sx={{ flexWrap: "wrap", rowGap: 0.5, alignItems: "center" }}>
          {type.requiredInputs.map((input) => (
            <Chip key={input} size="small" label={input} color="default" sx={{ fontSize: "0.75rem" }} />
          ))}
        </Stack>
      ) : (
        <Typography variant="body2" sx={{ color: "text.secondary", fontStyle: "italic" }}>None</Typography>
      )
  }
];

export function EquationTypesPage() {
  const [equationTypes, setEquationTypes] = useState<EquationTypeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");

  const loadData = () => {
    setLoading(true);
    setError(null);
    masterDataOptions
      .getEquationTypes()
      .then((data) => {
        setEquationTypes(data);
        setLoading(false);
      })
      .catch((e: unknown) => {
        const errObj = e as { response?: { data?: { message?: string } }; message?: string };
        setError(errObj.response?.data?.message ?? errObj.message ?? "Could not load equation types.");
        setLoading(false);
      });
  };

  useEffect(() => {
    loadData();
  }, []);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return equationTypes;
    return equationTypes.filter((t) =>
      [t.name, t.code, t.formulaText, ...(t.requiredInputs ?? [])].some((v) => v?.toLowerCase().includes(q))
    );
  }, [equationTypes, search]);

  return (
    <LabPage
      title="Equation Types"
      subtitle="Predefined mathematical formulas and required calculation parameters for analytical test methods and system suitability."
      filters={
        <FilterBar
          search={search}
          onSearch={setSearch}
          placeholder="Search name, code or formula"
          resultCount={filtered.length}
          onRefresh={loadData}
          refreshing={loading}
        />
      }
    >
      {error && <Alert severity="error">{error}</Alert>}
      <RegisterTable
        columns={columns}
        rows={filtered}
        getRowId={(t) => t.code}
        loading={loading}
        empty={{
          title: search ? "No matching equation types" : "No equation types found",
          description: search ? "Try a different search term." : undefined
        }}
      />
    </LabPage>
  );
}
