import { useEffect, useMemo, useState } from "react";
import { Box, FormControl, FormControlLabel, FormHelperText, InputLabel, MenuItem, Select, Stack, Switch, TextField, Typography } from "@mui/material";
import { SolutionMasterService, SolutionMaster } from "./services/SolutionMasterService";
import { MaterialMasterService, MaterialMasterEntry } from "./services/MaterialMasterService";
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
}

// Titration configuration block of the physicochemical Test Master dialog.
// Visibility rules live in titrationConfig.ts (unit tested).
export function TitrationConfigSection({ form, onChange, sectionId }: Props) {
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
      .catch(() => active && setLoadError("Could not load titrants, indicators or reference standards."));
    return () => {
      active = false;
    };
  }, []);

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
  const standardOptions = useMemo(
    () => standards.filter(inSection),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [standards, sectionId]
  );

  const indicatorOptions = useMemo(
    () => indicators.filter(inSection),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [indicators, sectionId]
  );

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
      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>Titration Settings (USP &lt;541&gt;)</Typography>
      {loadError && <Typography color="error" sx={{ fontSize: 12, mb: 1 }}>{loadError}</Typography>}
      <Stack spacing={2}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          {select("titration-type-label", "Titration type", form.type, TITRATION_TYPE_OPTIONS, (t) => set({ type: t, titrantSolutionMasterId: "", excessSolutionMasterId: "" }))}
          {v.mode && select("titration-mode-label", "Mode", form.mode, TITRATION_MODE_OPTIONS, (m) => set({ mode: m }))}
          {v.calculation && select("titration-calc-label", "Calculation method", form.calculation, TITRATION_CALCULATION_OPTIONS.filter((o) => o.value !== "Relative" || form.mode !== "Residual"), (c) => set({ calculation: c }))}
        </Stack>
        {kf && (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Karl Fischer is direct, USP-factor only; the titrant strength is the reagent water equivalent (mg H2O/mL).
          </Typography>
        )}

        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          {select(
            "titration-titrant-label",
            kf ? "KF reagent (titrant)" : "Titrant",
            form.titrantSolutionMasterId,
            titrantOptions.map((s) => ({ value: s.id, label: titrantLabel(s) })),
            (id) => set({ titrantSolutionMasterId: id })
          )}
          {v.excess &&
            select(
              "titration-excess-label",
              "Excess titrant",
              form.excessSolutionMasterId,
              excessOptions.map((s) => ({ value: s.id, label: titrantLabel(s) })),
              (id) => set({ excessSolutionMasterId: id }),
              "The volumetric solution added in excess (same strength unit as the titrant)"
            )}
        </Stack>

        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          {v.factor && field("Equivalency factor F (mg per mEq/mmol)", "equivalencyFactor", "e.g. 88.06 (ascorbic acid, iodine)", "any", true)}
          {v.excess && field("Excess volume (mL)", "excessVolumeMl", "Required for residual titration", "any", true)}
          <TextField
            size="small"
            type="number"
            label="Replicate count *"
            value={form.replicateCount}
            onChange={(e) => set({ replicateCount: e.target.value })}
            slotProps={{ htmlInput: { min: 1, max: 10, step: 1 } }}
            helperText="1-10"
            sx={{ flex: 1, minWidth: 200 }}
          />
          {field("Maximum RSD % (optional)", "maxRsdPercent", "Over replicate results")}
        </Stack>

        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: "flex-start" }}>
          {select("titration-endpoint-label", "Endpoint", form.endpoint, TITRATION_ENDPOINT_OPTIONS, (e) => set({ endpoint: e }))}
          {v.indicator &&
            select(
              "titration-indicator-label",
              "Indicator",
              form.indicatorEntryId,
              indicatorOptions.map((s) => ({ value: s.id, label: `${s.name} (${s.code})` })),
              (id) => set({ indicatorEntryId: id }),
              indicatorOptions.length === 0 ? "No active Indicator entries in Reagents & Reference Standards" : "From Reagents & Reference Standards"
            )}
        </Stack>

        <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap" }} useFlexGap>
          <FormControlLabel
            control={<Switch checked={form.blankRequired} onChange={(e) => set({ blankRequired: e.target.checked })} />}
            label="Blank determination required (one per series)"
          />
          {v.nonAqueous && (
            <FormControlLabel
              control={<Switch checked={form.nonAqueous} onChange={(e) => set({ nonAqueous: e.target.checked })} />}
              label="Non-aqueous"
            />
          )}
          {v.tempCorrection && (
            <FormControlLabel
              control={<Switch checked={form.tempCorrection} onChange={(e) => set({ tempCorrection: e.target.checked })} />}
              label="Temperature volume correction"
            />
          )}
        </Stack>
        {v.tempCorrection && form.tempCorrection && field("Expansion coefficient (per deg C)", "expansionCoefficient", "Default 0.0011", "any", true)}

        {v.standard && (
          <Stack spacing={2}>
            {select(
              "titration-standard-label",
              "Reference standard",
              form.standardEntryId,
              standardOptions.map((s) => ({ value: s.id, label: `${s.name} (${s.code})` })),
              (id) => set({ standardEntryId: id }),
              "Reference standard lot or approved working standard of this entry is picked at analysis time"
            )}
          </Stack>
        )}
      </Stack>
    </Box>
  );
}
