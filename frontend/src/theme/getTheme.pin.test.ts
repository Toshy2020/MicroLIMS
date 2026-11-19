import { describe, expect, it } from "vitest";
import { getTheme } from "./index";

// Stable fingerprint of a theme: every key and value, functions reduced to a marker.
async function fingerprint(value: unknown): Promise<string> {
  const json = JSON.stringify(value, (key, v) => {
    if (typeof v === "function") return "[fn]";
    // The bench flag is the only addition the bench work makes to the theme.
    if (key === "bench") return undefined;
    return v;
  });
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(json));
  return Array.from(new Uint8Array(digest), (b) => b.toString(16).padStart(2, "0")).join("");
}

describe("getTheme without bench mode", () => {
  it("light theme is identical to the pre-bench theme", async () => {
    expect(await fingerprint(getTheme("light"))).toMatchInlineSnapshot(`"ca9d63eb0f7cd8be74b5ef8c30eb76c007ad60375e795606b5380eb97e9e976e"`);
  });
  it("dark theme is identical to the pre-bench theme", async () => {
    expect(await fingerprint(getTheme("dark"))).toMatchInlineSnapshot(`"cf47d140f94b3b9e7f1ff6bc2fb2d9549907d336f8748c8cbbd91e469ab1a09a"`);
  });
});
