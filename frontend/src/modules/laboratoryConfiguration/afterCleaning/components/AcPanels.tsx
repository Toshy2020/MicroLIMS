import { useEffect, useState } from "react";
import { MenuItem, Stack, TextField, Typography } from "@mui/material";
import { LimitFields, ChemistryLimitFields, LimitValues, PanelSection, SidePanel, emptyLimits } from "../../../../components/configHierarchy";
import { TestCodePicker } from "../../../../components/TestCodePicker";
import { useEditableTestCodes } from "../../../../hooks/useEditableTestCodes";
import { useTestDefinitions } from "../../../../hooks/useTestDefinitions";
import { AfterCleaningConfigService } from "../services/AfterCleaningConfigService";
import { AC_COUNT_TYPES, Machine, MachinePart, PATHOGEN_TEST_TYPE, PartConfig } from "../acConfigTypes";
import { cleaningTechniqueOf } from "../cleaningTechniques";
import { PathogenPicker } from "./PathogenPicker";

const errorText = (e: any, fallback: string) => e?.response?.data?.message ?? fallback;

interface MachineProps {
  open: boolean;
  machine: Machine | null;
  onClose: () => void;
  onSaved: (message: string, id?: number) => void;
}

export function AcMachinePanel({ open, machine, onClose, onSaved }: MachineProps) {
  const [name, setName] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setName(machine?.name ?? "");
    setError(null);
  }, [open, machine]);

  const save = async () => {
    if (!name.trim()) return setError("Name is required.");
    setSaving(true);
    setError(null);
    try {
      if (machine) {
        await AfterCleaningConfigService.updateMachine(machine.id, name.trim(), machine.version);
        onSaved(`"${name.trim()}" updated.`, machine.id);
      } else {
        const created = await AfterCleaningConfigService.createMachine(name.trim());
        onSaved(`"${name.trim()}" added.`, created?.id);
      }
    } catch (e) {
      setError(errorText(e, "Could not save this machine."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <SidePanel
      open={open}
      overline="Machine"
      title={machine ? `Rename ${machine.name}` : "Add machine"}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={machine ? "Save changes" : "Add machine"}
      error={error}
    >
      <TextField autoFocus size="small" label="Machine name" required placeholder="e.g. Filling line 2" value={name} onChange={(e) => setName(e.target.value)} />
    </SidePanel>
  );
}

interface PartProps {
  open: boolean;
  part: MachinePart | null;
  defaultMachineId: number | null;
  machines: Machine[];
  onClose: () => void;
  onSaved: (message: string, machineId?: number) => void;
}

export function AcPartPanel({ open, part, defaultMachineId, machines, onClose, onSaved }: PartProps) {
  const [name, setName] = useState("");
  const [machineId, setMachineId] = useState<number | "">("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setName(part?.name ?? "");
    setMachineId(part?.machineId ?? defaultMachineId ?? "");
    setError(null);
  }, [open, part, defaultMachineId]);

  const save = async () => {
    if (!name.trim() || machineId === "") return setError("Part name and machine are required.");
    setSaving(true);
    setError(null);
    try {
      if (part) await AfterCleaningConfigService.updateMachinePart(part.id, name.trim(), Number(machineId), part.version);
      else await AfterCleaningConfigService.createMachinePart(name.trim(), Number(machineId));
      onSaved(part ? `Part "${name.trim()}" updated.` : `Part "${name.trim()}" added.`, Number(machineId));
    } catch (e) {
      setError(errorText(e, "Could not save this part."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <SidePanel
      open={open}
      overline={machines.find((m) => m.id === machineId)?.name ?? "Machine part"}
      title={part ? `Edit ${part.name}` : "Add machine part"}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={part ? "Save changes" : "Add part"}
      error={error}
    >
      <Stack spacing={1.5}>
        <TextField autoFocus size="small" label="Part name" required placeholder="e.g. Filling needles" value={name} onChange={(e) => setName(e.target.value)} />
        <TextField select size="small" label="Machine" required value={machineId} onChange={(e) => setMachineId(Number(e.target.value))}>
          {machines.map((m) => (
            <MenuItem key={m.id} value={m.id}>
              {m.name}
            </MenuItem>
          ))}
        </TextField>
      </Stack>
    </SidePanel>
  );
}

interface CountProps {
  open: boolean;
  part: MachinePart | null;
  config: PartConfig | null;
  onClose: () => void;
  onSaved: (message: string) => void;
  editableCodes?: Set<string>;
}

// A swab or rinse count test or chemistry test for a part, with its limits.
export function AcCountTestPanel({ open, part, config, onClose, onSaved, editableCodes }: CountProps) {
  const { editableCodes: hookEditable } = useEditableTestCodes();
  const effectiveEditable = editableCodes !== undefined ? editableCodes : hookEditable;
  const { options: testDefinitions } = useTestDefinitions();

  const [testType, setTestType] = useState(AC_COUNT_TYPES[0].value);
  const [testCode, setTestCode] = useState("");
  const [limits, setLimits] = useState<LimitValues>(emptyLimits);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const activeDef = testDefinitions.find((t) => t.code === testCode);
  const isHplc = activeDef?.workflowType === "HplcMethodAssay";
  const isIcp = activeDef?.workflowType === "IcpMethodAssay";
  const isMethodLimit = isHplc || isIcp;
  const isPhyschem = activeDef
    ? activeDef.section?.code !== "MICRO" && activeDef.workflowType !== "CountTest"
    : config?.limitType != null;
  const technique = activeDef ? cleaningTechniqueOf(activeDef.workflowType) : (config?.limitType === "Qualitative" ? "UV" : "HPLC");
  const isUv = technique === "UV";

  useEffect(() => {
    if (!open) return;
    setTestType(config?.testType ?? AC_COUNT_TYPES[0].value);
    setTestCode(config?.testCode ?? "");
    if (config) {
      const def = testDefinitions.find((t) => t.code === config.testCode);
      const isMethod = def?.workflowType === "HplcMethodAssay" || def?.workflowType === "IcpMethodAssay";
      if (isMethod) {
        setLimits(emptyLimits);
      } else {
        const isChem = def ? def.section?.code !== "MICRO" && def.workflowType !== "CountTest" : config.limitType != null;
        const tech = def ? cleaningTechniqueOf(def.workflowType) : (config.limitType === "Qualitative" ? "UV" : "HPLC");
        setLimits({
          alertLimit: config.alertLimit ?? "",
          actionLimit: config.actionLimit ?? "",
          specLimit: config.specLimit ?? "",
          unit: config.unit ?? "",
          limitType: config.limitType ?? (isChem ? (tech === "UV" ? "Qualitative" : "Range") : "Range"),
          lowerLimit: config.lowerLimit ?? null,
          upperLimit: config.upperLimit ?? null,
          lowerInclusive: config.lowerInclusive ?? true,
          upperInclusive: config.upperInclusive ?? true,
          expectedResultText: config.expectedResultText ?? (tech === "UV" ? "No peaks" : "")
        });
      }
    } else {
      setLimits(emptyLimits);
    }
    setError(null);
  }, [open, config, testDefinitions]);

  const handleTestCodeChange = (newCode: string) => {
    setTestCode(newCode);
    const def = testDefinitions.find((t) => t.code === newCode);
    const isMethod = def?.workflowType === "HplcMethodAssay" || def?.workflowType === "IcpMethodAssay";
    if (isMethod) {
      setLimits(emptyLimits);
      return;
    }
    const isChem = def ? def.section?.code !== "MICRO" && def.workflowType !== "CountTest" : false;
    if (isChem) {
      const tech = cleaningTechniqueOf(def?.workflowType);
      if (tech === "UV") {
        setLimits((prev) => ({
          ...prev,
          limitType: "Qualitative",
          expectedResultText: prev.expectedResultText || "No peaks"
        }));
      } else {
        setLimits((prev) => ({
          ...prev,
          limitType: prev.limitType === "Qualitative" ? "Range" : (prev.limitType ?? "Range")
        }));
      }
    }
  };

  const save = async () => {
    if (!part) return;
    if (!testCode) return setError("Choose the test to run.");

    if (isPhyschem && !isMethodLimit) {
      const type = limits.limitType ?? (isUv ? "Qualitative" : "Range");
      if (type === "Range") {
        if (limits.lowerLimit == null && limits.upperLimit == null) {
          return setError("Range limit requires a lower limit, an upper limit, or both.");
        }
        if (limits.lowerLimit != null && limits.upperLimit != null && limits.lowerLimit > limits.upperLimit) {
          return setError("Lower limit cannot be greater than upper limit.");
        }
      } else if (type === "NotMoreThan") {
        if (limits.upperLimit == null) {
          return setError("Upper limit is required for Not More Than.");
        }
      } else if (type === "NotLessThan") {
        if (limits.lowerLimit == null) {
          return setError("Lower limit is required for Not Less Than.");
        }
      } else if (type === "Qualitative") {
        if (!limits.expectedResultText || limits.expectedResultText.trim() === "") {
          return setError("Expected result text is required for Qualitative.");
        }
      }
    }

    setSaving(true);
    setError(null);

    let spec = limits.specLimit;
    if (isPhyschem && !isMethodLimit) {
      const type = limits.limitType ?? (isUv ? "Qualitative" : "Range");
      if (type === "Range") {
        if (limits.lowerLimit != null && limits.upperLimit != null) spec = `${limits.lowerLimit}-${limits.upperLimit}`;
        else if (limits.lowerLimit != null) spec = `NLT ${limits.lowerLimit}`;
        else if (limits.upperLimit != null) spec = `NMT ${limits.upperLimit}`;
      } else if (type === "NotMoreThan") {
        spec = limits.upperLimit != null ? `NMT ${limits.upperLimit}` : "";
      } else if (type === "NotLessThan") {
        spec = limits.lowerLimit != null ? `NLT ${limits.lowerLimit}` : "";
      } else if (type === "Qualitative") {
        spec = limits.expectedResultText ?? "";
      }
    }

    const extra = isPhyschem && !isMethodLimit
      ? {
          limitType: limits.limitType ?? (isUv ? "Qualitative" : "Range"),
          lowerLimit: limits.lowerLimit ?? null,
          upperLimit: limits.upperLimit ?? null,
          lowerInclusive: limits.lowerInclusive ?? true,
          upperInclusive: limits.upperInclusive ?? true,
          expectedResultText: limits.expectedResultText ?? null
        }
      : undefined;

    const alertLimit = isMethodLimit ? "" : limits.alertLimit;
    const actionLimit = isMethodLimit ? "" : limits.actionLimit;
    const specLimit = isMethodLimit ? "" : (isPhyschem ? spec : limits.specLimit);
    const unit = isMethodLimit ? "" : limits.unit;

    try {
      if (config) {
        await AfterCleaningConfigService.updatePartConfiguration(
          config.id,
          testType,
          testCode,
          alertLimit,
          actionLimit,
          specLimit,
          false,
          unit,
          config.version,
          extra
        );
      } else {
        await AfterCleaningConfigService.createPartConfiguration(
          part.id,
          testType,
          testCode,
          alertLimit,
          actionLimit,
          specLimit,
          false,
          unit,
          extra
        );
      }
      onSaved(config ? `Test updated for ${part.name}.` : `Test added to ${part.name}.`);
    } catch (e) {
      setError(errorText(e, "Could not save this test."));
    } finally {
      setSaving(false);
    }
  };

  const unitHint = AC_COUNT_TYPES.find((t) => t.value === testType)?.unitHint;

  return (
    <SidePanel
      open={open}
      overline={part?.name}
      title={config ? (isPhyschem ? "Edit test" : "Edit count test") : (isPhyschem ? "Add test" : "Add swab / rinse test")}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={config ? "Save changes" : "Add test"}
      error={error}
    >
      <PanelSection title="1 · What is sampled">
        <TextField select size="small" label="Sample type" value={testType} onChange={(e) => setTestType(e.target.value)}>
          {AC_COUNT_TYPES.map((t) => (
            <MenuItem key={t.value} value={t.value}>
              {t.label}
            </MenuItem>
          ))}
        </TextField>
        <TestCodePicker value={testCode} onChange={handleTestCodeChange} label="Test" editableCodes={effectiveEditable ?? new Set<string>()} />
      </PanelSection>
      <PanelSection
        title="2 · Limits"
        hint={
          isMethodLimit
            ? undefined
            : isPhyschem
            ? (isUv ? "UV scan limit (defaults to Qualitative: 'No peaks')." : "Chemistry limits for residue testing.")
            : `Results for ${testType.toLowerCase()} samples are reported in ${unitHint}.`
        }
      >
        {isMethodLimit ? (
          <Typography variant="body2" sx={{ color: "text.secondary", py: 1 }}>
            {isHplc ? "Limit from the method" : "Limit from the ICP test"}
          </Typography>
        ) : isPhyschem ? (
          <ChemistryLimitFields idPrefix="ac-limit" value={limits} onChange={setLimits} unitPlaceholder="e.g. ppm, µg/swab" />
        ) : (
          <LimitFields idPrefix="ac-limit" value={limits} onChange={setLimits} unitPlaceholder={unitHint} />
        )}
      </PanelSection>
    </SidePanel>
  );
}

interface PathogenProps {
  open: boolean;
  part: MachinePart | null;
  // The part's current pathogen configurations.
  configs: PartConfig[];
  onClose: () => void;
  onSaved: (message: string) => void;
  editableCodes?: Set<string>;
}

// Every pathogen test for a part in one place: pick any number of
// pathogens; saving adds the new ones and removes the unticked ones
// (each is its own configuration row, so each gets its own TestOrder).
export function AcPathogenPanel({ open, part, configs, onClose, onSaved, editableCodes }: PathogenProps) {
  const { editableCodes: hookEditable } = useEditableTestCodes();
  const effectiveEditable = editableCodes !== undefined ? editableCodes : hookEditable;

  const [codes, setCodes] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setCodes(configs.map((c) => c.testCode));
    setError(null);
  }, [open, configs]);

  const added = codes.filter((code) => !configs.some((c) => c.testCode === code));
  const removed = configs.filter((c) => !codes.includes(c.testCode));

  const save = async () => {
    if (!part) return;
    setSaving(true);
    setError(null);
    const failed: string[] = [];
    for (const code of added) {
      if (effectiveEditable !== undefined && !effectiveEditable.has(code)) continue;
      try {
        await AfterCleaningConfigService.createPartConfiguration(part.id, PATHOGEN_TEST_TYPE, code, "", "", "", true, "");
      } catch (e) {
        failed.push(`${code}: ${errorText(e, "could not be added")}`);
      }
    }
    const safeRemoved = removed.filter((c) => effectiveEditable === undefined || effectiveEditable.has(c.testCode));
    for (const c of safeRemoved) {
      try {
        await AfterCleaningConfigService.deletePartConfiguration(c.id);
      } catch (e) {
        failed.push(`${c.testCode}: ${errorText(e, "could not be removed")}`);
      }
    }
    setSaving(false);
    if (failed.length > 0) {
      setError(`Some changes were not saved. ${failed.join(" ")}`);
      return;
    }
    onSaved(`Pathogen tests updated for ${part.name}.`);
  };

  return (
    <SidePanel
      open={open}
      overline={part?.name}
      title="Pathogen tests"
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel="Save pathogen tests"
      error={error}
    >
      <PanelSection
        title="Pathogens tested on this part"
        hint="Choose as many as needed. Each is a presence/absence test with its own test order - no numeric limits."
      >
        <PathogenPicker value={codes} onChange={setCodes} editableCodes={effectiveEditable ?? new Set<string>()} />
        {(added.length > 0 || removed.length > 0) && (
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
            {[added.length > 0 && `${added.length} to add`, removed.length > 0 && `${removed.length} to remove`].filter(Boolean).join(", ")}.
          </Typography>
        )}
      </PanelSection>
    </SidePanel>
  );
}
