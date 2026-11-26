import { describe, expect, it } from "vitest";
import { getGroupedMenu } from "../../../routes/menuConfig";
import type { MenuGroup, MenuItem } from "../../../routes/menuConfig";
import { PERMISSIONS } from "../../../routes/routes";
import { CHAPTERS } from "./guideLoader";
import { HELP_MAP, chapterForPath } from "./helpMap";
import { searchGuide } from "./searchIndex";

function menuPaths(groups: MenuGroup[]): string[] {
  const paths: string[] = [];
  const visit = (item: MenuItem) => {
    if (item.path) paths.push(item.path);
    item.children?.forEach(visit);
  };
  groups.forEach((g) => g.items.forEach(visit));
  return paths;
}

describe("guide chapters", () => {
  it("CHAPTERS has 46 entries numbered 1-46 with titles", () => {
    expect(CHAPTERS).toHaveLength(46);
    expect(CHAPTERS.map((c) => c.number)).toEqual(Array.from({ length: 46 }, (_, i) => i + 1));
    for (const chapter of CHAPTERS) {
      expect(chapter.id).toMatch(/^\d\d-[a-z0-9-]+$/);
      expect(chapter.title.length).toBeGreaterThan(0);
    }
  });

  it("no chapter shows markup markers: author comments are removed and none is malformed", async () => {
    // MarkdownView renders raw HTML as visible text. Chapters keep their author
    // comments (traceability markers); the loader removes them, and a malformed
    // one would survive and show on screen.
    for (const chapter of CHAPTERS) {
      const text = await chapter.load();
      for (const marker of ["<!--", "<--", "--!>"]) {
        expect(text.includes(marker), `${chapter.id} contains "${marker}"`).toBe(false);
      }
    }
  });

  it("the User Interface chapter has no unfilled {placeholder} left in its text", async () => {
    const chapter = CHAPTERS.find((c) => c.number === 6);
    const text = await chapter!.load();
    expect(text.match(/\{[A-Za-z][A-Za-z ]*\}/g) ?? []).toEqual([]);
  });

  it("each listed title is the first '## N. Title' line of its chapter", async () => {
    for (const chapter of CHAPTERS) {
      const text = await chapter.load();
      const match = /^## (\d+)\. (.+)$/m.exec(text);
      expect(match, chapter.id).not.toBeNull();
      expect(Number(match?.[1]), chapter.id).toBe(chapter.number);
      expect(match?.[2].trim(), chapter.id).toBe(chapter.title);
    }
  });
});

describe("HELP_MAP", () => {
  it("every menuConfig path has a HELP_MAP entry", () => {
    const groups = getGroupedMenu({
      role: "SectionHead",
      permissions: Object.values(PERMISSIONS),
      labCodes: ["MICRO", "FP"],
      physchemAreas: ["fp", "rmpm"]
    });
    const missing = [...new Set(menuPaths(groups))].filter((p) => !(p in HELP_MAP));
    expect(missing, `menu paths without a help chapter: ${missing.join(", ")}`).toEqual([]);
  });

  it("every HELP_MAP chapterId exists in CHAPTERS", () => {
    const ids = new Set(CHAPTERS.map((c) => c.id));
    const unknown = Object.entries(HELP_MAP).filter(([, t]) => !ids.has(t.chapterId)).map(([p, t]) => `${p} -> ${t.chapterId}`);
    expect(unknown).toEqual([]);
  });
});

describe("chapterForPath", () => {
  it("maps an exact path", () => {
    expect(chapterForPath("/my-work", "").chapterId).toBe("45-my-work");
    expect(chapterForPath("/quality", "").chapterId).toBe("46-quality-events");
    expect(chapterForPath("/quality/config", "").chapterId).toBe("46-quality-events");
    expect(chapterForPath("/receiving", "").chapterId).toBe("12-sample-receiving");
    expect(chapterForPath("/receiving/tracking", "").chapterId).toBe("11-sample-management");
  });

  it("uses the query string to choose between entries of the same path", () => {
    expect(chapterForPath("/preparation", "?lab=MICRO").chapterId).toBe("28-solution-standard-preparation");
    expect(chapterForPath("/inventory/materials", "?lab=FP").chapterId).toBe("29-material-stock");
    expect(chapterForPath("/inventory/equipment", "?lab=FP&x=1").chapterId).toBe("30-equipment");
  });

  it("maps a deep child to its nearest ancestor", () => {
    expect(chapterForPath("/oos-tracking/123", "").chapterId).toBe("20-oos-tracking");
    expect(chapterForPath("/document-control/documents/7", "").chapterId).toBe("38-document-control");
  });

  it("falls back to the parent section, then to the introduction", () => {
    expect(chapterForPath("/laboratory-configuration/brand-new-page", "").chapterId).toBe("32-laboratory-configuration");
    expect(chapterForPath("/something/unknown", "").chapterId).toBe("01-introduction");
  });
});

describe("searchGuide", () => {
  it("finds a word in a chapter body and returns its nearest heading", async () => {
    const chapter = CHAPTERS.find((c) => c.number === 20);
    const lines = (await chapter!.load()).split(/\r?\n/);
    let heading = "";
    let phrase = "";
    for (const line of lines) {
      const h = /^#{3,6}\s+(.*)$/.exec(line);
      if (h) heading = h[1].replace(/[`*_]/g, "").trim();
      else if (heading && /^[A-Z][a-z]+ [a-z]+ [a-z]+ [a-z]+ [a-z]+/.test(line)) {
        phrase = line.split(" ").slice(0, 5).join(" ");
        break;
      }
    }
    expect(phrase).not.toBe("");

    const results = await searchGuide(phrase);
    const hit = results.find((r) => r.chapterId === chapter!.id && r.heading === heading);
    expect(hit, `no result under "${heading}" for "${phrase}"`).toBeDefined();
    expect(hit?.excerpt.toLowerCase()).toContain(phrase.toLowerCase());
    expect(hit?.title).toBe(chapter!.title);
  });

  it("returns nothing for a one-letter or empty query", async () => {
    expect(await searchGuide("")).toEqual([]);
    expect(await searchGuide("a")).toEqual([]);
  });
});
