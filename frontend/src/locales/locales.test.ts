import { describe, expect, it } from "vitest";
import glossary from "./glossary.json";

type Tree = { [key: string]: string | Tree };
type Flat = Record<string, string>;

// Every namespace file in both languages, read straight from disk.
const files = import.meta.glob<Tree>("./*/*.json", { eager: true, import: "default" });

function flatten(tree: Tree, prefix = ""): Flat {
  const out: Flat = {};
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === "string") out[path] = value;
    else Object.assign(out, flatten(value, path));
  }
  return out;
}

function namespaces(lng: "en" | "ar"): Record<string, Flat> {
  const result: Record<string, Flat> = {};
  for (const [path, tree] of Object.entries(files)) {
    const match = /^\.\/(en|ar)\/(.+)\.json$/.exec(path);
    if (match && match[1] === lng) result[match[2]] = flatten(tree);
  }
  return result;
}

// {{name}} and {{name, format}} both count as the placeholder "name".
function placeholdersOf(text: string): string[] {
  return [...text.matchAll(/\{\{\s*([^,}\s]+)[^}]*\}\}/g)].map((m) => m[1]).sort();
}

function placeholderMismatches(en: Flat, ar: Flat): string[] {
  const problems: string[] = [];
  for (const [key, arText] of Object.entries(ar)) {
    const enText = en[key];
    if (enText === undefined) {
      problems.push(`${key}: exists in Arabic but not in English`);
      continue;
    }
    const want = placeholdersOf(enText).join(",");
    const got = placeholdersOf(arText).join(",");
    if (want !== got) problems.push(`${key}: English has [${want}] but Arabic has [${got}]`);
  }
  return problems;
}

function escapeRegExp(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function glossaryViolations(en: Flat, ar: Flat, terms: Record<string, string>): string[] {
  const problems: string[] = [];
  for (const [key, arText] of Object.entries(ar)) {
    const enText = en[key];
    if (enText === undefined) continue;
    for (const [term, arabicTerm] of Object.entries(terms)) {
      const inEnglish = new RegExp(`(?<![A-Za-z])${escapeRegExp(term)}(?![A-Za-z])`, "i").test(enText);
      if (inEnglish && !arText.includes(arabicTerm)) {
        problems.push(`${key}: English contains "${term}" so Arabic must contain "${arabicTerm}"`);
      }
    }
  }
  return problems;
}

describe("locale files", () => {
  it("has an English and an Arabic common namespace", () => {
    expect(Object.keys(namespaces("en"))).toContain("common");
    expect(Object.keys(namespaces("ar"))).toContain("common");
  });

  it("Placeholders_match_between_languages", () => {
    const en = namespaces("en");
    const ar = namespaces("ar");
    const problems = Object.entries(ar).flatMap(([ns, flat]) =>
      placeholderMismatches(en[ns] ?? {}, flat).map((p) => `${ns}: ${p}`)
    );
    expect(problems).toEqual([]);
  });

  it("detects a dropped, added or renamed placeholder", () => {
    const en = { a: "{{count}} samples in {{lab}}", b: "plain", c: "{{n, number}} items" };
    expect(placeholderMismatches(en, { a: "{{count}} عينات في {{lab}}", b: "عادي", c: "{{n}} عناصر" })).toEqual([]);
    expect(placeholderMismatches(en, { a: "عينات في {{lab}}" })).toHaveLength(1);
    expect(placeholderMismatches(en, { b: "{{extra}} عادي" })).toHaveLength(1);
    expect(placeholderMismatches(en, { a: "{{total}} عينات في {{lab}}" })).toHaveLength(1);
    expect(placeholderMismatches(en, { zz: "x" })).toHaveLength(1);
  });

  it("uses the glossary term wherever the English source contains one", () => {
    const en = namespaces("en");
    const ar = namespaces("ar");
    const problems = Object.entries(ar).flatMap(([ns, flat]) =>
      glossaryViolations(en[ns] ?? {}, flat, glossary).map((p) => `${ns}: ${p}`)
    );
    expect(problems).toEqual([]);
  });

  it("detects an Arabic string that ignores a glossary term", () => {
    const terms = { Sample: "عينة", "Out of specification": "خارج المواصفات" };
    expect(glossaryViolations({ a: "Sample received" }, { a: "نموذج مستلم" }, terms)).toHaveLength(1);
    expect(glossaryViolations({ a: "Sample received" }, { a: "تم استلام العينة" }, terms)).toEqual([]);
    expect(glossaryViolations({ a: "Samples received" }, { a: "نماذج" }, terms)).toEqual([]);
    expect(glossaryViolations({ a: "Out of specification" }, { a: "خارج الحدود" }, terms)).toHaveLength(1);
  });

  it("keeps every glossary entry complete and unique", () => {
    const entries = Object.entries(glossary);
    expect(entries.length).toBeGreaterThan(0);
    for (const [term, arabic] of entries) {
      expect(term.trim()).not.toBe("");
      expect(arabic.trim()).not.toBe("");
      expect(/[؀-ۿ]/.test(arabic)).toBe(true);
    }
    const arabicTerms = entries.map(([, a]) => a);
    expect(new Set(arabicTerms).size).toBe(arabicTerms.length);
  });
});
