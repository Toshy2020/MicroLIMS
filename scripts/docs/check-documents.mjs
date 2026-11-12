#!/usr/bin/env node
// Validates the specification set under documents/: IDs, cross-references,
// document headers and code locations. It does not judge content.
// Node built-ins only.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const AREAS = new Set(
  "RCV MIC PAT WAT ENV ACL PHY INS MED SOL CRY WST INV EQP OOS MON REV SIG AUD RPT MDM NTF ADM SEC".split(" "),
);
const SKIPPED_DIRS = new Set(["DocumentControl", "_templates"]);
const ID_PATTERNS = {
  URS: /^URS-([A-Z]{3})-\d{3}$/,
  FS: /^FS-([A-Z]{3})-\d{3}$/,
  CALC: /^CALC-\d{3}$/,
  CONF: /^CONF-\d{3}$/,
};
const HEADER_FIELDS = [
  "Document number", "Title", "Version", "Status", "Author", "Reviewer", "Approver", "Effective date",
];

function listMarkdown(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
    if (entry.isDirectory()) {
      if (!SKIPPED_DIRS.has(entry.name)) listMarkdown(path.join(dir, entry.name), out);
    } else if (entry.isFile() && entry.name.toLowerCase().endsWith(".md")) {
      out.push(path.join(dir, entry.name));
    }
  }
  return out;
}

const idRegex = /\b(?:URS|FS|CALC|CONF)-[A-Z0-9]+(?:-[A-Z0-9]+)*\b/g;

export function checkDocuments(rootDir, repoRoot) {
  const errors = [];
  const display = (file) => {
    const rel = path.relative(repoRoot, file);
    return (rel.startsWith("..") ? path.relative(rootDir, file) : rel).split(path.sep).join("/");
  };
  const err = (file, line, msg) => errors.push(`${display(file)}:${line} ${msg}`);

  if (!fs.existsSync(rootDir) || !fs.statSync(rootDir).isDirectory()) {
    errors.push(`${rootDir}:1 documents folder not found`);
    return { errors };
  }

  const defs = new Map(); // id -> [{file,line}]
  const entries = []; // URS/FS entries
  const conflictBodies = []; // text of each CONF entry in Conflicts.md
  const files = listMarkdown(rootDir);

  const define = (id, file, line) => {
    if (!defs.has(id)) defs.set(id, []);
    defs.get(id).push({ file, line });
  };

  for (const file of files) {
    const lines = fs.readFileSync(file, "utf8").split(/\r?\n/);
    const base = path.basename(file);
    const nameMatch = /^(URS|FS)-([A-Z]{3})\.md$/.exec(base);
    const isConflicts = base === "Conflicts.md";
    let inFence = false;
    let current = null; // current URS/FS entry
    let currentConf = null;
    let definesUrsFs = false;
    let firstEntryLine = Infinity;

    const closeEntry = () => { current = null; };

    lines.forEach((text, idx) => {
      const n = idx + 1;
      if (/^\s*(```|~~~)/.test(text)) { inFence = !inFence; return; }
      if (inFence) return;

      const heading = /^#{1,6}\s+((?:URS|FS|CALC|CONF)-\S+)\s*$/.exec(text);
      const row = /^\|\s*((?:CALC|CONF)-[A-Za-z0-9-]+)\s*\|/.exec(text);
      const defined = heading ? heading[1] : row ? row[1] : null;

      if (/^#{1,6}\s/.test(text) && !heading) { closeEntry(); currentConf = null; }

      if (defined) {
        const kind = defined.split("-")[0];
        const m = ID_PATTERNS[kind].exec(defined);
        if (!m) { err(file, n, `${defined} does not match the ID pattern ${ID_PATTERNS[kind].source}`); closeEntry(); currentConf = null; return; }
        if ((kind === "URS" || kind === "FS") && !AREAS.has(m[1])) {
          err(file, n, `${defined} uses unknown area code ${m[1]}`);
        }
        if (kind === "URS" || kind === "FS") {
          if (nameMatch && nameMatch[1] !== kind) err(file, n, `${defined} is a ${kind} entry in a ${nameMatch[1]} file`);
          else if (nameMatch && nameMatch[2] !== m[1]) err(file, n, `${defined} is in the wrong area file (${base})`);
          definesUrsFs = true;
          firstEntryLine = Math.min(firstEntryLine, n);
          current = { id: defined, kind, file, line: n, fields: {} };
          entries.push(current);
          currentConf = null;
        } else {
          closeEntry();
          if (kind === "CONF" && isConflicts && heading) { currentConf = { id: defined, text: "" }; conflictBodies.push(currentConf); }
        }
        define(defined, file, n);
        return;
      }

      if (currentConf) currentConf.text += text + "\n";
      if (current) {
        const f = /^\*\*([A-Za-z ]+):\*\*\s*(.*)$/.exec(text);
        if (f) {
          const key = f[1];
          if (current.fields[key] === undefined) current.fields[key] = { value: f[2].trim(), line: n };
        }
      }
    });
    // table-row CONF entries are matched above only when `defined` handled them;
    // make their text available for URS coverage.
    if (isConflicts) {
      lines.forEach((text) => {
        const r = /^\|\s*(CONF-\d{3})\s*\|/.exec(text);
        if (r) conflictBodies.push({ id: r[1], text });
      });
    }

    // Header table for URS/FS documents.
    if (nameMatch || definesUrsFs) {
      const headerLines = lines.slice(0, Number.isFinite(firstEntryLine) ? firstEntryLine - 1 : lines.length);
      const found = [];
      headerLines.forEach((t, i) => {
        const r = /^\|\s*([^|]+?)\s*\|\s*(.*?)\s*\|\s*$/.exec(t);
        if (r) found.push({ label: r[1], value: r[2], line: i + 1 });
      });
      let last = -1;
      for (const field of HEADER_FIELDS) {
        const idxF = found.findIndex((x) => x.label.toLowerCase() === field.toLowerCase());
        if (idxF < 0) { err(file, 1, `header field "${field}" is missing`); continue; }
        if (!found[idxF].value) err(file, found[idxF].line, `header field "${field}" is empty`);
        if (idxF < last) err(file, found[idxF].line, `header field "${field}" is out of order`);
        last = Math.max(last, idxF);
      }
      if (!headerLines.some((t) => /change history/i.test(t))) err(file, 1, 'header is missing the "Change history" table');
    }
  }

  // Unique IDs across all files.
  for (const [id, locs] of defs) {
    if (locs.length > 1) {
      for (const l of locs) {
        const others = locs.filter((o) => o !== l).map((o) => `${display(o.file)}:${o.line}`).join(", ");
        err(l.file, l.line, `${id} is defined more than once (duplicate; also at ${others})`);
      }
    }
  }

  const ursIds = new Map(entries.filter((e) => e.kind === "URS").map((e) => [e.id, e]));
  const conflictIds = new Set([...defs.keys()].filter((k) => k.startsWith("CONF-")));
  const satisfied = new Set();

  for (const e of entries) {
    const field = (k) => e.fields[k];
    if (e.kind === "URS") {
      const st = field("Status");
      if (!st) err(e.file, e.line, `${e.id} has no **Status:** field`);
      else if (!/^(Active|Withdrawn)\b/.test(st.value)) err(e.file, st.line, `${e.id} Status must be Active or Withdrawn, found "${st.value}"`);
      continue;
    }
    // FS entry
    const sat = field("Satisfies");
    if (!sat || !sat.value) err(e.file, sat ? sat.line : e.line, `${e.id} has no **Satisfies:** URS IDs`);
    else {
      for (const id of sat.value.split(/[,;\s]+/).filter(Boolean)) {
        if (!ID_PATTERNS.URS.test(id)) err(e.file, sat.line, `${e.id} Satisfies "${id}" which is not a URS ID`);
        else if (!ursIds.has(id)) err(e.file, sat.line, `${e.id} Satisfies unknown ${id}`);
        else {
          if (/^Withdrawn\b/.test(ursIds.get(id).fields.Status?.value ?? "")) err(e.file, sat.line, `${e.id} Satisfies withdrawn ${id}`);
          satisfied.add(id);
        }
      }
    }
    const code = field("Code");
    if (!code) err(e.file, e.line, `${e.id} has no **Code:** location`);
    else {
      const m = /^`([^`]+)`/.exec(code.value);
      if (!m) err(e.file, code.line, `${e.id} Code must start with a backticked repo-relative path`);
      else {
        const target = path.resolve(repoRoot, m[1]);
        const rel = path.relative(repoRoot, target);
        if (rel.startsWith("..") || path.isAbsolute(m[1])) err(e.file, code.line, `${e.id} Code path ${m[1]} is outside the repository`);
        else if (!fs.existsSync(target)) err(e.file, code.line, `${e.id} Code path ${m[1]} does not exist`);
      }
    }
    const conf = field("Conflicts");
    if (conf && conf.value) {
      for (const id of conf.value.split(/[,;\s]+/).filter(Boolean)) {
        if (!conflictIds.has(id)) err(e.file, conf.line, `${e.id} Conflicts names unknown ${id}`);
      }
    }
  }

  for (const [id, e] of ursIds) {
    if (/^Withdrawn\b/.test(e.fields.Status?.value ?? "")) continue;
    const inConflict = conflictBodies.some((c) => c.text.match(idRegex)?.includes(id));
    if (!satisfied.has(id) && !inConflict) {
      err(e.file, e.line, `${id} is Active but no FS entry Satisfies it and no Conflicts.md entry names it`);
    }
  }

  return { errors };
}

// CLI: node check-documents.mjs <documents-dir>
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const rootArg = process.argv[2];
  if (!rootArg) {
    console.error("usage: node scripts/docs/check-documents.mjs <documents-dir>");
    process.exit(2);
  }
  const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
  const { errors } = checkDocuments(path.resolve(rootArg), repoRoot);
  for (const e of errors) console.log(e);
  if (errors.length > 0) {
    console.error(`${errors.length} error(s)`);
    process.exit(1);
  }
}
