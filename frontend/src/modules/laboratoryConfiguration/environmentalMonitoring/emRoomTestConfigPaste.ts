import type { PasteAcceptedRow, PasteMatchRow, PastePlan } from "../../../components/entry/PastePreviewDialog";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import { EM_TEST_TYPES, emTestTypeLabel } from "./emConfigTypes";
import type { CreateRoomTestConfigPayload, EmDepartment, EmRoom, RoomTestConfig } from "./emConfigTypes";

export const MAX_EM_CONFIG_BATCH_ROWS = 200;

const fold = (value: string) => value.trim().toLowerCase();

export interface EmTestTypeOption {
  value: string;
  label: string;
  unitHint: string;
  needsUnit: boolean;
}

export interface PendingEmTestConfig {
  roomId: number;
  roomName: string;
  departmentName: string;
  roomLabel: string;
  testType: string;
  testTypeLabel: string;
  testCode: string;
  alertLimit: string;
  actionLimit: string;
  specLimit: string;
  unit: string;
  errors?: Record<string, string>;
}

interface RoomEntry {
  room: EmRoom;
  department: EmDepartment;
}

function flattenRooms(departments: EmDepartment[]): RoomEntry[] {
  return departments.flatMap((department) =>
    (department.rooms ?? []).map((room) => ({ room, department }))
  );
}

/**
 * Resolves a pasted room name ("Room" or "Department / Room") against rooms loaded on the page.
 * If the same room name exists in more than one department, requires "Department / Room" or reports ambiguous.
 */
export function resolveEmRoom(
  raw: string,
  departments: EmDepartment[]
): { room: EmRoom | null; department: EmDepartment | null; isDuplicateName: boolean; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { room: null, department: null, isDuplicateName: false, error: "Room is required." };
  }

  const entries = flattenRooms(departments);
  const key = fold(trimmed);

  const byRoomName = entries.filter((e) => fold(e.room.name) === key);
  if (byRoomName.length === 1) {
    return {
      room: byRoomName[0].room,
      department: byRoomName[0].department,
      isDuplicateName: false,
      error: null
    };
  }
  if (byRoomName.length > 1) {
    return {
      room: null,
      department: null,
      isDuplicateName: true,
      error: `Ambiguous room '${trimmed}' (exists in multiple departments; use 'Department / Room').`
    };
  }

  if (trimmed.includes("/")) {
    const slashMatches: RoomEntry[] = [];
    for (let idx = trimmed.indexOf("/"); idx !== -1; idx = trimmed.indexOf("/", idx + 1)) {
      const deptKey = fold(trimmed.slice(0, idx));
      const roomKey = fold(trimmed.slice(idx + 1));
      if (!deptKey || !roomKey) continue;
      for (const entry of entries) {
        if (fold(entry.department.name) === deptKey && fold(entry.room.name) === roomKey) {
          slashMatches.push(entry);
        }
      }
    }

    if (slashMatches.length === 1) {
      const matched = slashMatches[0];
      const sameRoomNameCount = entries.filter((e) => fold(e.room.name) === fold(matched.room.name)).length;
      return {
        room: matched.room,
        department: matched.department,
        isDuplicateName: sameRoomNameCount > 1,
        error: null
      };
    }
    if (slashMatches.length > 1) {
      return { room: null, department: null, isDuplicateName: true, error: `Ambiguous room '${trimmed}'.` };
    }
  }

  return { room: null, department: null, isDuplicateName: false, error: `Unknown room '${trimmed}'.` };
}

/**
 * Resolves an EM test type by its code/value or display label.
 */
export function resolveEmTestType(
  raw: string
): { testType: EmTestTypeOption | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { testType: null, error: "Test type is required." };
  }
  const key = fold(trimmed);
  const matched = EM_TEST_TYPES.find((t) => fold(t.value) === key || fold(t.label) === key);
  if (!matched) {
    return { testType: null, error: `Unknown test type '${trimmed}'.` };
  }
  return { testType: matched, error: null };
}

/**
 * Resolves a single test code or display name against loaded Test Master options.
 */
export function resolveEmTestCode(
  raw: string,
  options: TestDefinitionOption[]
): { option: TestDefinitionOption | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { option: null, error: "Test is required." };
  }
  const key = fold(trimmed);
  const byCode = options.filter((o) => fold(o.code) === key);
  if (byCode.length === 1) {
    return { option: byCode[0], error: null };
  }
  if (byCode.length > 1) {
    return { option: null, error: `Ambiguous test '${trimmed}'.` };
  }
  const byName = options.filter((o) => fold(o.displayName) === key);
  if (byName.length === 1) {
    return { option: byName[0], error: null };
  }
  if (byName.length > 1) {
    return { option: null, error: `Ambiguous test '${trimmed}'.` };
  }
  return { option: null, error: `Unknown test '${trimmed}'.` };
}

function formatRoomDisplay(raw: string, departments: EmDepartment[]): string {
  const resolved = resolveEmRoom(raw, departments);
  if (!resolved.room || !resolved.department) return raw.trim();
  return resolved.isDuplicateName || raw.includes("/")
    ? `${resolved.department.name} / ${resolved.room.name}`
    : resolved.room.name;
}

/**
 * Builds the `PastePlan` for EM room test configurations matching `EmTestConfigPanel`'s field order.
 * Limits are pasted as text and kept unchanged; the backend owns all limit logic.
 */
export function buildEmRoomTestConfigPastePlan({
  departments,
  configsByRoom,
  testOptions
}: {
  departments: EmDepartment[];
  configsByRoom: Record<number, RoomTestConfig[]>;
  testOptions: TestDefinitionOption[];
}): PastePlan {
  const allRooms = flattenRooms(departments);

  return {
    title: "Paste preview — Room test configurations",
    maxRows: MAX_EM_CONFIG_BATCH_ROWS,
    refusal: allRooms.length === 0 ? "Create a room before pasting test configurations." : undefined,
    columns: [
      {
        key: "room",
        label: "Room",
        check: (v) => resolveEmRoom(v, departments).error,
        normalize: (v) => formatRoomDisplay(v, departments)
      },
      {
        key: "testType",
        label: "Test type",
        check: (v) => resolveEmTestType(v).error,
        normalize: (v) => resolveEmTestType(v).testType?.label ?? v.trim()
      },
      {
        key: "testCode",
        label: "Test",
        check: (v) => resolveEmTestCode(v, testOptions).error,
        normalize: (v) => resolveEmTestCode(v, testOptions).option?.code ?? v.trim()
      },
      {
        key: "alertLimit",
        label: "Alert limit",
        check: () => null
      },
      {
        key: "actionLimit",
        label: "Action limit",
        check: () => null
      },
      {
        key: "specLimit",
        label: "Spec limit",
        check: () => null
      },
      {
        key: "unit",
        label: "Unit",
        check: () => null
      }
    ],
    match: (cells) => {
      const keyCounts = new Map<string, number>();
      const resolvedRows = cells.map((r) => {
        const roomRes = resolveEmRoom(r[0] ?? "", departments);
        const typeRes = resolveEmTestType(r[1] ?? "");
        const testRes = resolveEmTestCode(r[2] ?? "", testOptions);
        const composite =
          roomRes.room && typeRes.testType && testRes.option
            ? `${roomRes.room.id}:${fold(typeRes.testType.value)}:${fold(testRes.option.code)}`
            : null;
        if (composite) {
          keyCounts.set(composite, (keyCounts.get(composite) ?? 0) + 1);
        }
        return { roomRes, typeRes, testRes, composite };
      });

      const rows: PasteMatchRow[] = cells.map((r, idx) => {
        const { roomRes, typeRes, testRes, composite } = resolvedRows[idx];
        const rowProblems: string[] = [];

        if (composite && roomRes.room && typeRes.testType && testRes.option) {
          const existingActive = (configsByRoom[roomRes.room.id] ?? []).some(
            (c) =>
              c.isActive !== false &&
              fold(c.testType) === fold(typeRes.testType!.value) &&
              fold(c.testCode) === fold(testRes.option!.code)
          );
          if (existingActive) {
            rowProblems.push("An active configuration for this room, test type, and test already exists.");
          }
          if ((keyCounts.get(composite) ?? 0) > 1) {
            rowProblems.push("Duplicate room, test type, and test in paste.");
          }
        }

        return {
          target: null,
          values: {
            room: (r[0] ?? "").trim(),
            testType: (r[1] ?? "").trim(),
            testCode: (r[2] ?? "").trim(),
            // Keep limits strictly unchanged as pasted text; backend owns all limit evaluation/formatting.
            alertLimit: r[3] ?? "",
            actionLimit: r[4] ?? "",
            specLimit: r[5] ?? "",
            unit: r[6] ?? ""
          },
          problem: rowProblems.length > 0 ? rowProblems.join(" ") : undefined
        };
      });

      return { rows, unmatchedTargets: [] };
    }
  };
}

/**
 * Converts rows accepted from `PastePreviewDialog` into typed pending EM room test configurations.
 */
export function toPendingEmTestConfigs(
  accepted: PasteAcceptedRow[],
  departments: EmDepartment[],
  testOptions: TestDefinitionOption[]
): PendingEmTestConfig[] {
  return accepted.map((row) => {
    const roomRes = resolveEmRoom(row.values.room ?? "", departments);
    const typeRes = resolveEmTestType(row.values.testType ?? "");
    const testRes = resolveEmTestCode(row.values.testCode ?? "", testOptions);

    const roomName = roomRes.room?.name ?? (row.values.room ?? "").trim();
    const departmentName = roomRes.department?.name ?? "";
    const roomLabel =
      roomRes.isDuplicateName && departmentName ? `${departmentName} / ${roomName}` : roomName;
    const testType = typeRes.testType?.value ?? (row.values.testType ?? "").trim();

    return {
      roomId: roomRes.room?.id ?? 0,
      roomName,
      departmentName,
      roomLabel,
      testType,
      testTypeLabel: emTestTypeLabel(testType),
      testCode: testRes.option?.code ?? (row.values.testCode ?? "").trim(),
      alertLimit: row.values.alertLimit ?? "",
      actionLimit: row.values.actionLimit ?? "",
      specLimit: row.values.specLimit ?? "",
      unit: (row.values.unit ?? "").trim()
    };
  });
}

/**
 * Maps a pending configuration into the `CreateRoomTestConfigRequest` DTO for the batch endpoint.
 */
export function toCreateRoomTestConfigPayload(cfg: PendingEmTestConfig): CreateRoomTestConfigPayload {
  return {
    roomId: cfg.roomId,
    testType: cfg.testType,
    testCode: cfg.testCode,
    alertLimit: cfg.alertLimit,
    actionLimit: cfg.actionLimit,
    specLimit: cfg.specLimit,
    unit: cfg.unit.trim() === "" ? null : cfg.unit.trim()
  };
}
