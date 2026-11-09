// Frontend test & typing debt ratchet (CLAUDE.md Rules 4.5, 4.6, 6.1).
//
// The suite predates those rules, so legacy violations are not rewritten in
// one go. Instead their counts are pinned in test-debt-baseline.json and CI
// fails if any count RISES; files are fixed as they are touched, and the
// baseline is lowered with `npm run debt:update` (it can never be raised).
//
//   fireEventCalls         fireEvent.x( calls in tests              (Rule 4.6)
//   projectModuleMocks     vi.mock() of project code - components,  (Rule 4.5)
//                          hooks, contexts - rather than an HTTP/service
//                          boundary or a third-party package
//   anyInSource            `any` in non-test .ts/.tsx                (Rule 6.1)
//   anyInTests             `any` in test files                       (Rule 6.1)
import { readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

const FIRE_EVENT = /\bfireEvent\.[A-Za-z]+\(/g;
const VI_MOCK = /\bvi\.mock\(\s*["'`]([^"'`]+)["'`]/g;
const ANY = /(:\s*any\b|\bas any\b|<any>|\bany\[\])/g;

// A vi.mock target that is an allowed boundary: a package (no relative path),
// anything in a services/ folder (the HTTP layer), the HTTP client, or a
// module whose name says it is a service/API client.
export function isBoundaryMock(target) {
  if (!target.startsWith(".") && !target.startsWith("/")) return true;
  if (/(^|\/)services\//.test(target)) return true;
  const name = target.split("/").pop() ?? target;
  return /^(apiClient|errorReporter)$/.test(name) || /(Service|service|Api|api|Client)$/.test(name);
}

export function isTestFile(path) {
  return /\.test\.(ts|tsx)$/.test(path);
}

function count(re, text) {
  return (text.match(re) ?? []).length;
}

// Counts for one file's text. `path` decides whether it is a test.
export function measureFile(path, text) {
  const test = isTestFile(path);
  const mocks = test ? [...text.matchAll(VI_MOCK)].map((m) => m[1]) : [];
  return {
    fireEventCalls: test ? count(FIRE_EVENT, text) : 0,
    projectModuleMocks: mocks.filter((m) => !isBoundaryMock(m)).length,
    anyInSource: test ? 0 : count(ANY, text),
    anyInTests: test ? count(ANY, text) : 0
  };
}

export const METRICS = ["fireEventCalls", "projectModuleMocks", "anyInSource", "anyInTests"];

function* sourceFiles(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) yield* sourceFiles(full);
    else if (/\.(ts|tsx)$/.test(entry.name) && !entry.name.endsWith(".d.ts")) yield full;
  }
}

export function measureTree(srcDir) {
  const totals = Object.fromEntries(METRICS.map((m) => [m, 0]));
  const perFile = [];
  for (const file of sourceFiles(srcDir)) {
    const counts = measureFile(file, readFileSync(file, "utf8"));
    for (const m of METRICS) totals[m] += counts[m];
    perFile.push({ file: relative(srcDir, file).split(sep).join("/"), ...counts });
  }
  return { totals, perFile };
}

// Each metric must not exceed its baseline. Returns the regressions.
export function compare(totals, baseline) {
  return METRICS.filter((m) => totals[m] > (baseline[m] ?? 0))
    .map((m) => ({ metric: m, baseline: baseline[m] ?? 0, current: totals[m] }));
}

function main() {
  const here = fileURLToPath(new URL(".", import.meta.url));
  const root = join(here, "..");
  const baselinePath = join(root, "test-debt-baseline.json");
  const baseline = JSON.parse(readFileSync(baselinePath, "utf8"));
  const { totals, perFile } = measureTree(join(root, "src"));
  const update = process.argv.includes("--update");

  for (const m of METRICS) console.log(`${m.padEnd(20)} ${String(totals[m]).padStart(5)}  (baseline ${baseline[m]})`);

  const regressions = compare(totals, baseline);
  if (regressions.length > 0) {
    console.error("\nTest/typing debt went UP (CLAUDE.md Rules 4.5, 4.6, 6.1):");
    for (const r of regressions) {
      console.error(`  ${r.metric}: ${r.baseline} -> ${r.current}`);
      const worst = perFile.filter((f) => f[r.metric] > 0).sort((a, b) => b[r.metric] - a[r.metric]).slice(0, 10);
      for (const f of worst) console.error(`      ${f[r.metric]}  ${f.file}`);
    }
    console.error("\nUse userEvent instead of fireEvent, mock only service/HTTP boundaries, and type instead of `any`.");
    process.exit(1);
  }

  if (update) {
    const lowered = Object.fromEntries(METRICS.map((m) => [m, Math.min(totals[m], baseline[m])]));
    writeFileSync(baselinePath, JSON.stringify(lowered, null, 2) + "\n");
    console.log("\nBaseline lowered to the current counts.");
  } else if (METRICS.some((m) => totals[m] < baseline[m])) {
    console.log("\nDebt went down - run `npm run debt:update` to lock in the lower baseline.");
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) main();
