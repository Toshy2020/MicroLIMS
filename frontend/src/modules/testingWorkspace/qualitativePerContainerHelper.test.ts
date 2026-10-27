import { describe, expect, it } from "vitest";
import {
  getConclusionLine,
  isValidContainerCount,
  buildContainerRows,
  buildQualitativePayload
} from "./qualitativePerContainerHelper";

describe("getConclusionLine", () => {
  it("returns 'The container complies' when n = 1 and container complies", () => {
    expect(getConclusionLine(1, [{ index: 1, conforms: true }])).toBe("The container complies");
  });

  it("returns 'The container does not comply' when n = 1 and container fails", () => {
    expect(getConclusionLine(1, [{ index: 1, conforms: false }])).toBe("The container does not comply");
  });

  it("returns 'The container complies' when n = 1 and containers array is empty or null", () => {
    expect(getConclusionLine(1, [])).toBe("The container complies");
    expect(getConclusionLine(1, null)).toBe("The container complies");
  });

  it("returns 'All {n} containers comply' when n > 1 and all containers comply", () => {
    expect(
      getConclusionLine(3, [
        { index: 1, conforms: true },
        { index: 2, conforms: true },
        { index: 3, conforms: true }
      ])
    ).toBe("All 3 containers comply");
  });

  it("returns 'All {n} containers comply' when n > 1 and containers array is empty or null", () => {
    expect(getConclusionLine(5, [])).toBe("All 5 containers comply");
    expect(getConclusionLine(2, null)).toBe("All 2 containers comply");
  });

  it("returns 'Container {i} of {n} does not comply' when exactly one container fails", () => {
    expect(
      getConclusionLine(4, [
        { index: 1, conforms: true },
        { index: 2, conforms: false },
        { index: 3, conforms: true },
        { index: 4, conforms: true }
      ])
    ).toBe("Container 2 of 4 does not comply");
  });

  it("returns 'Containers {i, j} of {n} do not comply' with ascending indexes when multiple containers fail and string is <= 100 chars", () => {
    expect(
      getConclusionLine(4, [
        { index: 1, conforms: true },
        { index: 2, conforms: false },
        { index: 3, conforms: true },
        { index: 4, conforms: false }
      ])
    ).toBe("Containers 2, 4 of 4 do not comply");
  });

  it("sorts failing indexes in ascending order even if input is unordered", () => {
    expect(
      getConclusionLine(6, [
        { index: 5, conforms: false },
        { index: 1, conforms: false },
        { index: 3, conforms: false },
        { index: 2, conforms: true },
        { index: 4, conforms: true },
        { index: 6, conforms: true }
      ])
    ).toBe("Containers 1, 3, 5 of 6 do not comply");
  });

  it("falls back to '{k} of {n} containers do not comply' when the detailed string exceeds 100 characters", () => {
    // 30 failing containers out of 50
    const containers = Array.from({ length: 50 }, (_, i) => ({
      index: i + 1,
      conforms: i >= 30 // indexes 1..30 fail
    }));
    // Detailed would be: "Containers 1, 2, 3, ... 30 of 50 do not comply" (> 100 chars)
    expect(getConclusionLine(50, containers)).toBe("30 of 50 containers do not comply");
  });
});

describe("isValidContainerCount", () => {
  it("accepts integers from 1 to 200", () => {
    expect(isValidContainerCount(1)).toBe(true);
    expect(isValidContainerCount(50)).toBe(true);
    expect(isValidContainerCount(200)).toBe(true);
  });

  it("rejects numbers outside 1..200 and non-integers", () => {
    expect(isValidContainerCount(0)).toBe(false);
    expect(isValidContainerCount(-1)).toBe(false);
    expect(isValidContainerCount(201)).toBe(false);
    expect(isValidContainerCount(2.5)).toBe(false);
    expect(isValidContainerCount(NaN)).toBe(false);
    expect(isValidContainerCount(null)).toBe(false);
    expect(isValidContainerCount(undefined)).toBe(false);
  });
});

describe("buildContainerRows", () => {
  it("builds new rows 1..n defaulting to conforms: true and empty observation", () => {
    const rows = buildContainerRows([], 3);
    expect(rows).toEqual([
      { index: 1, conforms: true, observation: "" },
      { index: 2, conforms: true, observation: "" },
      { index: 3, conforms: true, observation: "" }
    ]);
  });

  it("preserves conforms and observation of existing container indexes when resizing", () => {
    const existing = [
      { index: 1, conforms: true, observation: "Pass" },
      { index: 2, conforms: false, observation: "Damaged seal" }
    ];
    const expanded = buildContainerRows(existing, 3);
    expect(expanded).toEqual([
      { index: 1, conforms: true, observation: "" },
      { index: 2, conforms: false, observation: "Damaged seal" },
      { index: 3, conforms: true, observation: "" }
    ]);

    const shrank = buildContainerRows(existing, 1);
    expect(shrank).toEqual([
      { index: 1, conforms: true, observation: "" }
    ]);
  });

  it("restores removed rows from containerMemory when growing after shrinking", () => {
    const memory = new Map<number, { conforms: boolean; observation: string }>([
      [1, { conforms: true, observation: "" }],
      [2, { conforms: false, observation: "Punctured foil" }],
      [3, { conforms: false, observation: "Wrong color" }]
    ]);

    // Growing from 1 container back to 3 containers using memory
    const restored = buildContainerRows([{ index: 1, conforms: true, observation: "" }], 3, memory);
    expect(restored).toEqual([
      { index: 1, conforms: true, observation: "" },
      { index: 2, conforms: false, observation: "Punctured foil" },
      { index: 3, conforms: false, observation: "Wrong color" }
    ]);
  });

  it("returns empty array when count is less than 1", () => {
    expect(buildContainerRows([], 0)).toEqual([]);
    expect(buildContainerRows([], -2)).toEqual([]);
  });
});

describe("buildQualitativePayload", () => {
  it("builds payload for non-per-container qualitative tests without containerCount and without parameter containers", () => {
    const payload = buildQualitativePayload({
      analysedAt: "2026-10-26T12:00:00",
      equipmentId: 12,
      password: "secretPassword",
      comment: "All parameters checked",
      isPerContainer: false,
      parameters: [
        {
          specificationId: 101,
          conforms: true,
          observation: "Off-white crystalline powder"
        },
        {
          specificationId: 102,
          conforms: false,
          observation: "Slight chemical odor"
        }
      ]
    });

    expect(payload.containerCount).toBeUndefined();
    expect(payload.equipmentId).toBe(12);
    expect(payload.password).toBe("secretPassword");
    expect(payload.comment).toBe("All parameters checked");
    expect(payload.parameters).toEqual([
      {
        specificationId: 101,
        conforms: true,
        observation: "Off-white crystalline powder"
      },
      {
        specificationId: 102,
        conforms: false,
        observation: "Slight chemical odor"
      }
    ]);
    expect((payload.parameters[0] as { containers?: unknown }).containers).toBeUndefined();
    expect((payload.parameters[1] as { containers?: unknown }).containers).toBeUndefined();
  });

  it("always sends observation (string or null) for each container, clearing observation for complying containers", () => {
    const payload = buildQualitativePayload({
      analysedAt: "2026-10-26T12:00:00",
      equipmentId: "",
      password: "secretPassword",
      comment: null,
      isPerContainer: true,
      containerCount: 3,
      parameters: [
        {
          specificationId: 201,
          conforms: false,
          observation: "",
          containers: [
            { index: 1, conforms: true, observation: "Leftover note" },
            { index: 2, conforms: false, observation: "Foreign particles visible" },
            { index: 3, conforms: false, observation: "   " }
          ]
        }
      ]
    });

    expect(payload.containerCount).toBe(3);
    expect(payload.equipmentId).toBeNull();
    expect(payload.password).toBe("secretPassword");
    expect(payload.comment).toBeNull();
    expect(payload.parameters).toHaveLength(1);
    expect(payload.parameters[0].specificationId).toBe(201);
    expect(payload.parameters[0].conforms).toBe(false);
    expect(payload.parameters[0].containers).toEqual([
      { index: 1, conforms: true, observation: null },
      { index: 2, conforms: false, observation: "Foreign particles visible" },
      { index: 3, conforms: false, observation: null }
    ]);
  });
});
