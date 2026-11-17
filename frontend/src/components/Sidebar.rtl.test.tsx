import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { renderInArabic, resetLanguage } from "../i18n/testUtils";
import { getGroupedMenu } from "../routes/menuConfig";
import { myWorkService } from "../modules/myWork/services/myWorkService";
import { Sidebar } from "./Sidebar";

vi.mock("../modules/myWork/services/myWorkService", () => ({
  myWorkService: { get: vi.fn(), summary: vi.fn(), take: vi.fn(), release: vi.fn() }
}));

function phoneWidth() {
  vi.stubGlobal(
    "matchMedia",
    vi.fn().mockImplementation((query: string) => ({
      matches: true,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn()
    }))
  );
}

function sidebar(props: { mobileOpen?: boolean; collapsed?: boolean } = {}) {
  const groups = getGroupedMenu({ role: "Analyst", permissions: [], labCodes: [] });
  return (
    <MemoryRouter initialEntries={["/my-work"]}>
      <Sidebar
        mobileOpen={props.mobileOpen ?? false}
        onMobileClose={() => {}}
        collapsed={props.collapsed ?? false}
        onToggleCollapse={() => {}}
        groups={groups}
        activeTrail={null}
      />
    </MemoryRouter>
  );
}

beforeEach(() => {
  vi.mocked(myWorkService.summary).mockResolvedValue({ total: 3, overdue: 0, now: 0, complete: true });
});

afterEach(async () => {
  vi.unstubAllGlobals();
  await resetLanguage();
});

describe("Sidebar in Arabic", () => {
  it("Drawer_opens_from_right_in_arabic", async () => {
    phoneWidth();
    await renderInArabic(sidebar({ mobileOpen: true }), ["common", "navigation"]);

    const paper = document.querySelector(".MuiDrawer-paper") as HTMLElement;
    expect(paper).toBeTruthy();
    // The drawer's left-anchored paper is mirrored by the right-to-left style cache: pinned to the right edge.
    expect(getComputedStyle(paper).right).toBe("0px");
    expect(getComputedStyle(paper).left).not.toBe("0px");
  });

  it("opens from the left in English, as before", async () => {
    phoneWidth();
    render(<PinnedLightTheme>{sidebar({ mobileOpen: true })}</PinnedLightTheme>);
    const paper = document.querySelector(".MuiDrawer-paper") as HTMLElement;
    expect(getComputedStyle(paper).left).toBe("0px");
  });

  it("shows translated group and page names and the translated collapse control", async () => {
    await renderInArabic(sidebar(), ["common", "navigation"]);
    const nav = await screen.findByRole("navigation", { name: "التنقل الرئيسي" });
    expect(await within(nav).findByText("نظرة عامة")).toBeTruthy();
    expect(within(nav).getByRole("link", { name: /^عملي/ })).toBeTruthy();
    expect(within(nav).getByRole("button", { name: "طي الشريط الجانبي" })).toBeTruthy();
  });

  it("names the My Work link with its Arabic task count", async () => {
    await renderInArabic(sidebar(), ["common", "navigation"]);
    const link = await screen.findByRole("link", { name: "عملي، 3 مهام" });
    expect(link).toBeTruthy();
  });

  it("opens a rail flyout with ArrowLeft in Arabic, not ArrowRight", async () => {
    const user = userEvent.setup();
    const groups = getGroupedMenu({ role: "Analyst", permissions: ["Documents.Config.Manage"], labCodes: [] });
    await renderInArabic(
      <MemoryRouter initialEntries={["/my-work"]}>
        <Sidebar mobileOpen={false} onMobileClose={() => {}} collapsed onToggleCollapse={() => {}} groups={groups} activeTrail={null} />
      </MemoryRouter>,
      ["common", "navigation"]
    );
    const parent = await screen.findByRole("link", { name: "التحكم في المستندات" });
    parent.focus();
    await user.keyboard("{ArrowRight}");
    expect(parent.getAttribute("aria-expanded")).toBe("false");
    await user.keyboard("{ArrowLeft}");
    expect(parent.getAttribute("aria-expanded")).toBe("true");
  });
});
