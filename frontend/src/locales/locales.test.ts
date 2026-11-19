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

// Arabic has six plural forms (zero, one, two, few, many, other) where English has
// two, so a plural key may exist in Arabic under a suffix English does not have.
// Such a key is compared with the English "other" form of the same key.
const PLURAL_SUFFIX = /_(zero|one|two|few|many|other)$/;
function englishText(en: Flat, key: string): string | undefined {
  return en[key] ?? (PLURAL_SUFFIX.test(key) ? en[key.replace(PLURAL_SUFFIX, "_other")] : undefined);
}

function placeholderMismatches(en: Flat, ar: Flat): string[] {
  const problems: string[] = [];
  for (const [key, arText] of Object.entries(ar)) {
    const enText = englishText(en, key);
    if (enText === undefined) {
      problems.push(`${key}: exists in Arabic but not in English`);
      continue;
    }
    // Arabic grammar leaves the number out of the zero, one and two forms
    // ("لا توجد مهام", "مهمة واحدة", "مهمتان"), so there {{count}} may be omitted;
    // every other placeholder must still match.
    const countOptional = /_(zero|one|two)$/.test(key);
    const wanted = placeholdersOf(enText);
    const gotList = placeholdersOf(arText);
    const want = (countOptional && !gotList.includes("count") ? wanted.filter((p) => p !== "count") : wanted).join(",");
    const got = gotList.join(",");
    if (want !== got) problems.push(`${key}: English has [${want}] but Arabic has [${got}]`);
  }
  return problems;
}

function escapeRegExp(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

// A glossary entry is the preferred Arabic term, or a list of accepted forms (the first is preferred), e.g.
// "Sample": ["عينة", "العينات"] so that "موقع أخذ العينات" is accepted for "sample location".
type GlossaryTerms = Record<string, string | string[]>;

function acceptedForms(arabic: string | string[]): string[] {
  return Array.isArray(arabic) ? arabic : [arabic];
}

// Placeholders ({{lot}}) and markup tags (<strong>) are not English words.
function englishWords(text: string): string {
  return text.replace(/\{\{[^}]*\}\}/g, " ").replace(/<[^>]*>/g, " ");
}

function glossaryViolations(en: Flat, ar: Flat, terms: GlossaryTerms): string[] {
  const problems: string[] = [];
  for (const [key, arText] of Object.entries(ar)) {
    const enText = englishText(en, key);
    if (enText === undefined) continue;
    const words = englishWords(enText);
    for (const [term, arabic] of Object.entries(terms)) {
      const inEnglish = new RegExp(`(?<![A-Za-z])${escapeRegExp(term)}(?![A-Za-z])`, "i").test(words);
      const forms = acceptedForms(arabic);
      if (inEnglish && !forms.some((form) => arText.includes(form))) {
        problems.push(`${key}: English contains "${term}" so Arabic must contain "${forms.join('" or "')}"`);
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

  it("Arabic zero, one and two forms may leave out the count, but nothing else", () => {
    const en = { tasks_one: "{{count}} task in {{lab}}", tasks_other: "{{count}} tasks in {{lab}}" };
    expect(placeholderMismatches(en, { tasks_two: "مهمتان في {{lab}}", tasks_one: "مهمة واحدة في {{lab}}" })).toEqual([]);
    expect(placeholderMismatches(en, { tasks_few: "مهام في {{lab}}" })).toHaveLength(1);
    expect(placeholderMismatches(en, { tasks_two: "مهمتان" })).toHaveLength(1);
  });

  it("detects a dropped, added or renamed placeholder", () => {
    const en = { a: "{{count}} samples in {{lab}}", b: "plain", c: "{{n, number}} items" };
    expect(placeholderMismatches(en, { a: "{{count}} عينات في {{lab}}", b: "عادي", c: "{{n}} عناصر" })).toEqual([]);
    expect(placeholderMismatches(en, { a: "عينات في {{lab}}" })).toHaveLength(1);
    expect(placeholderMismatches(en, { b: "{{extra}} عادي" })).toHaveLength(1);
    expect(placeholderMismatches(en, { a: "{{total}} عينات في {{lab}}" })).toHaveLength(1);
    expect(placeholderMismatches(en, { zz: "x" })).toHaveLength(1);
  });

  it("accepts Arabic plural forms that English does not have, and still checks their placeholders", () => {
    const en = { tasks_one: "{{count}} task", tasks_other: "{{count}} tasks" };
    expect(placeholderMismatches(en, { tasks_few: "{{count}} مهام", tasks_two: "{{count}} مهمتان" })).toEqual([]);
    expect(placeholderMismatches(en, { tasks_few: "مهام" })).toHaveLength(1);
    expect(placeholderMismatches(en, { tasks_many_extra: "x" })).toHaveLength(1);
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

  it("accepts any listed form of a term and ignores placeholders and tags", () => {
    const terms = { Sample: ["عينة", "العينات"], Lot: ["تشغيلة", "تشغيلات"] };
    expect(glossaryViolations({ a: "Sample locations" }, { a: "مواقع أخذ العينات" }, terms)).toEqual([]);
    expect(glossaryViolations({ a: "Sample locations" }, { a: "مواقع" }, terms)).toHaveLength(1);
    expect(glossaryViolations({ a: "{{name}}, {{lot}}" }, { a: "{{name}}، {{lot}}" }, terms)).toEqual([]);
    expect(glossaryViolations({ a: "<lot>Media</lot> lot" }, { a: "دفعة" }, terms)).toHaveLength(1);
  });

  it("keeps every glossary entry complete and unique", () => {
    const entries = Object.entries(glossary);
    expect(entries.length).toBeGreaterThan(0);
    for (const [term, arabic] of entries) {
      expect(term.trim()).not.toBe("");
      for (const form of acceptedForms(arabic)) {
        expect(form.trim()).not.toBe("");
        expect(/[؀-ۿ]/.test(form)).toBe(true);
      }
    }
    const arabicTerms = entries.map(([, a]) => acceptedForms(a)[0]);
    expect(new Set(arabicTerms).size).toBe(arabicTerms.length);
  });
});
