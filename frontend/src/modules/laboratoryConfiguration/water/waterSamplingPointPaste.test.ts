import { describe, expect, it } from "vitest";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import type { SamplingPoint, WaterDept } from "./waterConfigTypes";
import {
  MAX_WATER_BATCH_ROWS,
  buildWaterSamplingPointPastePlan,
  parseFrequencyInput,
  resolveTestCodes,
  resolveWaterDepartment,
  toCreateWaterSamplingPointPayload,
  toPendingWaterPoints
} from "./waterSamplingPointPaste";

const DEPARTMENTS: WaterDept[] = [
  { id: 1, name: "Purified Water System", samplingPoints: [] },
  { id: 2, name: "WFI Loop", samplingPoints: [] }
];

const TEST_OPTIONS: TestDefinitionOption[] = [
  {
    id: 1,
    code: "TAMC-Water",
    displayName: "TAMC for Water",
    isActive: true,
    workflowType: "CountTest"
  },
  {
    id: 2,
    code: "PH",
    displayName: "pH",
    isActive: true,
    workflowType: "Measurement"
  },
  {
    id: 3,
    code: "COND-A",
    displayName: "Conductivity",
    isActive: true,
    workflowType: "Measurement"
  },
  {
    id: 4,
    code: "COND-B",
    displayName: "Conductivity",
    isActive: true,
    workflowType: "Measurement"
  }
];

describe("parseFrequencyInput", () => {
  it("parses empty, daily, weekly, and monthly formats", () => {
    expect(parseFrequencyInput("").error).toBeNull();
    expect(parseFrequencyInput("Daily")).toEqual({
      value: { type: "Daily", interval: 1, weekday: "", anchorMonth: "", weekOfMonth: "" },
      error: null
    });
    expect(parseFrequencyInput("Daily, working days").value.type).toBe("Daily");
    expect(parseFrequencyInput("Weekly on Monday")).toEqual({
      value: { type: "Weekly", interval: 1, weekday: "Monday", anchorMonth: "", weekOfMonth: "" },
      error: null
    });
    expect(parseFrequencyInput("Every 2 weeks, friday")).toEqual({
      value: { type: "Weekly", interval: 2, weekday: "Friday", anchorMonth: "", weekOfMonth: "" },
      error: null
    });
    expect(parseFrequencyInput("Monthly, week 2 on Tuesday")).toEqual({
      value: { type: "Monthly", interval: 1, weekday: "Tuesday", anchorMonth: "", weekOfMonth: 2 },
      error: null
    });
    expect(parseFrequencyInput("Every 3 months from March")).toEqual({
      value: { type: "Monthly", interval: 3, weekday: "", anchorMonth: 3, weekOfMonth: "" },
      error: null
    });
  });

  it("rejects weekly without weekday and invalid values", () => {
    expect(parseFrequencyInput("Weekly").error).toMatch(/weekday/i);
    expect(parseFrequencyInput("Monthly, week 5").error).toMatch(/between 1 and 4/i);
    expect(parseFrequencyInput("Hourly").error).toBeTruthy();
  });
});

describe("resolveWaterDepartment & resolveTestCodes", () => {
  it("resolves departments case-insensitively and flags unknown or ambiguous names", () => {
    expect(resolveWaterDepartment("purified water system", DEPARTMENTS).dept?.id).toBe(1);
    expect(resolveWaterDepartment("", DEPARTMENTS).error).toMatch(/required/i);
    expect(resolveWaterDepartment("Missing", DEPARTMENTS).error).toMatch(/unknown water system/i);

    const dupDepts: WaterDept[] = [
      { id: 1, name: "Loop", samplingPoints: [] },
      { id: 2, name: "loop", samplingPoints: [] }
    ];
    expect(resolveWaterDepartment("Loop", dupDepts).error).toMatch(/ambiguous/i);
  });

  it("resolves tests by code or display name and flags unknown or ambiguous names", () => {
    expect(resolveTestCodes("tamc-water, pH", TEST_OPTIONS)).toEqual({
      codes: ["TAMC-Water", "PH"],
      error: null
    });
    expect(resolveTestCodes("TAMC for Water; COND-A", TEST_OPTIONS)).toEqual({
      codes: ["TAMC-Water", "COND-A"],
      error: null
    });
    expect(resolveTestCodes("Conductivity", TEST_OPTIONS).error).toMatch(/ambiguous/i);
    expect(resolveTestCodes("NoSuchTest", TEST_OPTIONS).error).toMatch(/unknown test/i);
  });
});

describe("buildWaterSamplingPointPastePlan", () => {
  it("flags existing codes, duplicate codes in paste, and converts accepted rows to payloads", () => {
    const existingPoints: SamplingPoint[] = [
      {
        id: 10,
        code: "PW-01",
        location: "Tap 1",
        testingFrequency: "Daily",
        assignedTestCodes: ["TAMC-Water"],
        waterDepartmentId: 1
      }
    ];
    const plan = buildWaterSamplingPointPastePlan({
      departments: DEPARTMENTS,
      existingPoints,
      testOptions: TEST_OPTIONS
    });

    expect(plan.maxRows).toBe(MAX_WATER_BATCH_ROWS);
    expect(plan.columns.map((c) => c.label)).toEqual([
      "Point code",
      "Water system",
      "Point name",
      "Frequency",
      "Assigned tests"
    ]);
    expect(plan.columns[0].check("PW-01")).toMatch(/already exists/i);

    const matched = plan.match?.([
      ["PW-10", "Purified Water System", "Loop A", "Daily", "TAMC-Water"],
      ["pw-10", "WFI Loop", "Loop B", "Daily", "PH"]
    ]);
    expect(matched?.rows[0].problem).toMatch(/duplicate/i);
    expect(matched?.rows[1].problem).toMatch(/duplicate/i);

    const pending = toPendingWaterPoints(
      [
        {
          target: null,
          values: {
            code: "PW-12",
            waterDepartment: "WFI Loop",
            location: "User Point",
            frequency: "Monthly, week 2",
            assignedTestCodes: "TAMC-Water, PH"
          }
        }
      ],
      DEPARTMENTS,
      TEST_OPTIONS
    );

    expect(toCreateWaterSamplingPointPayload(pending[0])).toEqual({
      code: "PW-12",
      location: "User Point",
      testingFrequency: "Monthly, week 2",
      assignedTestCodes: ["TAMC-Water", "PH"],
      waterDepartmentId: 2,
      frequency: {
        type: "Monthly",
        interval: 1,
        samplingWeekday: null,
        anchorMonth: null,
        weekOfMonth: 2
      }
    });
  });
});
