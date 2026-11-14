import { describe, expect, it } from "vitest";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import type { EmDepartment, RoomTestConfig } from "./emConfigTypes";
import {
  MAX_EM_CONFIG_BATCH_ROWS,
  buildEmRoomTestConfigPastePlan,
  resolveEmRoom,
  resolveEmTestCode,
  resolveEmTestType,
  toCreateRoomTestConfigPayload,
  toPendingEmTestConfigs
} from "./emRoomTestConfigPaste";

const DEPARTMENTS: EmDepartment[] = [
  {
    id: 1,
    name: "Sterile Filling",
    class: "Grade B",
    testingFrequency: "Daily",
    rooms: [
      { id: 10, name: "Filling Room 1", departmentId: 1, gradeClassification: "B", kind: "Room" },
      { id: 11, name: "Airlock", departmentId: 1, gradeClassification: "C", kind: "Room" }
    ]
  },
  {
    id: 2,
    name: "Packaging",
    class: "Grade D",
    testingFrequency: "Weekly",
    rooms: [
      { id: 20, name: "Pack Line 1", departmentId: 2, gradeClassification: "D", kind: "Room" },
      { id: 21, name: "Airlock", departmentId: 2, gradeClassification: "D", kind: "Room" }
    ]
  }
];

const TEST_OPTIONS: TestDefinitionOption[] = [
  { id: 1, code: "TAMC-EM", displayName: "Total Aerobic Count", isActive: true, workflowType: "CountTest" },
  { id: 2, code: "TYMC-EM", displayName: "Total Yeast & Mold", isActive: true, workflowType: "CountTest" },
  { id: 3, code: "DUP-A", displayName: "Duplicate Name", isActive: true, workflowType: "CountTest" },
  { id: 4, code: "DUP-B", displayName: "Duplicate Name", isActive: true, workflowType: "CountTest" }
];

describe("EM room test config resolvers", () => {
  it("resolves unique rooms by name and requires Department / Room when ambiguous", () => {
    expect(resolveEmRoom("filling room 1", DEPARTMENTS).room?.id).toBe(10);
    expect(resolveEmRoom("Airlock", DEPARTMENTS).error).toMatch(/ambiguous room.*Department \/ Room/i);
    expect(resolveEmRoom("Packaging / Airlock", DEPARTMENTS).room?.id).toBe(21);
    expect(resolveEmRoom("Sterile Filling/Airlock", DEPARTMENTS).room?.id).toBe(11);
    expect(resolveEmRoom("Missing Room", DEPARTMENTS).error).toMatch(/unknown room/i);
  });

  it("resolves test types by value or label and test codes by code or display name", () => {
    expect(resolveEmTestType("PassiveAirSample").testType?.value).toBe("PassiveAirSample");
    expect(resolveEmTestType("active air sample").testType?.value).toBe("ActiveAirSample");
    expect(resolveEmTestType("Swab").error).toMatch(/unknown test type/i);

    expect(resolveEmTestCode("tamc-em", TEST_OPTIONS).option?.code).toBe("TAMC-EM");
    expect(resolveEmTestCode("Total Yeast & Mold", TEST_OPTIONS).option?.code).toBe("TYMC-EM");
    expect(resolveEmTestCode("Duplicate Name", TEST_OPTIONS).error).toMatch(/ambiguous test/i);
    expect(resolveEmTestCode("NoTest", TEST_OPTIONS).error).toMatch(/unknown test/i);
  });
});

describe("buildEmRoomTestConfigPastePlan", () => {
  it("flags active existing configs and duplicates, ignores inactive configs, and keeps limits unchanged", () => {
    const configsByRoom: Record<number, RoomTestConfig[]> = {
      10: [
        {
          id: 100,
          roomId: 10,
          testType: "PassiveAirSample",
          testCode: "TAMC-EM",
          alertLimit: "5",
          actionLimit: "10",
          specLimit: "20",
          unit: "",
          isActive: true
        },
        {
          id: 101,
          roomId: 10,
          testType: "ActiveAirSample",
          testCode: "TAMC-EM",
          alertLimit: "1",
          actionLimit: "3",
          specLimit: "5",
          unit: "CFU/m³",
          isActive: false
        }
      ]
    };

    const plan = buildEmRoomTestConfigPastePlan({
      departments: DEPARTMENTS,
      configsByRoom,
      testOptions: TEST_OPTIONS
    });

    expect(plan.maxRows).toBe(MAX_EM_CONFIG_BATCH_ROWS);
    expect(plan.columns.map((c) => c.label)).toEqual([
      "Room",
      "Test type",
      "Test",
      "Alert limit",
      "Action limit",
      "Spec limit",
      "Unit"
    ]);

    const matched = plan.match?.([
      ["Filling Room 1", "Passive air sample", "TAMC-EM", "5", "10", "20", ""],
      ["Filling Room 1", "Active air sample", "TAMC-EM", "< 1", " 5.0 ", ">10", "CFU/m³"],
      ["Pack Line 1", "Surface sample", "TYMC-EM", "2", "5", "10", ""],
      ["Pack Line 1", "SurfaceAirSample", "Total Yeast & Mold", "2", "5", "10", ""]
    ]);

    expect(matched?.rows[0].problem).toMatch(/active configuration.*already exists/i);
    // Inactive existing config must NOT block a new active config
    expect(matched?.rows[1].problem).toBeUndefined();
    // Limits preserved verbatim without trimming/reformatting
    expect(matched?.rows[1].values.alertLimit).toBe("< 1");
    expect(matched?.rows[1].values.actionLimit).toBe(" 5.0 ");
    expect(matched?.rows[1].values.specLimit).toBe(">10");

    expect(matched?.rows[2].problem).toMatch(/duplicate/i);
    expect(matched?.rows[3].problem).toMatch(/duplicate/i);

    const pending = toPendingEmTestConfigs(
      [
        {
          target: null,
          values: {
            room: "Packaging / Airlock",
            testType: "Active air sample",
            testCode: "TAMC-EM",
            alertLimit: " 5 ",
            actionLimit: "<10",
            specLimit: "NMT 20",
            unit: "CFU/m³"
          }
        }
      ],
      DEPARTMENTS,
      TEST_OPTIONS
    );

    expect(toCreateRoomTestConfigPayload(pending[0])).toEqual({
      roomId: 21,
      testType: "ActiveAirSample",
      testCode: "TAMC-EM",
      alertLimit: " 5 ",
      actionLimit: "<10",
      specLimit: "NMT 20",
      unit: "CFU/m³"
    });
  });
});
