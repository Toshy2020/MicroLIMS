import { describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { HelpPage } from "./HelpPage";

function renderPage(path: string, loadChapter?: (id: string) => Promise<string>) {
  return render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/help" element={<HelpPage loadChapter={loadChapter} />} />
          <Route path="/help/:chapterId" element={<HelpPage loadChapter={loadChapter} />} />
        </Routes>
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("HelpPage", () => {
  it("HelpPage lists 44 chapters and renders one by URL", async () => {
    const user = userEvent.setup();
    renderPage("/help");

    const list = screen.getByRole("list", { name: "Guide chapters" });
    expect(within(list).getAllByRole("link")).toHaveLength(44);

    await user.click(within(list).getByRole("link", { name: /Sample Receiving/ }));
    expect(await screen.findByRole("heading", { level: 2, name: /Sample Receiving/ })).toBeTruthy();
    expect(screen.getByRole("link", { name: "All chapters" }).getAttribute("href")).toBe("/help");
    expect(screen.queryByRole("list", { name: "Guide chapters" })).toBeNull();
  });

  it("renders a chapter straight from its URL", async () => {
    renderPage("/help/16-pathogen-testing");
    expect(await screen.findByRole("heading", { level: 2, name: /Pathogen Testing/ })).toBeTruthy();
  });

  it("chapter links inside a chapter navigate within the guide", async () => {
    const user = userEvent.setup();
    const loadChapter = vi.fn(async (id: string) =>
      id === "12-sample-receiving"
        ? "## 12. Receiving\n\nSee [the review chapter](17-review.md#approving)."
        : "## 17. Review\n\n### Approving\n\nText."
    );
    renderPage("/help/12-sample-receiving", loadChapter);

    await user.click(await screen.findByRole("link", { name: "the review chapter" }));
    expect(await screen.findByRole("heading", { level: 2, name: "17. Review" })).toBeTruthy();
    expect(loadChapter).toHaveBeenCalledWith("17-review");
  });

  it("unknown chapter id shows not-found with a link back to the list", async () => {
    renderPage("/help/99-nothing");
    expect(await screen.findByRole("heading", { name: "Chapter not found" })).toBeTruthy();
    expect(screen.getByRole("link", { name: "Back to all chapters" }).getAttribute("href")).toBe("/help");
  });

  it("a failed chapter load shows Retry", async () => {
    const user = userEvent.setup();
    let calls = 0;
    const loadChapter = vi.fn(async () => {
      calls += 1;
      if (calls === 1) throw new Error("boom");
      return "## 12. Receiving\n\nBody.";
    });
    renderPage("/help/12-sample-receiving", loadChapter);
    await user.click(await screen.findByRole("button", { name: "Retry" }));
    expect(await screen.findByRole("heading", { level: 2, name: "12. Receiving" })).toBeTruthy();
  });
});
