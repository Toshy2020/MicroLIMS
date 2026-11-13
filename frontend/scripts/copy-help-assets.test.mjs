import { mkdirSync, mkdtempSync, readdirSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { describe, expect, it } from "vitest";

const script = path.resolve(import.meta.dirname, "copy-help-assets.mjs");

function fixture(chapterText, shots) {
  const root = mkdtempSync(path.join(tmpdir(), "help-assets-"));
  mkdirSync(path.join(root, "guide"));
  mkdirSync(path.join(root, "shots"));
  writeFileSync(path.join(root, "guide", "01-intro.md"), chapterText);
  for (const s of shots) writeFileSync(path.join(root, "shots", s), "png");
  return root;
}

function run(root) {
  return spawnSync(process.execPath, [script, path.join(root, "guide"), path.join(root, "shots"), path.join(root, "out")], { encoding: "utf8" });
}

describe("copy-help-assets", () => {
  it("copies every referenced screenshot", () => {
    const root = fixture("## 1. Intro\n![Alt](../screenshots/a.png)\n", ["a.png", "b.png"]);
    const result = run(root);
    expect(result.status).toBe(0);
    expect(readdirSync(path.join(root, "out")).sort()).toEqual(["a.png", "b.png"]);
  });

  it("fails and names the chapter and file when a chapter references a missing screenshot", () => {
    const root = fixture("## 1. Intro\n![Alt](../screenshots/gone.png)\n", ["a.png"]);
    const result = run(root);
    expect(result.status).not.toBe(0);
    expect(result.stderr).toContain("01-intro.md");
    expect(result.stderr).toContain("gone.png");
  });
});
