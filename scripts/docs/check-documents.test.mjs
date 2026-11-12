import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { checkDocuments } from "./check-documents.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, "..", "..");
const fixture = (name) => path.join(here, "fixtures", name);
const run = (name) => checkDocuments(fixture(name), repoRoot).errors;

// Passes only when one error mentions every given fragment (ID and file).
function assertError(errors, ...fragments) {
  const hit = errors.find((e) => fragments.every((f) => e.includes(f)));
  assert.ok(hit, `no error contains ${JSON.stringify(fragments)}; got:\n${errors.join("\n")}`);
  assert.match(hit, /^[^\s:]+:\d+ \S/, "error must be '<file>:<line> <message>'");
}

test("valid_set_passes", () => {
  assert.deepEqual(run("valid"), []);
});

test("duplicate_id_across_files_fails", () => {
  const errors = run("duplicate");
  assertError(errors, "URS-MIC-001", "duplicate/URS/copy/URS-MIC.md", "duplicate");
  assertError(errors, "URS-MIC-001", "duplicate/URS/URS-MIC.md");
});

test("urs_without_fs_or_conflict_fails", () => {
  const errors = run("orphan-urs");
  assertError(errors, "URS-MIC-002", "orphan-urs/URS/URS-MIC.md");
  assert.equal(errors.filter((e) => e.includes("URS-MIC-001")).length, 0);
});

test("fs_naming_unknown_urs_fails", () => {
  assertError(run("bad-ref"), "URS-MIC-099", "bad-ref/FunctionalSpecification/FS-MIC.md");
});

test("missing_header_field_fails", () => {
  const errors = run("bad-header");
  assertError(errors, "Approver", "bad-header/URS/URS-MIC.md");
  assert.equal(errors.filter((e) => e.includes("bad-header/FunctionalSpecification")).length, 0);
});

test("fs_code_location_must_exist", () => {
  assertError(
    run("bad-code-path"),
    "FS-MIC-001",
    "bad-code-path/FunctionalSpecification/FS-MIC.md",
    "DoesNotExist.cs",
  );
});

test("withdrawn_urs_needs_no_fs", () => {
  assert.deepEqual(run("withdrawn"), []);
});

test("empty_documents_folder_passes", () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "emptydocs-"));
  try {
    assert.deepEqual(checkDocuments(dir, repoRoot).errors, []);
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

test("malformed_id_fails", () => {
  assertError(run("bad-id"), "URS-MIC-1", "bad-id/URS/URS-MIC.md", "pattern");
});

test("missing_root_folder_fails", () => {
  const errors = checkDocuments(path.join(here, "fixtures", "nope"), repoRoot).errors;
  assertError(errors, "nope");
});

// Document Control IDs used in code must still resolve after the move.
// An ID resolves when a markdown file under documents/DocumentControl/ contains it,
// or when documents/DocumentControl/README.md lists it under "IDs defined in Word documents".
test("existing_dc_ids_still_resolve", () => {
  const idPattern = /(?<![\w-])(?:DC-URS-\d+|FS-1[a-d]-\d+)(?![\w-])/g;
  const skipDirs = new Set(["bin", "obj", "node_modules"]);

  const codeIds = new Map(); // id -> first file that uses it
  const walk = (dir) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      if (entry.isDirectory()) {
        if (!skipDirs.has(entry.name)) walk(path.join(dir, entry.name));
      } else if (entry.isFile()) {
        const file = path.join(dir, entry.name);
        for (const id of fs.readFileSync(file, "utf8").match(idPattern) ?? []) {
          if (!codeIds.has(id)) codeIds.set(id, path.relative(repoRoot, file));
        }
      }
    }
  };
  walk(path.join(repoRoot, "backend"));
  walk(path.join(repoRoot, "frontend", "src"));

  const dcDir = path.join(repoRoot, "documents", "DocumentControl");
  const mdIds = new Set();
  const readMarkdown = (dir) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) readMarkdown(full);
      else if (entry.isFile() && entry.name.toLowerCase().endsWith(".md")) {
        for (const id of fs.readFileSync(full, "utf8").match(idPattern) ?? []) mdIds.add(id);
      }
    }
  };
  if (fs.existsSync(dcDir)) readMarkdown(dcDir);

  const wordIds = new Set();
  const readmePath = path.join(dcDir, "README.md");
  if (fs.existsSync(readmePath)) {
    let inSection = false;
    for (const line of fs.readFileSync(readmePath, "utf8").split(/\r?\n/)) {
      if (/^#{1,6}\s/.test(line)) {
        inSection = /^#{1,6}\s+IDs defined in Word documents\s*$/.test(line);
        continue;
      }
      if (inSection) for (const id of line.match(idPattern) ?? []) wordIds.add(id);
    }
  }

  const unresolved = [...codeIds.keys()]
    .filter((id) => !mdIds.has(id) && !wordIds.has(id))
    .map((id) => `${id} (used in ${codeIds.get(id)})`);
  assert.deepEqual(unresolved, [], `Document Control IDs with no home under documents/DocumentControl/:\n${unresolved.join("\n")}`);
});
