// Hard-coded interface text ratchet (F4, Arabic and RTL).
//
// Every user-visible English string written directly in a screen file must
// move into src/locales/en/<namespace>.json. That is done module by module, so
// the count per module is pinned in i18n-debt-baseline.json and CI fails if any
// module's count RISES (or a new module appears with text in it). Lower the
// baseline with `npm run i18n:update` after converting a module; it can never
// be raised. What counts is defined by eslint.i18n.config.js (the
// i18next/no-literal-string rule), which is kept apart from eslint.config.js so
// that `npm run lint` does not fail on the existing text.
import { ESLint } from "eslint";
import { readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

// npm runs scripts (and vitest runs tests) from the frontend folder.
const ROOT = process.cwd();
const RULE = "i18next/no-literal-string";

let eslint;
function engine() {
  eslint ??= new ESLint({ cwd: ROOT, overrideConfigFile: join(ROOT, "eslint.i18n.config.js") });
  return eslint;
}

// "src/modules/myWork/tabs/X.tsx" -> "modules/myWork"; "src/components/X.tsx" -> "components".
export function moduleOf(path) {
  const parts = path.split(/[\\/]/);
  if (parts[1] === "modules") return parts.length > 3 ? `modules/${parts[2]}` : "modules";
  return parts.length <= 2 ? "(root)" : parts[1];
}

function isCounted(path) {
  return /\.tsx$/.test(path) && !/\.test\.tsx$/.test(path);
}

// Number of hard-coded literals in one file's text (path is relative to the frontend folder).
export async function measureText(path, text) {
  if (!isCounted(path)) return 0;
  const results = await engine().lintText(text, { filePath: join(ROOT, path) });
  return results.flatMap((r) => r.messages).filter((m) => m.ruleId === RULE).length;
}

export function totalsByModule(perFile) {
  const modules = {};
  for (const { file, count } of perFile) {
    if (count > 0) modules[moduleOf(file)] = (modules[moduleOf(file)] ?? 0) + count;
  }
  return modules;
}

function sorted(modules) {
  return Object.fromEntries(Object.entries(modules).sort(([a], [b]) => a.localeCompare(b)));
}

// A module must not exceed its baseline; a module absent from the baseline has 0.
export function compare(current, baseline) {
  return Object.entries(current.modules)
    .filter(([module, count]) => count > (baseline.modules[module] ?? 0))
    .map(([module, count]) => ({ module, baseline: baseline.modules[module] ?? 0, current: count }))
    .sort((a, b) => a.module.localeCompare(b.module));
}

function* sourceFiles(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) yield* sourceFiles(full);
    else if (isCounted(entry.name)) yield full;
  }
}

async function measureTree() {
  const perFile = [];
  for (const full of sourceFiles(join(ROOT, "src"))) {
    const file = relative(ROOT, full).split(sep).join("/");
    perFile.push({ file, count: await measureText(file, readFileSync(full, "utf8")) });
  }
  const modules = totalsByModule(perFile);
  return { total: Object.values(modules).reduce((a, b) => a + b, 0), modules: sorted(modules), perFile };
}

async function main() {
  const baselinePath = join(ROOT, "i18n-debt-baseline.json");
  const init = process.argv.includes("--init");
  const update = process.argv.includes("--update");
  const current = await measureTree();

  if (init) {
    writeFileSync(baselinePath, JSON.stringify({ total: current.total, modules: current.modules }, null, 2) + "\n");
    console.log(`Baseline written: ${current.total} hard-coded literals in ${Object.keys(current.modules).length} modules.`);
    return;
  }

  const baseline = JSON.parse(readFileSync(baselinePath, "utf8"));
  console.log(`hard-coded literals ${String(current.total).padStart(6)}  (baseline ${baseline.total})`);

  const regressions = compare(current, baseline);
  if (regressions.length > 0) {
    console.error("\nHard-coded interface text went UP:");
    for (const r of regressions) {
      console.error(`  ${r.module}: ${r.baseline} -> ${r.current}`);
      const worst = current.perFile.filter((f) => moduleOf(f.file) === r.module && f.count > 0)
        .sort((a, b) => b.count - a.count).slice(0, 5);
      for (const f of worst) console.error(`      ${f.count}  ${f.file}`);
    }
    console.error("\nUse translation keys (useTranslation) instead of literal text; see src/locales/.");
    process.exit(1);
  }

  const lowered = Object.entries(baseline.modules)
    .map(([module, count]) => [module, Math.min(count, current.modules[module] ?? 0)])
    .filter(([, count]) => count > 0);
  const next = { total: lowered.reduce((a, [, c]) => a + c, 0), modules: Object.fromEntries(lowered) };

  if (update) {
    writeFileSync(baselinePath, JSON.stringify(next, null, 2) + "\n");
    console.log("\nBaseline lowered to the current counts.");
  } else if (next.total < baseline.total) {
    console.log("\nHard-coded text went down - run `npm run i18n:update` to lock in the lower baseline.");
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  main().catch((error) => {
    console.error(error);
    process.exit(2);
  });
}
