import { describe, expect, it } from "vitest";
import { i18n } from "./index";

// Every English namespace ships with the app, so a screen never shows a raw key
// while its texts load (i18n runs with useSuspense: false).
describe("bundled English texts", () => {
  it("a namespace other than common is available at once, without loading it", () => {
    expect(i18n.hasResourceBundle("en", "myWork")).toBe(true);
    expect(i18n.hasResourceBundle("en", "navigation")).toBe(true);
    expect(i18n.t("title", { ns: "myWork", lng: "en" })).toBe("My Work");
  });

  it("an Arabic screen shows English, never the key, until its Arabic file arrives", () => {
    const fixedT = i18n.getFixedT("ar", "myWork");
    const text = fixedT("title");
    expect(text).not.toBe("title");
  });
});
