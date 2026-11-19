// Verifies the generated production service worker (dist/sw.js) never
// precaches or routes API requests, the API origin, or help screenshots.
//
// Usage: node scripts/check-sw.mjs [swPath] [apiOrigin]
import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const DEFAULT_SW_PATH = path.resolve(here, "../dist/sw.js");

function resolveApiOrigin(explicitOrigin) {
  // Same variables the app reads for its API base URL (src/services/apiBaseUrl.ts).
  const raw = explicitOrigin || process.env.VITE_API_ORIGIN || process.env.VITE_API_BASE_URL || process.env.VITE_API_URL || "";
  if (!raw.trim()) return "";
  try {
    return new URL(raw).origin;
  } catch {
    return raw.trim();
  }
}

function extractPrecacheUrls(swText) {
  const match = swText.match(/precacheAndRoute\(\s*(\[[\s\S]*?\])\s*[,)]/);
  if (!match) return null;
  const urls = [];
  const urlPattern = /["']?url["']?\s*:\s*["']([^"']+)["']/g;
  for (const m of match[1].matchAll(urlPattern)) {
    urls.push(m[1]);
  }
  return urls;
}

export function checkServiceWorker(swPath = DEFAULT_SW_PATH, { apiOrigin } = {}) {
  if (!existsSync(swPath)) {
    throw new Error(`check-sw: service worker not found at ${swPath}`);
  }
  const swText = readFileSync(swPath, "utf8");
  if (!swText.trim()) {
    throw new Error(`check-sw: service worker at ${swPath} is empty`);
  }

  const normalizedSwText = swText.replace(/\\\//g, "/");
  const forbidden = ["/api/", "help/screenshots/"];
  const resolvedOrigin = resolveApiOrigin(apiOrigin);
  if (resolvedOrigin) {
    forbidden.push(resolvedOrigin);
  }

  for (const token of forbidden) {
    if (normalizedSwText.includes(token)) {
      throw new Error(`check-sw: forbidden reference "${token}" found in ${swPath}`);
    }
  }

  const precacheUrls = extractPrecacheUrls(swText);
  if (!precacheUrls || precacheUrls.length === 0) {
    throw new Error(`check-sw: no precacheAndRoute entries found in ${swPath}`);
  }

  for (const url of precacheUrls) {
    for (const token of forbidden) {
      if (url.includes(token)) {
        throw new Error(`check-sw: forbidden precache entry "${url}" (matched "${token}")`);
      }
    }
  }

  return { precacheCount: precacheUrls.length, precacheUrls };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const [swArg, originArg] = process.argv.slice(2);
  try {
    const result = checkServiceWorker(swArg ? path.resolve(swArg) : DEFAULT_SW_PATH, {
      apiOrigin: originArg,
    });
    console.log(`check-sw: OK (${result.precacheCount} precache entries)`);
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exit(1);
  }
}
