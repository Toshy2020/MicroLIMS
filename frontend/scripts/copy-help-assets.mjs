// Copies the guide screenshots into the production build (dist/help/screenshots)
// and fails the build when a guide chapter references a screenshot that does
// not exist, so the in-app Help never ships a broken image.
//
// Usage: node scripts/copy-help-assets.mjs [guideDir] [screenshotsDir] [outDir]
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const DEFAULTS = {
  guideDir: path.resolve(here, "../../documentation/guide"),
  screenshotsDir: path.resolve(here, "../../documentation/screenshots"),
  outDir: path.resolve(here, "../dist/help/screenshots")
};

const IMAGE = /!\[[^\]]*\]\(\s*([^)\s]+)/g;

export function findMissingScreenshots(guideDir, screenshotsDir) {
  const missing = [];
  const chapters = readdirSync(guideDir).filter((f) => /^\d\d-.*\.md$/.test(f));
  for (const chapter of chapters) {
    const text = readFileSync(path.join(guideDir, chapter), "utf8");
    for (const match of text.matchAll(IMAGE)) {
      const src = match[1];
      const idx = src.indexOf("screenshots/");
      if (idx < 0 || /^[a-z]+:/i.test(src)) continue;
      const file = decodeURIComponent(src.slice(idx + "screenshots/".length));
      if (!existsSync(path.join(screenshotsDir, file))) missing.push({ chapter, file });
    }
  }
  return missing;
}

export function copyHelpAssets({ guideDir, screenshotsDir, outDir } = DEFAULTS) {
  const missing = findMissingScreenshots(guideDir, screenshotsDir);
  if (missing.length > 0) {
    const lines = missing.map((m) => `  chapter ${m.chapter} references missing screenshot ${m.file}`);
    throw new Error(`Help assets: ${missing.length} missing screenshot(s)\n${lines.join("\n")}`);
  }
  mkdirSync(outDir, { recursive: true });
  cpSync(screenshotsDir, outDir, { recursive: true });
  return readdirSync(outDir).length;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const [guideDir, screenshotsDir, outDir] = process.argv.slice(2);
  try {
    const count = copyHelpAssets({
      guideDir: guideDir ? path.resolve(guideDir) : DEFAULTS.guideDir,
      screenshotsDir: screenshotsDir ? path.resolve(screenshotsDir) : DEFAULTS.screenshotsDir,
      outDir: outDir ? path.resolve(outDir) : DEFAULTS.outDir
    });
    console.log(`Help assets: ${count} screenshots copied`);
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exit(1);
  }
}
