import { useEffect, useState } from "react";
import { MenuItem, Stack, TextField } from "@mui/material";
import { FrequencyValue, LimitFields, LimitValues, PanelSection, SidePanel, StructuredFrequencyField, emptyFrequency, emptyLimits, frequencyFrom, frequencyLabel, frequencyRule } from "../../../../components/configHierarchy";
import { TestCodePicker } from "../../../../components/TestCodePicker";
import { EMConfigService } from "../services/EMConfigService";
import { EM_GRADES, EM_TEST_TYPES, EmDepartment, EmRoom, RoomTestConfig, emTestType } from "../emConfigTypes";

const errorText = (e: any, fallback: string) => e?.response?.data?.message ?? fallback;

interface DeptProps {
  open: boolean;
  dept: EmDepartment | null;
  onClose: () => void;
  onSaved: (message: string, id?: number) => void;
}

export function EmDepartmentPanel({ open, dept, onClose, onSaved }: DeptProps) {
  const [name, setName] = useState("");
  const [cls, setCls] = useState("");
  const [frequency, setFrequency] = useState<FrequencyValue>(emptyFrequency);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setName(dept?.name ?? "");
    setCls(dept?.class ?? "");
    setFrequency(dept ? frequencyFrom(dept) : emptyFrequency);
    setError(null);
  }, [open, dept]);

  const save = async () => {
    if (!name.trim()) return setError("Name is required.");
    setSaving(true);
    setError(null);
    try {
      if (dept) {
        await EMConfigService.updateDepartment(dept.id, name.trim(), cls, frequencyLabel(frequency), dept.version, frequencyRule(frequency));
        onSaved(`"${name.trim()}" updated.`, dept.id);
      } else {
        const created = await EMConfigService.createDepartment(name.trim(), cls, frequencyLabel(frequency), frequencyRule(frequency));
        onSaved(`"${name.trim()}" added.`, created?.id);
      }
    } catch (e) {
      setError(errorText(e, "Could not save this department."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <SidePanel
      open={open}
      overline="Department"
      title={dept ? `Edit ${dept.name}` : "Add department"}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={dept ? "Save changes" : "Add department"}
      error={error}
    >
      <Stack spacing={1.5}>
        <TextField autoFocus size="small" label="Name" required value={name} onChange={(e) => setName(e.target.value)} />
        <TextField size="small" label="Class" placeholder="e.g. Grade C" value={cls} onChange={(e) => setCls(e.target.value)} />
        <StructuredFrequencyField idPrefix="em-dept-frequency" value={frequency} onChange={setFrequency} />
      </Stack>
    </SidePanel>
  );
}

interface RoomProps {
  open: boolean;
  room: EmRoom | null;
  defaultDepartmentId: number | null;
  departments: EmDepartment[];
  onClose: () => void;
  onSaved: (message: string, departmentId?: number) => void;
}

export function EmRoomPanel({ open, room, defaultDepartmentId, departments, onClose, onSaved }: RoomProps) {
  const [name, setName] = useState("");
  const [departmentId, setDepartmentId] = useState<number | "">("");
  const [grade, setGrade] = useState("A");
  const [kind, setKind] = useState<"Room" | "LAF">("Room");
  const [parentRoomId, setParentRoomId] = useState<number | "">("");
  const [frequency, setFrequency] = useState<FrequencyValue>(emptyFrequency);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setName(room?.name ?? "");
    setDepartmentId(room?.departmentId ?? defaultDepartmentId ?? "");
    setGrade(room?.gradeClassification || "A");
    setKind(room?.kind ?? "Room");
    setParentRoomId(room?.parentRoomId ?? "");
    setFrequency(room ? frequencyFrom(room) : emptyFrequency);
    setError(null);
  }, [open, room, defaultDepartmentId]);

  const department = departments.find((d) => d.id === departmentId);
  const parentRooms = (department?.rooms ?? []).filter((r) => r.kind !== "LAF" && r.id !== room?.id);

  const save = async () => {
    if (!name.trim() || departmentId === "") return setError("Room name and department are required.");
    if (kind === "LAF" && parentRoomId === "") return setError("Choose the room this LAF sits in.");
    setSaving(true);
    setError(null);
    const extra = { kind, parentRoomId: kind === "LAF" ? Number(parentRoomId) : null, frequency: frequencyRule(frequency) };
    try {
      if (room) await EMConfigService.updateRoom(room.id, name.trim(), Number(departmentId), grade, room.version, extra);
      else await EMConfigService.createRoom(name.trim(), Number(departmentId), grade, extra);
      onSaved(room ? `Room "${name.trim()}" updated.` : `Room "${name.trim()}" added.`, Number(departmentId));
    } catch (e) {
      setError(errorText(e, "Could not save this room."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <SidePanel
      open={open}
      overline={departments.find((d) => d.id === departmentId)?.name ?? "Room"}
      title={room ? `Edit ${room.name}` : "Add room"}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={room ? "Save changes" : "Add room"}
      error={error}
    >
      <Stack spacing={1.5}>
        <TextField autoFocus size="small" label="Room name" required value={name} onChange={(e) => setName(e.target.value)} />
        <TextField select size="small" label="Department" required value={departmentId} onChange={(e) => setDepartmentId(Number(e.target.value))}>
          {departments.map((d) => (
            <MenuItem key={d.id} value={d.id}>
              {d.name}
            </MenuItem>
          ))}
        </TextField>
        <TextField select size="small" label="Grade" value={grade} onChange={(e) => setGrade(e.target.value)}>
          {EM_GRADES.map((g) => (
            <MenuItem key={g} value={g}>
              Grade {g}
            </MenuItem>
          ))}
        </TextField>
        <TextField select size="small" label="Type" value={kind} onChange={(e) => setKind(e.target.value as "Room" | "LAF")}>
          <MenuItem value="Room">Room</MenuItem>
          <MenuItem value="LAF">LAF (laminar air flow unit)</MenuItem>
        </TextField>
        {kind === "LAF" && (
          <TextField select size="small" label="Sits in room" required value={parentRoomId} onChange={(e) => setParentRoomId(Number(e.target.value))}>
            {parentRooms.map((r) => (
              <MenuItem key={r.id} value={r.id}>
                {r.name}
              </MenuItem>
            ))}
          </TextField>
        )}
        <StructuredFrequencyField
          idPrefix="em-room-frequency"
          value={frequency}
          onChange={setFrequency}
          noneLabel="Same as department"
          inheritedHint={department && frequencyLabel(department) ? `Uses the department's frequency: ${frequencyLabel(department)}.` : "The department has no structured frequency yet."}
        />
      </Stack>
    </SidePanel>
  );
}

interface ConfigProps {
  open: boolean;
  room: EmRoom | null;
  config: RoomTestConfig | null;
  onClose: () => void;
  onSaved: (message: string) => void;
}

// One test for a room: what is sampled (test type), which test runs,
// and its limits. Unit is required for the types without a fixed unit.
export function EmTestConfigPanel({ open, room, config, onClose, onSaved }: ConfigProps) {
  const [testType, setTestType] = useState(EM_TEST_TYPES[0].value);
  const [testCode, setTestCode] = useState("");
  const [limits, setLimits] = useState<LimitValues>(emptyLimits);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setTestType(config?.testType ?? EM_TEST_TYPES[0].value);
    setTestCode(config?.testCode ?? "");
    setLimits(
      config
        ? { alertLimit: config.alertLimit ?? "", actionLimit: config.actionLimit ?? "", specLimit: config.specLimit ?? "", unit: config.unit ?? "" }
        : emptyLimits
    );
    setError(null);
  }, [open, config]);

  const type = emTestType(testType);

  const save = async () => {
    if (!room) return;
    if (!testCode) return setError("Choose the test to run.");
    if (type?.needsUnit && !limits.unit.trim()) return setError(`Unit is required for ${type.label}.`);
    setSaving(true);
    setError(null);
    try {
      if (config) {
        await EMConfigService.updateRoomTestConfiguration(config.id, testType, testCode, limits.alertLimit, limits.actionLimit, limits.specLimit, limits.unit, config.version);
      } else {
        await EMConfigService.createRoomTestConfiguration(room.id, testType, testCode, limits.alertLimit, limits.actionLimit, limits.specLimit, limits.unit);
      }
      onSaved(config ? `Test updated for ${room.name}.` : `Test added to ${room.name}.`);
    } catch (e) {
      setError(errorText(e, "Could not save this test."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <SidePanel
      open={open}
      overline={room?.name}
      title={config ? "Edit test" : "Add test"}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={config ? "Save changes" : "Add test"}
      error={error}
    >
      <PanelSection title="1 · What is sampled">
        <TextField select size="small" label="Test type" value={testType} onChange={(e) => setTestType(e.target.value)}>
          {EM_TEST_TYPES.map((t) => (
            <MenuItem key={t.value} value={t.value}>
              {t.label}
            </MenuItem>
          ))}
        </TextField>
        <TestCodePicker value={testCode} onChange={setTestCode} label="Test" />
      </PanelSection>
      <PanelSection
        title="2 · Limits"
        hint={type?.needsUnit ? `${type.label} reports the unit you enter here, so it is required.` : "Leave a limit blank if it does not apply."}
      >
        <LimitFields idPrefix="em-limit" value={limits} onChange={setLimits} unitRequired={type?.needsUnit} unitPlaceholder={type?.unitHint} />
      </PanelSection>
    </SidePanel>
  );
}
