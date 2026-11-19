import { useEffect, useState } from "react";
import { useTranslation } from "../../../../i18n";
import { MenuItem, Stack, TextField } from "@mui/material";
import { FrequencyValue, LimitFields, LimitValues, PanelSection, SidePanel, StructuredFrequencyField, emptyFrequency, emptyLimits, frequencyFrom, frequencyLabel, frequencyRule } from "../../../../components/configHierarchy";
import { TestCodePicker } from "../../../../components/TestCodePicker";
import { RecordChangedBanner } from "../../../../components/RecordChangedBanner";
import { noteOwnSave } from "../../../../hooks/useRecordChanged";
import { useReloadedRecord } from "../../../../hooks/useReloadedRecord";
import { EMConfigService } from "../services/EMConfigService";
import { EM_GRADES, EM_TEST_TYPES, EmDepartment, EmRoom, RoomTestConfig, emTestType } from "../emConfigTypes";

const errorText = (e: any, fallback: string) => e?.response?.data?.message ?? fallback;

interface DeptProps {
  open: boolean;
  dept: EmDepartment | null;
  onClose: () => void;
  onSaved: (message: string, id?: number) => void;
}

export function EmDepartmentPanel({ open, dept: openedDept, onClose, onSaved }: DeptProps) {
  const { t } = useTranslation("labConfig");
  const { current: dept, reload } = useReloadedRecord<EmDepartment>(openedDept, async (d) =>
    ((await EMConfigService.getDepartments()) as EmDepartment[]).find((x) => x.id === d.id)
  );
  // Set when the user changes a field, cleared whenever the form is (re)loaded from the record.
  const [touched, setTouched] = useState(false);
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
    setTouched(false);
    setError(null);
  }, [open, dept]);

  const save = async () => {
    if (!name.trim()) return setError(t("environmentalMonitoring.dept.nameRequired"));
    setSaving(true);
    setError(null);
    try {
      if (dept) {
        await EMConfigService.updateDepartment(dept.id, name.trim(), cls, frequencyLabel(frequency), dept.version, frequencyRule(frequency));
        noteOwnSave("department", dept.id);
        onSaved(t("environmentalMonitoring.dept.updated", { name: name.trim() }), dept.id);
      } else {
        const created = await EMConfigService.createDepartment(name.trim(), cls, frequencyLabel(frequency), frequencyRule(frequency));
        onSaved(t("environmentalMonitoring.dept.added", { name: name.trim() }), created?.id);
      }
    } catch (e) {
      setError(errorText(e, t("environmentalMonitoring.dept.saveError")));
    } finally {
      setSaving(false);
    }
  };

  return (
    <SidePanel
      open={open}
      overline={t("environmentalMonitoring.dept.overline")}
      title={dept ? t("environmentalMonitoring.dept.editTitle", { name: dept.name }) : t("environmentalMonitoring.dept.addTitle")}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={dept ? t("environmentalMonitoring.dept.saveChanges") : t("environmentalMonitoring.dept.addTitle")}
      error={error}
    >
      <Stack spacing={1.5}>
        {open && dept && <RecordChangedBanner type="department" id={dept.id} dirty={touched} onReload={() => void reload()} />}
        <TextField autoFocus size="small" label={t("environmentalMonitoring.dept.name")} required value={name} onChange={(e) => { setTouched(true); setName(e.target.value); }} />
        <TextField size="small" label={t("environmentalMonitoring.dept.class")} placeholder={t("environmentalMonitoring.dept.classPlaceholder")} value={cls} onChange={(e) => { setTouched(true); setCls(e.target.value); }} />
        <StructuredFrequencyField idPrefix="em-dept-frequency" value={frequency} onChange={(v) => { setTouched(true); setFrequency(v); }} />
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

export function EmRoomPanel({ open, room: openedRoom, defaultDepartmentId, departments, onClose, onSaved }: RoomProps) {
  const { t } = useTranslation("labConfig");
  const { current: room, reload } = useReloadedRecord<EmRoom>(openedRoom, async (r) =>
    ((await EMConfigService.getRooms()) as EmRoom[]).find((x) => x.id === r.id)
  );
  const [touched, setTouched] = useState(false);
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
    setTouched(false);
    setError(null);
  }, [open, room, defaultDepartmentId]);

  const department = departments.find((d) => d.id === departmentId);
  const parentRooms = (department?.rooms ?? []).filter((r) => r.kind !== "LAF" && r.id !== room?.id);

  const save = async () => {
    if (!name.trim() || departmentId === "") return setError(t("environmentalMonitoring.roomPanel.required"));
    if (kind === "LAF" && parentRoomId === "") return setError(t("environmentalMonitoring.roomPanel.lafParent"));
    setSaving(true);
    setError(null);
    const extra = { kind, parentRoomId: kind === "LAF" ? Number(parentRoomId) : null, frequency: frequencyRule(frequency) };
    try {
      if (room) {
        await EMConfigService.updateRoom(room.id, name.trim(), Number(departmentId), grade, room.version, extra);
        noteOwnSave("room", room.id);
      } else await EMConfigService.createRoom(name.trim(), Number(departmentId), grade, extra);
      onSaved(room ? t("environmentalMonitoring.roomPanel.updated", { name: name.trim() }) : t("environmentalMonitoring.roomPanel.added", { name: name.trim() }), Number(departmentId));
    } catch (e) {
      setError(errorText(e, t("environmentalMonitoring.roomPanel.saveError")));
    } finally {
      setSaving(false);
    }
  };

  return (
    <SidePanel
      open={open}
      overline={departments.find((d) => d.id === departmentId)?.name ?? t("environmentalMonitoring.roomPanel.overline")}
      title={room ? t("environmentalMonitoring.roomPanel.editTitle", { name: room.name }) : t("environmentalMonitoring.roomPanel.addTitle")}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={room ? t("environmentalMonitoring.roomPanel.saveChanges") : t("environmentalMonitoring.roomPanel.addTitle")}
      error={error}
    >
      <Stack spacing={1.5}>
        {open && room && <RecordChangedBanner type="room" id={room.id} dirty={touched} onReload={() => void reload()} />}
        <TextField autoFocus size="small" label={t("environmentalMonitoring.roomPanel.roomName")} required value={name} onChange={(e) => { setTouched(true); setName(e.target.value); }} />
        <TextField select size="small" label={t("environmentalMonitoring.roomPanel.department")} required value={departmentId} onChange={(e) => { setTouched(true); setDepartmentId(Number(e.target.value)); }}>
          {departments.map((d) => (
            <MenuItem key={d.id} value={d.id}>
              {d.name}
            </MenuItem>
          ))}
        </TextField>
        <TextField select size="small" label={t("environmentalMonitoring.roomPanel.grade")} value={grade} onChange={(e) => { setTouched(true); setGrade(e.target.value); }}>
          {EM_GRADES.map((g) => (
            <MenuItem key={g} value={g}>
              {t("environmentalMonitoring.grade", { grade: g })}
            </MenuItem>
          ))}
        </TextField>
        <TextField select size="small" label={t("environmentalMonitoring.roomPanel.type")} value={kind} onChange={(e) => { setTouched(true); setKind(e.target.value as "Room" | "LAF"); }}>
          <MenuItem value="Room">{t("environmentalMonitoring.roomPanel.typeRoom")}</MenuItem>
          <MenuItem value="LAF">{t("environmentalMonitoring.roomPanel.typeLaf")}</MenuItem>
        </TextField>
        {kind === "LAF" && (
          <TextField select size="small" label={t("environmentalMonitoring.roomPanel.sitsIn")} required value={parentRoomId} onChange={(e) => { setTouched(true); setParentRoomId(Number(e.target.value)); }}>
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
          onChange={(v) => { setTouched(true); setFrequency(v); }}
          noneLabel={t("environmentalMonitoring.roomPanel.sameAsDept")}
          inheritedHint={department && frequencyLabel(department) ? t("environmentalMonitoring.roomPanel.usesDept", { frequency: frequencyLabel(department) }) : t("environmentalMonitoring.roomPanel.noDeptFrequency")}
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
export function EmTestConfigPanel({ open, room, config: openedConfig, onClose, onSaved }: ConfigProps) {
  const { t } = useTranslation("labConfig");
  const typeLabel = (value: string) => t(`environmentalMonitoring.testTypes.${value}`, { defaultValue: emTestType(value)?.label ?? value });
  const { current: config, reload } = useReloadedRecord<RoomTestConfig>(openedConfig, async (c) =>
    ((await EMConfigService.getRoomTestConfigurations(c.roomId)) as RoomTestConfig[]).find((x) => x.id === c.id)
  );
  const [touched, setTouched] = useState(false);
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
    setTouched(false);
    setError(null);
  }, [open, config]);

  const type = emTestType(testType);

  const save = async () => {
    if (!room) return;
    if (!testCode) return setError(t("environmentalMonitoring.testPanel.chooseTest"));
    if (type?.needsUnit && !limits.unit.trim()) return setError(t("environmentalMonitoring.testPanel.unitRequired", { type: typeLabel(testType) }));
    setSaving(true);
    setError(null);
    try {
      if (config) {
        await EMConfigService.updateRoomTestConfiguration(config.id, testType, testCode, limits.alertLimit, limits.actionLimit, limits.specLimit, limits.unit, config.version);
        noteOwnSave("roomTestConfiguration", config.id);
      } else {
        await EMConfigService.createRoomTestConfiguration(room.id, testType, testCode, limits.alertLimit, limits.actionLimit, limits.specLimit, limits.unit);
      }
      onSaved(config ? t("environmentalMonitoring.testPanel.updated", { room: room.name }) : t("environmentalMonitoring.testPanel.added", { room: room.name }));
    } catch (e) {
      setError(errorText(e, t("environmentalMonitoring.testPanel.saveError")));
    } finally {
      setSaving(false);
    }
  };

  return (
    <SidePanel
      open={open}
      overline={room?.name}
      title={config ? t("environmentalMonitoring.testPanel.editTitle") : t("environmentalMonitoring.testPanel.addTitle")}
      onClose={onClose}
      onSave={save}
      saving={saving}
      saveLabel={config ? t("environmentalMonitoring.testPanel.saveChanges") : t("environmentalMonitoring.testPanel.addTitle")}
      error={error}
    >
      {open && config && <RecordChangedBanner type="roomTestConfiguration" id={config.id} dirty={touched} onReload={() => void reload()} />}
      <PanelSection title={t("environmentalMonitoring.testPanel.section1")}>
        <TextField select size="small" label={t("environmentalMonitoring.testPanel.testType")} value={testType} onChange={(e) => { setTouched(true); setTestType(e.target.value); }}>
          {EM_TEST_TYPES.map((tt) => (
            <MenuItem key={tt.value} value={tt.value}>
              {typeLabel(tt.value)}
            </MenuItem>
          ))}
        </TextField>
        <TestCodePicker value={testCode} onChange={(code) => { setTouched(true); setTestCode(code); }} label={t("environmentalMonitoring.testPanel.test")} />
      </PanelSection>
      <PanelSection
        title={t("environmentalMonitoring.testPanel.section2")}
        hint={type?.needsUnit ? t("environmentalMonitoring.testPanel.hintUnit", { type: typeLabel(testType) }) : t("environmentalMonitoring.testPanel.hintBlank")}
      >
        <LimitFields idPrefix="em-limit" value={limits} onChange={(v) => { setTouched(true); setLimits(v); }} unitRequired={type?.needsUnit} unitPlaceholder={t(`environmentalMonitoring.unitHints.${testType}`, { defaultValue: type?.unitHint })} />
      </PanelSection>
    </SidePanel>
  );
}
