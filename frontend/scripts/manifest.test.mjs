import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("manifest.webmanifest", () => {
  it("manifest has name, short_name, start_url \"/\", display \"standalone\", orientation \"any\", 192/512/maskable icons", () => {
    const publicDir = path.resolve(import.meta.dirname, "../public");
    const raw = readFileSync(path.join(publicDir, "manifest.webmanifest"), "utf8");
    const manifest = JSON.parse(raw);

    expect(manifest.name).toBe("MicroLIMS");
    expect(manifest.short_name).toBe("MicroLIMS");
    expect(manifest.start_url).toBe("/");
    expect(manifest.display).toBe("standalone");
    expect(manifest.orientation).toBe("any");

    const has192 = manifest.icons.some((i) => i.sizes === "192x192" && i.type === "image/png");
    const has512 = manifest.icons.some((i) => i.sizes === "512x512" && i.type === "image/png" && i.purpose !== "maskable");
    const hasMaskable = manifest.icons.some((i) => i.sizes === "512x512" && i.type === "image/png" && i.purpose === "maskable");

    expect(has192).toBe(true);
    expect(has512).toBe(true);
    expect(hasMaskable).toBe(true);

    for (const icon of manifest.icons) {
      const iconPath = path.join(publicDir, icon.src.replace(/^\//, ""));
      expect(existsSync(iconPath)).toBe(true);
    }
  });
});
