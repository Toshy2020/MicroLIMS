import { frequencyLabel, frequencyRule } from "../../../components/configHierarchy";
import type { FrequencyValue } from "../../../components/configHierarchy";
import { parseFrequencyInput } from "../../../components/entry/frequencyPaste";
import type { PasteAcceptedRow, PasteMatchRow, PastePlan } from "../../../components/entry/PastePreviewDialog";
import { EM_GRADES } from "./emConfigTypes";
import type { CreateRoomPayload, EmDepartment, EmRoom, EmRoomKind } from "./emConfigTypes";

export const MAX_EM_ROOM_BATCH_ROWS = 200;

const fold = (value: string) => value.trim().toLowerCase();

export interface PendingEmRoom {
  name: string;
  departmentId: number;
  departmentName: string;
  gradeClassification: string;
  kind: EmRoomKind;
  parentRoomId: number | null;
  parentRoomName: string;
  frequency: FrequencyValue;
  frequencyLabelText: string;
  errors?: Record<string, string>;
}

/**
 * Resolves a pasted department name against the departments loaded on the page.
 */
export function resolveEmDepartment(
  raw: string,
  departments: EmDepartment[]
): { dept: EmDepartment | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { dept: null, error: "Department is required." };
  }
  const key = fold(trimmed);
  const matches = departments.filter((d) => fold(d.name) === key);
  if (matches.length === 1) {
    return { dept: matches[0], error: null };
  }
  if (matches.length > 1) {
    return { dept: null, error: `Ambiguous department '${trimmed}'.` };
  }
  return { dept: null, error: `Unknown department '${trimmed}'.` };
}

/**
 * Resolves a pasted grade string ("A" or "Grade A") against `EM_GRADES`.
 */
export function resolveEmGrade(raw: string): { grade: string | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { grade: null, error: "Grade is required." };
  }
  const stripped = trimmed.replace(/^grade\s+/i, "").trim();
  const key = fold(stripped);
  const matched = EM_GRADES.find((g) => g.toLowerCase() === key);
  if (!matched) {
    return { grade: null, error: `Unknown grade '${trimmed}'. Expected A, B, C, or D.` };
  }
  return { grade: matched, error: null };
}

/**
 * Resolves a pasted room kind ("Room", "LAF", or "LAF (laminar air flow unit)").
 * Blank defaults to "Room".
 */
export function resolveEmRoomKind(raw: string): { kind: EmRoomKind | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { kind: "Room", error: null };
  }
  const key = fold(trimmed);
  if (key === "room") {
    return { kind: "Room", error: null };
  }
  if (key === "laf" || key === "laf (laminar air flow unit)") {
    return { kind: "LAF", error: null };
  }
  return { kind: null, error: `Unknown type '${trimmed}'. Expected Room or LAF.` };
}

/**
 * Resolves the parent room for an LAF within a resolved department.
 */
export function resolveParentRoom(
  raw: string,
  dept: EmDepartment
): { parentRoom: EmRoom | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { parentRoom: null, error: "Choose the room this LAF sits in." };
  }
  const key = fold(trimmed);
  const rooms = dept.rooms ?? [];
  const matchingRooms = rooms.filter((r) => fold(r.name) === key && r.kind !== "LAF");
  if (matchingRooms.length === 1) {
    return { parentRoom: matchingRooms[0], error: null };
  }
  if (matchingRooms.length > 1) {
    return { parentRoom: null, error: `Ambiguous parent room '${trimmed}' in ${dept.name}.` };
  }
  const matchingLaf = rooms.find((r) => fold(r.name) === key && r.kind === "LAF");
  if (matchingLaf) {
    return { parentRoom: null, error: `'${trimmed}' is an LAF; an LAF must sit in a Room.` };
  }
  return { parentRoom: null, error: `Unknown parent room '${trimmed}' in ${dept.name}.` };
}

/**
 * Builds the `PastePlan` for EM rooms matching `EmRoomPanel`'s field order.
 */
export function buildEmRoomPastePlan({ departments }: { departments: EmDepartment[] }): PastePlan {
  return {
    title: "Paste preview — Rooms",
    maxRows: MAX_EM_ROOM_BATCH_ROWS,
    refusal: departments.length === 0 ? "Create a department before pasting rooms." : undefined,
    columns: [
      {
        key: "name",
        label: "Room name",
        check: (v) => {
          const trimmed = v.trim();
          if (!trimmed) return "Room name is required.";
          if (trimmed.length > 150) return "Room name must be at most 150 characters.";
          return null;
        },
        normalize: (v) => v.trim()
      },
      {
        key: "department",
        label: "Department",
        check: (v) => resolveEmDepartment(v, departments).error,
        normalize: (v) => resolveEmDepartment(v, departments).dept?.name ?? v.trim()
      },
      {
        key: "gradeClassification",
        label: "Grade",
        check: (v) => resolveEmGrade(v).error,
        normalize: (v) => resolveEmGrade(v).grade ?? v.trim()
      },
      {
        key: "kind",
        label: "Type",
        check: (v) => resolveEmRoomKind(v).error,
        normalize: (v) => resolveEmRoomKind(v).kind ?? v.trim()
      },
      {
        key: "parentRoom",
        label: "Sits in room",
        check: () => null
      },
      {
        key: "frequency",
        label: "Frequency",
        check: (v) => parseFrequencyInput(v, { weekdayRequired: false }).error,
        normalize: (v) => {
          const parsed = parseFrequencyInput(v, { weekdayRequired: false });
          return parsed.error === null ? frequencyLabel(parsed.value) : v.trim();
        }
      }
    ],
    match: (cells) => {
      const keyCounts = new Map<string, number>();
      const resolvedDepts = cells.map((r) => resolveEmDepartment(r[1] ?? "", departments).dept);

      cells.forEach((r, idx) => {
        const nameKey = fold(r[0] ?? "");
        if (!nameKey) return;
        const dept = resolvedDepts[idx];
        const deptKey = dept ? String(dept.id) : `raw:${fold(r[1] ?? "")}`;
        const composite = `${deptKey}:${nameKey}`;
        keyCounts.set(composite, (keyCounts.get(composite) ?? 0) + 1);
      });

      const rows: PasteMatchRow[] = cells.map((r, idx) => {
        const name = (r[0] ?? "").trim();
        const deptRaw = (r[1] ?? "").trim();
        const gradeRaw = (r[2] ?? "").trim();
        const kindRaw = (r[3] ?? "").trim();
        const parentRaw = (r[4] ?? "").trim();
        const freqRaw = (r[5] ?? "").trim();

        const dept = resolvedDepts[idx];
        const kindRes = resolveEmRoomKind(kindRaw);
        const rowProblems: string[] = [];

        if (dept && name) {
          const exists = (dept.rooms ?? []).some((existing) => fold(existing.name) === fold(name));
          if (exists) {
            rowProblems.push(`Already exists in ${dept.name}.`);
          }
          const composite = `${dept.id}:${fold(name)}`;
          if ((keyCounts.get(composite) ?? 0) > 1) {
            rowProblems.push("Duplicate room name in this department in paste.");
          }
        } else if (name) {
          const composite = `raw:${fold(deptRaw)}:${fold(name)}`;
          if ((keyCounts.get(composite) ?? 0) > 1) {
            rowProblems.push("Duplicate room name in paste.");
          }
        }

        let normalizedParent = parentRaw;
        if (kindRes.kind === "LAF") {
          if (dept) {
            const parentRes = resolveParentRoom(parentRaw, dept);
            if (parentRes.error) {
              rowProblems.push(`Sits in room: ${parentRes.error}`);
            } else if (parentRes.parentRoom) {
              normalizedParent = parentRes.parentRoom.name;
            }
          } else if (!parentRaw) {
            rowProblems.push("Sits in room: Choose the room this LAF sits in.");
          }
        } else if (kindRes.kind === "Room" && parentRaw) {
          rowProblems.push("Sits in room: Only an LAF sits in a parent room.");
        }

        return {
          target: null,
          values: {
            name,
            department: deptRaw,
            gradeClassification: gradeRaw,
            kind: kindRaw,
            parentRoom: normalizedParent,
            frequency: freqRaw
          },
          problem: rowProblems.length > 0 ? rowProblems.join(" ") : undefined
        };
      });

      return { rows, unmatchedTargets: [] };
    }
  };
}

/**
 * Converts rows accepted from `PastePreviewDialog` into typed pending EM rooms.
 */
export function toPendingEmRooms(
  accepted: PasteAcceptedRow[],
  departments: EmDepartment[]
): PendingEmRoom[] {
  return accepted.map((row) => {
    const dept = resolveEmDepartment(row.values.department ?? "", departments).dept;
    const grade = resolveEmGrade(row.values.gradeClassification ?? "").grade ?? (row.values.gradeClassification ?? "").trim();
    const kind = resolveEmRoomKind(row.values.kind ?? "").kind ?? "Room";
    const parent =
      kind === "LAF" && dept
        ? resolveParentRoom(row.values.parentRoom ?? "", dept).parentRoom
        : null;
    const freq = parseFrequencyInput(row.values.frequency ?? "", { weekdayRequired: false }).value;

    return {
      name: (row.values.name ?? "").trim(),
      departmentId: dept?.id ?? 0,
      departmentName: dept?.name ?? (row.values.department ?? "").trim(),
      gradeClassification: grade,
      kind,
      parentRoomId: parent?.id ?? null,
      parentRoomName: parent?.name ?? "",
      frequency: freq,
      frequencyLabelText: frequencyLabel(freq)
    };
  });
}

/**
 * Maps a pending EM room into the `CreateRoomRequest` DTO for the batch endpoint.
 */
export function toCreateRoomPayload(room: PendingEmRoom): CreateRoomPayload {
  return {
    name: room.name,
    departmentId: room.departmentId,
    gradeClassification: room.gradeClassification,
    kind: room.kind,
    parentRoomId: room.kind === "LAF" ? room.parentRoomId : null,
    frequency: frequencyRule(room.frequency)
  };
}
