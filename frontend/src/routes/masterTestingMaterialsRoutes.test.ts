import { describe, expect, it } from "vitest";
import { resolveTraceabilityRoute } from "./routes";

describe("traceability routes into Master Testing Materials", () => {
  it.each([
    ["material-master", undefined, "/laboratory-configuration/master-testing-materials?tab=reagents"],
    ["media-configurations", undefined, "/laboratory-configuration/master-testing-materials?tab=media"],
    ["reference-organisms", undefined, "/laboratory-configuration/master-testing-materials?tab=organisms"],
    [undefined, "materialmasterentry", "/laboratory-configuration/master-testing-materials?tab=reagents"],
    [undefined, "referenceorganism", "/laboratory-configuration/master-testing-materials?tab=organisms"]
  ])("resolves %s / %s", (target, type, expected) => {
    expect(resolveTraceabilityRoute(target, type)).toBe(expected);
  });
});
