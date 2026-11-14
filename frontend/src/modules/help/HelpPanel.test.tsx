import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Link, MemoryRouter } from "react-router-dom";
import { useState } from "react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { HelpMenu } from "./HelpMenu";
import { HelpPanelProvider } from "./HelpPanelContext";
import type { HelpPanelServices } from "./HelpPanelContext";
import { HelpPanelSlot } from "./HelpPanelSlot";

// The only module mocked is a services/ boundary (the version endpoint).
const versionService = vi.hoisted(() => ({ get: vi.fn() }));
vi.mock("../../services/systemVersionService", () => ({ systemVersionService: versionService }));

const GUIDANCE = "Guidance only. Your approved SOPs take precedence.";

function chapterText(id: string): string {
  return `## 1. Chapter ${id}\n\nIntro for ${id}.\n\n### Reading the result\n\nDetails for ${id}.`;
}

function makeServices(overrides: Partial<HelpPanelServices> = {}): HelpPanelServices {
  return {
    loadChapter: vi.fn(async (id: string) => chapterText(id)),
    ...overrides
  };
}

function PageControls() {
  const [count, setCount] = useState(0);
  return (
    <main>
      <button type="button" onClick={() => setCount((c) => c + 1)}>Page action</button>
      <output aria-label="Page clicks">{count}</output>
      <Link to="/oos-tracking">Go to OOS</Link>
      <label>
        Notes
        <input type="text" />
      </label>
    </main>
  );
}

function renderHelp(services: HelpPanelServices, initialPath = "/receiving") {
  return render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={[initialPath]}>
        <HelpPanelProvider>
          <HelpMenu />
          <PageControls />
          <HelpPanelSlot services={services} />
        </HelpPanelProvider>
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

async function openFromMenu(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole("button", { name: "Help" }));
  await user.click(await screen.findByRole("menuitem", { name: /Help for this page/ }));
  return screen.findByRole("region", { name: "Help" });
}

let scrolled: Element[] = [];

beforeEach(() => {
  versionService.get.mockResolvedValue({ version: "1.0.0", environment: "Test" });
  scrolled = [];
  Element.prototype.scrollIntoView = function scrollIntoView(this: Element) {
    scrolled.push(this);
  };
});

afterEach(() => {
  vi.clearAllMocks();
  vi.unstubAllGlobals();
});

describe("HelpPanel", () => {
  it("opens the chapter mapped to the current route and scrolls to the mapped heading", async () => {
    const user = userEvent.setup();
    const services = makeServices({
      resolveTarget: () => ({ chapterId: "12-sample-receiving", heading: "Reading the result" })
    });
    renderHelp(services);

    const panel = await openFromMenu(user);
    expect(await within(panel).findByRole("heading", { name: "1. Chapter 12-sample-receiving" })).toBeTruthy();
    expect(services.loadChapter).toHaveBeenCalledWith("12-sample-receiving");
    const target = within(panel).getByRole("heading", { name: "Reading the result" });
    await waitFor(() => expect(scrolled).toContain(target));
  });

  it("route change while open switches chapter", async () => {
    const user = userEvent.setup();
    renderHelp(makeServices(), "/receiving");

    const panel = await openFromMenu(user);
    expect(await within(panel).findByRole("heading", { name: "1. Chapter 12-sample-receiving" })).toBeTruthy();

    await user.click(screen.getByRole("link", { name: "Go to OOS" }));
    expect(await within(panel).findByRole("heading", { name: "1. Chapter 20-oos-tracking" })).toBeTruthy();
    expect(within(panel).queryByRole("heading", { name: "1. Chapter 12-sample-receiving" })).toBeNull();
  });

  it("F1 opens; Escape closes and returns focus to the Help button", async () => {
    const user = userEvent.setup();
    renderHelp(makeServices());

    expect(screen.queryByRole("region", { name: "Help" })).toBeNull();
    const notPrevented = document.body.dispatchEvent(new KeyboardEvent("keydown", { key: "F1", bubbles: true, cancelable: true }));
    expect(notPrevented).toBe(false); // default browser help is prevented
    const panel = await screen.findByRole("region", { name: "Help" });
    await waitFor(() => expect(document.activeElement).toBe(within(panel).getByRole("heading", { name: "Help" })));

    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("region", { name: "Help" })).toBeNull());
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Help" }));
  });

  it("F1 inside a text field with a modifier held is left to the browser", async () => {
    const user = userEvent.setup();
    renderHelp(makeServices());
    await user.click(screen.getByRole("textbox", { name: "Notes" }));
    const field = screen.getByRole("textbox", { name: "Notes" });
    const notPrevented = field.dispatchEvent(new KeyboardEvent("keydown", { key: "F1", ctrlKey: true, bubbles: true, cancelable: true }));
    expect(notPrevented).toBe(true);
    expect(screen.queryByRole("region", { name: "Help" })).toBeNull();
    // Plain F1 in the same field still opens help.
    field.dispatchEvent(new KeyboardEvent("keydown", { key: "F1", bubbles: true, cancelable: true }));
    expect(await screen.findByRole("region", { name: "Help" })).toBeTruthy();
  });

  it("Escape pressed while working in the page leaves the panel open and focus where it was", async () => {
    const user = userEvent.setup();
    renderHelp(makeServices());
    await openFromMenu(user);

    const notes = screen.getByRole("textbox", { name: "Notes" });
    await user.click(notes);
    await user.keyboard("{Escape}");
    expect(document.activeElement).toBe(notes);
    expect(screen.getByRole("region", { name: "Help" })).toBeTruthy();
  });

  it("page controls remain clickable while open", async () => {
    const user = userEvent.setup();
    renderHelp(makeServices());
    await openFromMenu(user);

    await user.click(screen.getByRole("button", { name: "Page action" }));
    await user.click(screen.getByRole("button", { name: "Page action" }));
    expect(screen.getByLabelText("Page clicks").textContent).toBe("2");
    expect(screen.getByRole("region", { name: "Help" })).toBeTruthy();
    // Not modal: nothing behind is hidden from assistive technology.
    expect(screen.getByRole("button", { name: "Page action" }).closest("[aria-hidden='true']")).toBeNull();
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("guidance line and version shown", async () => {
    const user = userEvent.setup();
    renderHelp(makeServices());
    const panel = await openFromMenu(user);
    expect(within(panel).getByText(GUIDANCE)).toBeTruthy();
    expect(within(panel).getByText(`Version ${__APP_VERSION__}`)).toBeTruthy();
  });

  it("search lists results and opens one", async () => {
    const user = userEvent.setup();
    const search = vi.fn(async () => [
      { chapterId: "16-pathogen-testing", title: "Pathogen Testing", heading: "Reading the result", excerpt: "pathogen result" },
      { chapterId: "17-review", title: "Review", heading: "Review", excerpt: "review a pathogen" }
    ]);
    renderHelp(makeServices({ search }));
    const panel = await openFromMenu(user);

    await user.type(within(panel).getByRole("searchbox", { name: "Search the guide" }), "pathogen");
    const list = await within(panel).findByRole("list", { name: "Search results" });
    expect(within(list).getAllByRole("listitem")).toHaveLength(2);
    await waitFor(() => expect(within(panel).getByRole("status").textContent).toContain("2 results"));

    await user.click(within(list).getByRole("button", { name: /Pathogen Testing/ }));
    expect(await within(panel).findByRole("heading", { name: "1. Chapter 16-pathogen-testing" })).toBeTruthy();
    const target = within(panel).getByRole("heading", { name: "Reading the result" });
    await waitFor(() => expect(scrolled).toContain(target));
  });

  it("load failure shows Retry and the full-guide link", async () => {
    const user = userEvent.setup();
    let calls = 0;
    const loadChapter = vi.fn(async (id: string) => {
      calls += 1;
      if (calls === 1) throw new Error("chunk failed");
      return chapterText(id);
    });
    renderHelp(makeServices({ loadChapter }));
    const panel = await openFromMenu(user);

    expect(await within(panel).findByRole("alert")).toBeTruthy();
    const full = within(panel).getByRole("link", { name: "Open the full guide" });
    expect(full.getAttribute("href")).toBe("/help/12-sample-receiving");

    await user.click(within(panel).getByRole("button", { name: "Retry" }));
    expect(await within(panel).findByRole("heading", { name: "1. Chapter 12-sample-receiving" })).toBeTruthy();
    expect(within(panel).queryByRole("alert")).toBeNull();
  });

  it("loading skeleton; reduced-motion: no slide animation", async () => {
    const user = userEvent.setup();
    let release: (text: string) => void = () => {};
    const loadChapter = vi.fn(() => new Promise<string>((resolve) => { release = resolve; }));
    renderHelp(makeServices({ loadChapter }));
    const panel = await openFromMenu(user);

    expect(within(panel).getByRole("status", { name: "Loading chapter" })).toBeTruthy();
    expect(panel.getAttribute("data-motion")).toBe("slide");
    release(chapterText("12-sample-receiving"));
    expect(await within(panel).findByRole("heading", { name: "1. Chapter 12-sample-receiving" })).toBeTruthy();
    expect(within(panel).queryByRole("status", { name: "Loading chapter" })).toBeNull();

    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("region", { name: "Help" })).toBeNull());

    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: query.includes("prefers-reduced-motion"),
      media: query,
      onchange: null,
      addEventListener: () => {},
      removeEventListener: () => {},
      addListener: () => {},
      removeListener: () => {},
      dispatchEvent: () => false
    }));
    const reopened = await openFromMenu(user);
    expect(reopened.getAttribute("data-motion")).toBe("none");
  });
});
