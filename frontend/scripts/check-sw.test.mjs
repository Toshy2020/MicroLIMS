import { mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { describe, expect, it } from "vitest";

const script = path.resolve(import.meta.dirname, "check-sw.mjs");

function runWithSw(swContent, apiOrigin = "https://api.microlims.example") {
  const dir = mkdtempSync(path.join(tmpdir(), "check-sw-"));
  const swPath = path.join(dir, "sw.js");
  writeFileSync(swPath, swContent, "utf8");
  return spawnSync(process.execPath, [script, swPath, apiOrigin], { encoding: "utf8" });
}

describe("check-sw", () => {
  it("passes when precache list and routes contain only build assets", () => {
    const sw = `
      precacheAndRoute([{url:"index.html",revision:"1"},{url:"assets/index-abc.js",revision:null}]);
      registerRoute(new NavigationRoute(createHandlerBoundToURL("/index.html")));
    `;
    const result = runWithSw(sw);
    expect(result.status).toBe(0);
    expect(result.stdout).toContain("2 precache entries");
  });

  it("fails if the generated sw.js precache list or routes contain /api/", () => {
    const swWithApiPrecache = `precacheAndRoute([{url:"/api/samples",revision:"1"}]);`;
    const res1 = runWithSw(swWithApiPrecache);
    expect(res1.status).not.toBe(0);
    expect(res1.stderr).toContain("/api/");

    const swWithApiRoute = `
      precacheAndRoute([{url:"index.html",revision:"1"}]);
      registerRoute(({url}) => url.pathname.startsWith("/api/"), new NetworkFirst());
    `;
    const res2 = runWithSw(swWithApiRoute);
    expect(res2.status).not.toBe(0);
    expect(res2.stderr).toContain("/api/");

    const swWithApiRegexRoute = `
      precacheAndRoute([{url:"index.html",revision:"1"}]);
      registerRoute(/\\/api\\//, new NetworkOnly());
    `;
    const res3 = runWithSw(swWithApiRegexRoute);
    expect(res3.status).not.toBe(0);
    expect(res3.stderr).toContain("/api/");
  });

  it("fails if the generated sw.js contains the API origin", () => {
    const sw = `
      precacheAndRoute([{url:"index.html",revision:"1"}]);
      registerRoute("https://api.microlims.example/health", new NetworkOnly());
    `;
    const result = runWithSw(sw, "https://api.microlims.example");
    expect(result.status).not.toBe(0);
    expect(result.stderr).toContain("https://api.microlims.example");
  });

  it("fails if help screenshots are precached", () => {
    const sw = `precacheAndRoute([{url:"help/screenshots/01.png",revision:"1"}]);`;
    const result = runWithSw(sw);
    expect(result.status).not.toBe(0);
    expect(result.stderr).toContain("help/screenshots/");
  });
});
