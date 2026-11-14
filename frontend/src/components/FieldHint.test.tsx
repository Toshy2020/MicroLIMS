import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { BenchModeProvider, BENCH_MODE_STORAGE_KEY } from "../theme/BenchModeContext";
import { HelpPanelProvider } from "../modules/help/HelpPanelContext";
import { HelpPanelSlot } from "../modules/help/HelpPanelSlot";
import { CHAPTERS } from "../modules/help/content/guideLoader";
import { FieldHint, describedByWithHint, hintDescribedBy } from "./FieldHint";
import type { Hint } from "./FieldHint";
import type { HelpPanelServices } from "../modules/help/HelpPanelContext";

const GUIDANCE = "Guidance only. Your approved SOPs take precedence.";
const PLAIN_TEXT = "The code that identifies this item. It must be unique.";
const GUIDED_HINT: Hint = {
  text: PLAIN_TEXT,
  guide: { chapterId: "31-items-test-master-specifications", heading: "Creating an item" }
};

// Chapter text stub: the help panel is a real component; only the guide
// loader (a content boundary) is replaced.
const CHAPTER_TEXT = [
  "## 31. Items, Test Master and Specifications",
  "",
  "Intro text for the items chapter.",
  "",
  "### Creating an item",
  "",
  "Steps for creating an item."
].join("\n");

const services: HelpPanelServices = {
  loadChapter: vi.fn(async () => CHAPTER_TEXT),
  search: vi.fn(async () => [])
};

let scrolled: Element[] = [];

beforeEach(() => {
  scrolled = [];
  Element.prototype.scrollIntoView = function scrollIntoView(this: Element) {
    scrolled.push(this);
  };
});

afterEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
});

function renderField(hint: Hint = GUIDED_HINT, { bench = false }: { bench?: boolean } = {}) {
  localStorage.setItem(BENCH_MODE_STORAGE_KEY, bench ? "on" : "off");
  return render(
    <BenchModeProvider>
      <PinnedLightTheme>
        <MemoryRouter initialEntries={["/laboratory-configuration/items"]}>
          <HelpPanelProvider>
            <form>
              <FieldHint id="item-code" label="Item Code" hint={hint} />
              <label htmlFor="item-code">Item Code</label>
              <input id="item-code" type="text" aria-describedby={hintDescribedBy("item-code")} />
            </form>
            <HelpPanelSlot services={services} />
          </HelpPanelProvider>
        </MemoryRouter>
      </PinnedLightTheme>
    </BenchModeProvider>
  );
}

function aboutButton() {
  return screen.getByRole("button", { name: "About Item Code" });
}

describe("FieldHint", () => {
  it("opens on click, Enter and Space; not on hover", async () => {
    const user = userEvent.setup();
    renderField();
    const button = aboutButton();

    await user.hover(button);
    expect(screen.queryByRole("dialog")).toBeNull();

    await user.click(button);
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(PLAIN_TEXT)).toBeTruthy();
    expect(within(dialog).getByText(GUIDANCE)).toBeTruthy();
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    aboutButton().focus();
    await user.keyboard("{Enter}");
    expect(await screen.findByRole("dialog")).toBeTruthy();
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    aboutButton().focus();
    await user.keyboard(" ");
    expect(await screen.findByRole("dialog")).toBeTruthy();
  });

  it("Escape closes and returns focus", async () => {
    const user = userEvent.setup();
    renderField();
    await user.click(aboutButton());
    await screen.findByRole("dialog");

    await user.keyboard("{Escape}");

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(document.activeElement).toBe(aboutButton());
  });

  it('"Read more" opens the help panel at the linked heading', async () => {
    const user = userEvent.setup();
    renderField();
    await user.click(aboutButton());
    await user.click(await screen.findByRole("button", { name: "Read more" }));

    const panel = await screen.findByRole("region", { name: "Help" });
    expect(await within(panel).findByText("Intro text for the items chapter.")).toBeTruthy();
    await waitFor(() => {
      expect(scrolled.some((el) => el.textContent === "Creating an item")).toBe(true);
    });
    expect(services.loadChapter).toHaveBeenCalledWith("31-items-test-master-specifications");
  });

  it('has no "Read more" when the hint has no guide link', async () => {
    const user = userEvent.setup();
    renderField({ text: PLAIN_TEXT });
    await user.click(aboutButton());
    await screen.findByRole("dialog");
    expect(screen.queryByRole("button", { name: "Read more" })).toBeNull();
  });

  it("target is ≥ 24 px (≥ 48 px in bench mode)", () => {
    const { unmount } = renderField(GUIDED_HINT, { bench: false });
    const standard = getComputedStyle(aboutButton());
    expect(parseFloat(standard.minWidth)).toBeGreaterThanOrEqual(24);
    expect(parseFloat(standard.minHeight)).toBeGreaterThanOrEqual(24);
    unmount();

    renderField(GUIDED_HINT, { bench: true });
    const bench = getComputedStyle(aboutButton());
    expect(parseFloat(bench.minWidth)).toBeGreaterThanOrEqual(48);
    expect(parseFloat(bench.minHeight)).toBeGreaterThanOrEqual(48);
  });

  it("the field is described by the hint text", () => {
    renderField();
    const input = document.getElementById("item-code");
    expect(input?.getAttribute("aria-describedby")).toBe(hintDescribedBy("item-code"));
    const described = document.getElementById(hintDescribedBy("item-code"));
    expect(described?.textContent).toBe(PLAIN_TEXT);
  });
});

describe("describedByWithHint", () => {
  it("lists the helper text first, then the hint", () => {
    expect(describedByWithHint("code", "code-field")).toBe("code-field-helper-text code-hint");
  });

  it("lists only the hint when no helper text is shown", () => {
    expect(describedByWithHint("code", "code-field", false)).toBe("code-hint");
  });
});

// Every hint key is used by exactly one FieldHint, and every hint points at a
// real guide chapter. The scan reads the module sources, so a key that is
// defined but never placed on a form (or placed twice) fails the build.
const HINT_FILES = import.meta.glob("../modules/**/hints.ts", { eager: true }) as Record<
  string,
  { hints: Record<string, Hint> }
>;
const MODULE_SOURCES = import.meta.glob("../modules/**/*.tsx", {
  query: "?raw",
  import: "default",
  eager: true
}) as Record<string, string>;

describe("shared field hints", () => {
  it("has hints.ts files in the first forms' modules", () => {
    const files = Object.keys(HINT_FILES).map((p) => p.replace(/^\.\.\/modules\//, "").replace(/\/hints\.ts$/, "")).sort();
    expect(files).toEqual([
      "laboratoryConfiguration/items",
      "laboratoryConfiguration/masterDataSimple",
      "laboratoryConfiguration/media",
      "receiving",
      "testingWorkspace"
    ]);
  });

  it("every key in each hints.ts is used by exactly one FieldHint", () => {
    for (const [path, mod] of Object.entries(HINT_FILES)) {
      const dir = path.slice(0, path.lastIndexOf("/") + 1);
      const text = Object.entries(MODULE_SOURCES)
        .filter(([p]) => p.startsWith(dir) && !/\.test\.tsx$/.test(p))
        .map(([, source]) => source)
        .join("\n");
      const usages = [...text.matchAll(/hint=\{hints\.(\w+)\}/g)].map((m) => m[1]);
      for (const key of Object.keys(mod.hints)) {
        expect(usages.filter((u) => u === key), `${path} key "${key}"`).toHaveLength(1);
      }
      const tags = text.match(/<FieldHint\b/g)?.length ?? 0;
      expect(tags, `${path}: every <FieldHint> takes a hints.ts key`).toBe(usages.length);
    }
  });

  it("every guide.chapterId exists in CHAPTERS", () => {
    const ids = new Set(CHAPTERS.map((c) => c.id));
    const missing = Object.entries(HINT_FILES).flatMap(([path, mod]) =>
      Object.entries(mod.hints)
        .filter(([, hint]) => hint.guide && !ids.has(hint.guide.chapterId))
        .map(([key, hint]) => `${path} ${key} -> ${hint.guide?.chapterId}`)
    );
    expect(missing).toEqual([]);
  });

  it("no hint text matches /\\d+\\s*(cfu|%|°C|hours?|days?)/i and no hint states a number", () => {
    const offending = Object.entries(HINT_FILES).flatMap(([path, mod]) =>
      Object.entries(mod.hints)
        .filter(([, hint]) => /\d+\s*(cfu|%|°C|hours?|days?)/i.test(hint.text) || /\d/.test(hint.text))
        .map(([key]) => `${path} ${key}`)
    );
    expect(offending).toEqual([]);
  });
});
