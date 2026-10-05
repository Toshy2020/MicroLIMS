import { describe, expect, it } from "vitest";
import {
  defaultAreaFor,
  areaIncludes,
  toggleBoth,
  isBoth,
  areaOfItemCategory
} from "./testMasterArea";

describe("testMasterArea", () => {
  describe("defaultAreaFor", () => {
    it("returns FinishedProduct for fp", () => {
      expect(defaultAreaFor("fp")).toBe("FinishedProduct");
    });

    it("returns RawPackaging for rmpm", () => {
      expect(defaultAreaFor("rmpm")).toBe("RawPackaging");
    });
  });

  describe("areaIncludes", () => {
    it("matches FinishedProduct only for fp", () => {
      expect(areaIncludes("FinishedProduct", "fp")).toBe(true);
      expect(areaIncludes("FinishedProduct", "rmpm")).toBe(false);
    });

    it("matches RawPackaging only for rmpm", () => {
      expect(areaIncludes("RawPackaging", "rmpm")).toBe(true);
      expect(areaIncludes("RawPackaging", "fp")).toBe(false);
    });

    it("matches Both for both fp and rmpm", () => {
      expect(areaIncludes("Both", "fp")).toBe(true);
      expect(areaIncludes("Both", "rmpm")).toBe(true);
    });

    it("treats null or undefined as included in both fp and rmpm", () => {
      expect(areaIncludes(null, "fp")).toBe(true);
      expect(areaIncludes(null, "rmpm")).toBe(true);
      expect(areaIncludes(undefined, "fp")).toBe(true);
      expect(areaIncludes(undefined, "rmpm")).toBe(true);
    });
  });

  describe("toggleBoth", () => {
    it("returns Both when checked is true on fp", () => {
      expect(toggleBoth("FinishedProduct", "fp", true)).toBe("Both");
      expect(toggleBoth(null, "fp", true)).toBe("Both");
      expect(toggleBoth("Both", "fp", true)).toBe("Both");
    });

    it("returns Both when checked is true on rmpm", () => {
      expect(toggleBoth("RawPackaging", "rmpm", true)).toBe("Both");
      expect(toggleBoth(null, "rmpm", true)).toBe("Both");
      expect(toggleBoth("Both", "rmpm", true)).toBe("Both");
    });

    it("returns FinishedProduct when checked is false on fp", () => {
      expect(toggleBoth("Both", "fp", false)).toBe("FinishedProduct");
      expect(toggleBoth("FinishedProduct", "fp", false)).toBe("FinishedProduct");
      expect(toggleBoth(null, "fp", false)).toBe("FinishedProduct");
    });

    it("returns RawPackaging when checked is false on rmpm", () => {
      expect(toggleBoth("Both", "rmpm", false)).toBe("RawPackaging");
      expect(toggleBoth("RawPackaging", "rmpm", false)).toBe("RawPackaging");
      expect(toggleBoth(null, "rmpm", false)).toBe("RawPackaging");
    });
  });

  describe("isBoth", () => {
    it("identifies Both correctly", () => {
      expect(isBoth("Both")).toBe(true);
      expect(isBoth("FinishedProduct")).toBe(false);
      expect(isBoth("RawPackaging")).toBe(false);
      expect(isBoth(null)).toBe(false);
      expect(isBoth(undefined)).toBe(false);
    });
  });
});

describe("areaOfItemCategory", () => {
  it("matches the server: only raw and packaging materials are rmpm", () => {
    expect(areaOfItemCategory("RawMaterial")).toBe("rmpm");
    expect(areaOfItemCategory("PackagingMaterial")).toBe("rmpm");
    expect(areaOfItemCategory("FinishedProduct")).toBe("fp");
    expect(areaOfItemCategory("Water")).toBe("fp");
    expect(areaOfItemCategory(undefined)).toBe("fp");
  });
});
