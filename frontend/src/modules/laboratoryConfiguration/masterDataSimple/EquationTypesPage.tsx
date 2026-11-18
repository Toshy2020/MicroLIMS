import { useEffect, useMemo, useState } from "react";
import { Alert, Box, Chip, Stack, Typography } from "@mui/material";
import FunctionsIcon from "@mui/icons-material/Functions";
import { LabPage, FilterBar, RegisterTable, RegisterColumn } from "../../../components/lab";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import { masterDataOptions, EquationTypeDto } from "../../../services/masterDataOptions";

const getColumns = (t: ReturnType<typeof useTranslation>["t"]): RegisterColumn<EquationTypeDto>[] => [
  {
    key: "name",
    label: t("equationTypes.columns.name"),
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
    label: t("equationTypes.columns.code"),
    sortable: true,
    width: 140,
    render: (type) => <Box component="span" sx={{ fontFamily: monospaceFontFamily, fontSize: "0.8rem" }}>{type.code}</Box>
  },
  {
    key: "formulaText",
    label: t("equationTypes.columns.formulaText"),
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
        <Typography variant="body2" sx={{ color: "text.secondary", fontStyle: "italic" }}>{t("equationTypes.none")}</Typography>
      )
  },
  {
    key: "requiredInputs",
    label: t("equationTypes.columns.requiredInputs"),
    width: 280,
    render: (type) =>
      type.requiredInputs && type.requiredInputs.length > 0 ? (
        <Stack useFlexGap direction="row" spacing={0.75} sx={{ flexWrap: "wrap", rowGap: 0.5, alignItems: "center" }}>
          {type.requiredInputs.map((input) => (
            <Chip key={input} size="small" label={input} color="default" sx={{ fontSize: "0.75rem" }} />
          ))}
        </Stack>
      ) : (
        <Typography variant="body2" sx={{ color: "text.secondary", fontStyle: "italic" }}>{t("equationTypes.none")}</Typography>
      )
  }
];

export function EquationTypesPage() {
  const { t } = useTranslation("masterData");
  const columns = useMemo(() => getColumns(t), [t]);
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
        setError(errObj.response?.data?.message ?? errObj.message ?? t("equationTypes.loadError"));
        setLoading(false);
      });
  };

  useEffect(() => {
    loadData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return equationTypes;
    return equationTypes.filter((et) =>
      [et.name, et.code, et.formulaText, ...(et.requiredInputs ?? [])].some((v) => v?.toLowerCase().includes(q))
    );
  }, [equationTypes, search]);

  return (
    <LabPage
      title={t("equationTypes.title")}
      subtitle={t("equationTypes.subtitle")}
      filters={
        <FilterBar
          search={search}
          onSearch={setSearch}
          placeholder={t("equationTypes.searchPlaceholder")}
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
        getRowId={(et) => et.code}
        loading={loading}
        empty={{
          title: search ? t("equationTypes.emptyMatching") : t("equationTypes.emptyTitle"),
          description: search ? t("equationTypes.emptyDesc") : undefined
        }}
      />
    </LabPage>
  );
}
