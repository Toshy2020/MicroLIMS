import { describe, expect, it } from "vitest";
import pkg from "../../../../package.json";
import { CHAPTERS } from "../content/guideLoader";
import {
  compareVersions,
  isNewerVersion,
  itemsForRole,
  RELEASE_NOTES,
  type ReleaseNote
} from "./releaseNotes";

describe("RELEASE_NOTES", () => {
  it("newest note version equals package.json version and __APP_VERSION__", () => {
    expect(RELEASE_NOTES.length).toBeGreaterThan(0);
    expect(RELEASE_NOTES[0].version).toBe(pkg.version);
    expect(RELEASE_NOTES[0].version).toBe(__APP_VERSION__);
  });

  it("links only valid guide chapters and contains no hardcoded limits or time windows", () => {
    const validChapters = new Set(CHAPTERS.map((c) => c.id));
    for (const note of RELEASE_NOTES) {
      expect(note.date).toMatch(/^\d{4}-\d{2}-\d{2}$/);
      expect(note.items.length).toBeGreaterThan(0);
      for (const item of note.items) {
        expect(item.text.trim().length).toBeGreaterThan(0);
        expect(item.text).not.toMatch(/\d+\s*(cfu|%|°C|hours?|days?)/i);
        if (item.guide) {
          expect(validChapters.has(item.guide.chapterId)).toBe(true);
        }
      }
    }
  });
});

describe("version comparison helpers", () => {
  it("compares dotted versions numerically so 0.10.0 > 0.9.0", () => {
    expect(compareVersions("0.10.0", "0.9.0")).toBeGreaterThan(0);
    expect(compareVersions("0.9.0", "0.10.0")).toBeLessThan(0);
    expect(compareVersions("1.2.0", "1.2")).toBe(0);
    expect(compareVersions("0.1.0", "0.1.0")).toBe(0);
    expect(compareVersions("2.0.0", "1.99.99")).toBeGreaterThan(0);
  });

  it("treats equal versions as not greater and absent stored value as older", () => {
    expect(isNewerVersion("0.10.0", "0.9.0")).toBe(true);
    expect(isNewerVersion("0.9.0", "0.10.0")).toBe(false);
    expect(isNewerVersion("0.1.0", "0.1.0")).toBe(false);
    expect(isNewerVersion("0.1.0", null)).toBe(true);
    expect(isNewerVersion("0.1.0", undefined)).toBe(true);
    expect(isNewerVersion("0.1.0", "")).toBe(true);
    expect(isNewerVersion("0.1.0", "   ")).toBe(true);
  });
});

describe("itemsForRole", () => {
  const sampleNote: ReleaseNote = {
    version: "0.2.0",
    date: "2026-11-15",
    items: [
      { roles: "all", text: "Shared change" },
      { roles: ["Analyst", "SectionHead"], text: "Bench entry change" },
      { roles: ["Qa"], text: "QA audit change" }
    ]
  };

  it("returns only items visible to the requested role", () => {
    expect(itemsForRole(sampleNote, "Analyst").map((i) => i.text)).toEqual([
      "Shared change",
      "Bench entry change"
    ]);
    expect(itemsForRole(sampleNote, "Qa").map((i) => i.text)).toEqual([
      "Shared change",
      "QA audit change"
    ]);
    expect(itemsForRole(sampleNote, "Reviewer").map((i) => i.text)).toEqual(["Shared change"]);
    expect(itemsForRole(sampleNote, null).map((i) => i.text)).toEqual(["Shared change"]);
  });
});
