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
