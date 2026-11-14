import { describe, expect, it } from "vitest";
import type { EmDepartment } from "./emConfigTypes";
import {
  MAX_EM_ROOM_BATCH_ROWS,
  buildEmRoomPastePlan,
  resolveEmDepartment,
  resolveEmGrade,
  resolveEmRoomKind,
  resolveParentRoom,
  toCreateRoomPayload,
  toPendingEmRooms
} from "./emRoomPaste";

const DEPARTMENTS: EmDepartment[] = [
  {
    id: 1,
    name: "Sterile Filling",
    class: "Grade B",
    testingFrequency: "Daily",
    rooms: [
      { id: 10, name: "Filling Room 1", departmentId: 1, gradeClassification: "B", kind: "Room" },
      { id: 11, name: "LAF-01", departmentId: 1, gradeClassification: "A", kind: "LAF", parentRoomId: 10 }
    ]
  },
  {
    id: 2,
    name: "Packaging",
    class: "Grade D",
    testingFrequency: "Weekly",
    rooms: [
      { id: 20, name: "Pack Line 1", departmentId: 2, gradeClassification: "D", kind: "Room" }
    ]
  }
];

describe("EM room paste resolvers", () => {
  it("resolves departments case-insensitively and flags unknown or ambiguous names", () => {
    expect(resolveEmDepartment("sterile filling", DEPARTMENTS).dept?.id).toBe(1);
    expect(resolveEmDepartment("", DEPARTMENTS).error).toMatch(/required/i);
    expect(resolveEmDepartment("Unknown", DEPARTMENTS).error).toMatch(/unknown department/i);

    const dupDepts: EmDepartment[] = [
      { id: 1, name: "Filling", class: "A", testingFrequency: "", rooms: [] },
      { id: 2, name: "filling", class: "B", testingFrequency: "", rooms: [] }
    ];
    expect(resolveEmDepartment("Filling", dupDepts).error).toMatch(/ambiguous department/i);
  });

  it("resolves grades with or without Grade prefix", () => {
    expect(resolveEmGrade("a")).toEqual({ grade: "A", error: null });
    expect(resolveEmGrade("Grade C")).toEqual({ grade: "C", error: null });
    expect(resolveEmGrade("")).toEqual({ grade: null, error: "Grade is required." });
    expect(resolveEmGrade("Grade E").error).toMatch(/unknown grade/i);
  });

  it("resolves room kinds and defaults blank to Room", () => {
    expect(resolveEmRoomKind("")).toEqual({ kind: "Room", error: null });
    expect(resolveEmRoomKind("room")).toEqual({ kind: "Room", error: null });
    expect(resolveEmRoomKind("laf")).toEqual({ kind: "LAF", error: null });
    expect(resolveEmRoomKind("LAF (laminar air flow unit)")).toEqual({ kind: "LAF", error: null });
    expect(resolveEmRoomKind("Isolator").error).toMatch(/unknown type/i);
  });

  it("resolves parent rooms for LAFs and rejects another LAF as parent", () => {
    expect(resolveParentRoom("filling room 1", DEPARTMENTS[0]).parentRoom?.id).toBe(10);
    expect(resolveParentRoom("", DEPARTMENTS[0]).error).toMatch(/choose the room/i);
    expect(resolveParentRoom("LAF-01", DEPARTMENTS[0]).error).toMatch(/is an LAF/i);
    expect(resolveParentRoom("Pack Line 1", DEPARTMENTS[0]).error).toMatch(/unknown parent room/i);
  });
});

describe("buildEmRoomPastePlan", () => {
  it("exposes columns matching the create form and flags existing, duplicate, and LAF parent errors", () => {
    const plan = buildEmRoomPastePlan({ departments: DEPARTMENTS });

    expect(plan.maxRows).toBe(MAX_EM_ROOM_BATCH_ROWS);
    expect(plan.columns.map((c) => c.label)).toEqual([
      "Room name",
      "Department",
      "Grade",
      "Type",
      "Sits in room",
      "Frequency"
    ]);

    const matched = plan.match?.([
      ["Filling Room 1", "Sterile Filling", "B", "Room", "", "Daily"],
      ["Filling Room 1", "Packaging", "D", "Room", "", "Weekly"],
      ["New Room", "Packaging", "C", "Room", "", ""],
      ["new room", "Packaging", "C", "Room", "", ""],
      ["LAF-New", "Sterile Filling", "A", "LAF", "", ""],
      ["Room-With-Parent", "Packaging", "D", "Room", "Pack Line 1", ""]
    ]);

    expect(matched?.rows[0].problem).toMatch(/already exists in Sterile Filling/i);
    // Same room name in a different department is allowed
    expect(matched?.rows[1].problem).toBeUndefined();
    expect(matched?.rows[2].problem).toMatch(/duplicate room name/i);
    expect(matched?.rows[3].problem).toMatch(/duplicate room name/i);
    expect(matched?.rows[4].problem).toMatch(/choose the room this laf sits in/i);
    expect(matched?.rows[5].problem).toMatch(/only an laf sits in a parent room/i);
  });

  it("allows weekly frequency without a weekday and builds exact CreateRoomRequest payloads", () => {
    const plan = buildEmRoomPastePlan({ departments: DEPARTMENTS });
    expect(plan.columns[5].check("Weekly")).toBeNull();

    const pending = toPendingEmRooms(
      [
        {
          target: null,
          values: {
            name: "Cleanroom B",
            department: "Sterile Filling",
            gradeClassification: "B",
            kind: "Room",
            parentRoom: "",
            frequency: "Weekly"
          }
        },
        {
          target: null,
          values: {
            name: "LAF-02",
            department: "Sterile Filling",
            gradeClassification: "A",
            kind: "LAF",
            parentRoom: "Filling Room 1",
            frequency: ""
          }
        }
      ],
      DEPARTMENTS
    );

    expect(toCreateRoomPayload(pending[0])).toEqual({
      name: "Cleanroom B",
      departmentId: 1,
      gradeClassification: "B",
      kind: "Room",
      parentRoomId: null,
      frequency: {
        type: "Weekly",
        interval: 1,
        samplingWeekday: null,
        anchorMonth: null,
        weekOfMonth: null
      }
    });

    expect(toCreateRoomPayload(pending[1])).toEqual({
      name: "LAF-02",
      departmentId: 1,
      gradeClassification: "A",
      kind: "LAF",
      parentRoomId: 10,
      frequency: { type: null }
    });
  });
});
