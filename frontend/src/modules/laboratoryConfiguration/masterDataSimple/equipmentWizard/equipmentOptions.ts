export const EQUIPMENT_TYPES = [
  { value: "Incubator", label: "Incubator" },
  { value: "Autoclave", label: "Autoclave" },
  { value: "LafCabinet", label: "LAF Cabinet" },
  { value: "BiologicalSafetyCabinet", label: "Biological Safety Cabinet" },
  { value: "WaterBath", label: "Water Bath" },
  { value: "Hplc", label: "HPLC" },
  { value: "PhMeter", label: "pH Meter" },
  { value: "Balance", label: "Balance" },
  { value: "IcpOes", label: "ICP-OES" },
  { value: "Other", label: "Other" }
];

// HPLC, ICP-OES, pH meters and balances belong to the Finished Product laboratory
// and are configured on the FP Instruments page, not here.
export const FP_ONLY_EQUIPMENT_TYPES = ["Hplc", "PhMeter", "Balance", "IcpOes"];
export const MICRO_EQUIPMENT_TYPES = EQUIPMENT_TYPES.filter((t) => !FP_ONLY_EQUIPMENT_TYPES.includes(t.value));

export const CDS_SOFTWARE_OPTIONS = [
  { value: "ShimadzuLabSolutions", label: "Shimadzu LabSolutions" },
  { value: "AgilentOpenLab", label: "Agilent OpenLab" },
  { value: "WatersEmpower3", label: "Waters Empower 3" },
  { value: "PerkinElmerSyngistix", label: "PerkinElmer Syngistix" }
];

// Which settings each equipment type has. The settings step shows only these.
export const SET_POINT_TYPES: readonly string[] = ["Incubator", "WaterBath"];
// Connection settings are a general, optional field that the old dialog offered for every type and
// nothing on the server interprets per type, so every type keeps it (no configuration option is removed).
export const CONNECTION_TYPES: readonly string[] = EQUIPMENT_TYPES.map((t) => t.value);
export const CDS_TYPES: readonly string[] = ["Hplc"];

export const equipmentTypeLabel = (type: string): string =>
  EQUIPMENT_TYPES.find((t) => t.value === type)?.label ?? type;
