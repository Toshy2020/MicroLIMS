import { useEffect, useMemo, useState } from "react";
import { Box, FormControl, FormControlLabel, FormHelperText, InputLabel, MenuItem, Select, Stack, Switch, TextField, Typography } from "@mui/material";
import { SolutionMasterService, SolutionMaster } from "./services/SolutionMasterService";
import { MaterialMasterService, MaterialMasterEntry } from "./services/MaterialMasterService";
import { MoistureRole } from "../../../services/masterDataOptions";
import { useTranslation } from "../../../i18n";
import {
  TITRATION_CALCULATION_OPTIONS,
  TITRATION_ENDPOINT_OPTIONS,
  TITRATION_MODE_OPTIONS,
  TITRATION_TYPE_OPTIONS,
  TitrationFormState,
  normaliseTitrationForm,
  titrationVisibility
} from "./titrationConfig";

interface Props {
  form: TitrationFormState;
  onChange: (next: TitrationFormState) => void;
  sectionId?: number | "" | null;
  moistureRole?: MoistureRole;
  onMoistureRoleChange?: (role: MoistureRole) => void;
}

// Titration configuration block of the physicochemical Test Master dialog.
// Visibility rules live in titrationConfig.ts (unit tested).
export function TitrationConfigSection({ form, onChange, sectionId, moistureRole = "None", onMoistureRoleChange }: Props) {
  const { t } = useTranslation("masterData");
  const [titrants, setTitrants] = useState<SolutionMaster[]>([]);
  const [standards, setStandards] = useState<MaterialMasterEntry[]>([]);
  const [indicators, setIndicators] = useState<MaterialMasterEntry[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    Promise.all([
      SolutionMasterService.getAll("Titrant", true),
      MaterialMasterService.getAll("ReferenceStandard", true),
      MaterialMasterService.getAll("Indicator", true)
    ])
      .then(([t, s, i]) => {
        if (!active) return;
        setTitrants(t);
        setStandards(s);
        setIndicators(i);
      })
      .catch(() => active && setLoadError(t("titration.loadError")));
    return () => {
      active = false;
    };
  }, [t]);

  const v = titrationVisibility(form);
  const kf = form.type === "KarlFischer";
  const set = (patch: Partial<TitrationFormState>) => onChange(normaliseTitrationForm({ ...form, ...patch }));

  const inSection = (x: { sectionId: number }) => sectionId == null || sectionId === "" || x.sectionId === sectionId;
  // KF titrants are the mg H2O/mL reagents and only those; every other type excludes them.
  const titrantOptions = useMemo(
    () => titrants.filter((s) => inSection(s) && (kf ? s.strengthUnit === "MgWaterPerMl" : s.strengthUnit !== "MgWaterPerMl")),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [titrants, kf, sectionId]
  );
  const backUnit = titrants.find((s) => s.id === form.titrantSolutionMasterId)?.strengthUnit;
  const excessOptions = titrantOptions.filter((s) => !backUnit || s.strengthUnit === backUnit);
  // Reagent and standard master entries are app-wide, so they aren't filtered by section.
  const standardOptions = standards;
  const indicatorOptions = indicators;

  const titrantLabel = (s: SolutionMaster) =>
    `${s.name} (${s.nominalStrength ?? "?"} ${s.strengthUnit === "Molar" ? "M" : s.strengthUnit === "MgWaterPerMl" ? "mg H2O/mL" : "N"})`;

  const select = <T extends string | number>(
    id: string,
    label: string,
    value: T | "",
    options: { value: T; label: string }[],
    onPick: (v: T) => void,
    helper?: string
  ) => (
    <FormControl size="small" sx={{ flex: 1, minWidth: 200 }} required>
      <InputLabel id={id}>{label}</InputLabel>
      <Select labelId={id} label={label} value={value} onChange={(e) => onPick(e.target.value as T)}>
        {options.map((o) => (
          <MenuItem key={String(o.value)} value={o.value}>
            {o.label}
          </MenuItem>
        ))}
      </Select>
      {helper && <FormHelperText>{helper}</FormHelperText>}
    </FormControl>
  );

  const field = (label: string, key: keyof TitrationFormState, helper?: string, step: string = "any", required = false) => (
    <TextField
      size="small"
      type="number"
      label={required ? `${label} *` : label}
      value={form[key] as string}
      onChange={(e) => set({ [key]: e.target.value } as Partial<TitrationFormState>)}
      slotProps={{ htmlInput: { min: 0, step } }}
      helperText={helper}
      sx={{ flex: 1, minWidth: 200 }}
    />
  );

  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }} data-testid="titration-config">
      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>{t("titration.sectionTitle")}</Typography>
      {loadError && <Typography color="error" sx={{ fontSize: 12, mb: 1 }}>{loadError}</Typography>}
      <Stack spacing={2}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          {select(
            "titration-type-label",
            t("titration.typeLabel"),
            form.type,
            TITRATION_TYPE_OPTIONS.map((o) => ({ value: o.value, label: t(`titration.types.${o.value}`, { defaultValue: o.label }) })),
            (val) => {
              set({ type: val, titrantSolutionMasterId: "", excessSolutionMasterId: "" });
              if (val !== "KarlFischer" && moistureRole === "Water") {
                onMoistureRoleChange?.("None");
              }
            }
          )}
          {v.mode && select(
            "titration-mode-label",
            t("titration.modeLabel"),
            form.mode,
            TITRATION_MODE_OPTIONS.map((o) => ({ value: o.value, label: t(`titration.modes.${o.value}`, { defaultValue: o.label }) })),
            (m) => set({ mode: m })
          )}
          {v.calculation && select(
            "titration-calc-label",
            t("titration.calculationLabel"),
            form.calculation,
            TITRATION_CALCULATION_OPTIONS.filter((o) => o.value !== "Relative" || form.mode !== "Residual").map((o) => ({
              value: o.value,
              label: t(`titration.calculations.${o.value}`, { defaultValue: o.label })
            })),
            (c) => set({ calculation: c })
          )}
        </Stack>
        {kf && (
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: "center" }}>
            <Typography variant="body2" sx={{ color: "text.secondary", flex: 1 }}>
              {t("titration.kfDescription")}
            </Typography>
            <FormControl size="small" sx={{ minWidth: 200, maxWidth: 300 }}>
              <InputLabel id="titration-moisture-role-label">{t("titration.moistureRoleLabel")}</InputLabel>
              <Select<MoistureRole>
                labelId="titration-moisture-role-label"
                label={t("titration.moistureRoleLabel")}
                value={moistureRole === "LossOnDrying" ? "None" : (moistureRole || "None")}
                onChange={(e) => onMoistureRoleChange?.(e.target.value as MoistureRole)}
              >
                <MenuItem value="None">{t("titration.moistureRoleNone")}</MenuItem>
                <MenuItem value="Water">{t("titration.moistureRoleWater")}</MenuItem>
              </Select>
            </FormControl>
          </Stack>
        )}

        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          {select(
            "titration-titrant-label",
            kf ? t("titration.kfTitrantLabel") : t("titration.titrantLabel"),
            form.titrantSolutionMasterId,
            titrantOptions.map((s) => ({ value: s.id, label: titrantLabel(s) })),
            (id) => set({ titrantSolutionMasterId: id })
          )}
          {v.excess &&
            select(
              "titration-excess-label",
              t("titration.excessTitrantLabel"),
              form.excessSolutionMasterId,
              excessOptions.map((s) => ({ value: s.id, label: titrantLabel(s) })),
              (id) => set({ excessSolutionMasterId: id }),
              t("titration.excessTitrantHelper")
            )}
        </Stack>

        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          {v.factor && field(t("titration.equivalencyFactorLabel"), "equivalencyFactor", t("titration.equivalencyFactorHelper"), "any", true)}
          {v.excess && field(t("titration.excessVolumeLabel"), "excessVolumeMl", t("titration.excessVolumeHelper"), "any", true)}
          <TextField
            size="small"
            type="number"
            label={t("titration.replicateCountLabel")}
            value={form.replicateCount}
            onChange={(e) => set({ replicateCount: e.target.value })}
            slotProps={{ htmlInput: { min: 1, max: 10, step: 1 } }}
            helperText={t("titration.replicateCountHelper")}
            sx={{ flex: 1, minWidth: 200 }}
          />
          {field(t("titration.maxRsdLabel"), "maxRsdPercent", t("titration.maxRsdHelper"))}
        </Stack>

        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: "flex-start" }}>
          {select(
            "titration-endpoint-label",
            t("titration.endpointLabel"),
            form.endpoint,
            TITRATION_ENDPOINT_OPTIONS.map((o) => ({ value: o.value, label: t(`titration.endpoints.${o.value}`, { defaultValue: o.label }) })),
            (e) => set({ endpoint: e })
          )}
          {v.indicator &&
            select(
              "titration-indicator-label",
              t("titration.indicatorLabel"),
              form.indicatorEntryId,
              indicatorOptions.map((s) => ({ value: s.id, label: `${s.name} (${s.code})` })),
              (id) => set({ indicatorEntryId: id }),
              indicatorOptions.length === 0 ? t("titration.indicatorEmptyHelper") : t("titration.indicatorHelper")
            )}
        </Stack>

        <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap" }} useFlexGap>
          <FormControlLabel
            control={<Switch checked={form.blankRequired} onChange={(e) => set({ blankRequired: e.target.checked })} />}
            label={t("titration.blankRequiredLabel")}
          />
          {v.nonAqueous && (
            <FormControlLabel
              control={<Switch checked={form.nonAqueous} onChange={(e) => set({ nonAqueous: e.target.checked })} />}
              label={t("titration.nonAqueousLabel")}
            />
          )}
          {v.tempCorrection && (
            <FormControlLabel
              control={<Switch checked={form.tempCorrection} onChange={(e) => set({ tempCorrection: e.target.checked })} />}
              label={t("titration.tempCorrectionLabel")}
            />
          )}
        </Stack>
        {v.tempCorrection && form.tempCorrection && field(t("titration.expansionCoeffLabel"), "expansionCoefficient", t("titration.expansionCoeffHelper"), "any", true)}

        {v.standard && (
          <Stack spacing={2}>
            {select(
              "titration-standard-label",
              t("titration.referenceStandardLabel"),
              form.standardEntryId,
              standardOptions.map((s) => ({ value: s.id, label: `${s.name} (${s.code})` })),
              (id) => set({ standardEntryId: id }),
              t("titration.referenceStandardHelper")
            )}
          </Stack>
        )}
      </Stack>
    </Box>
  );
}
