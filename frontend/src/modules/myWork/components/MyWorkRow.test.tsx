import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { monospaceFontFamily } from "../../../theme/palette";
import type { MyWorkItem } from "../myWorkTypes";
import { MyWorkRow } from "./MyWorkRow";

afterEach(cleanup);

function makeItem(over: Partial<MyWorkItem> = {}): MyWorkItem {
  return {
    key: "test-10",
    groupKey: "toPerform",
    identifier: "SMP-2026-0042",
    title: "Total Aerobic Microbial Count",
    context: "Water for Injection · Microbiology",
    dueAtUtc: "2026-11-14T12:30:00Z",
    urgency: "Now",
    locked: false,
    target: { type: "test-step", sampleId: 42, testOrderId: 10 },
    actions: [],
    sectionName: "Microbiology",
    lab: "MICRO",
    ...over
  };
}

function renderRow(item: MyWorkItem, handlers?: { onTake?: (i: MyWorkItem) => void; onRelease?: (i: MyWorkItem) => void }) {
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <MyWorkRow
          item={item}
          timeZoneId="Africa/Cairo"
          onTake={handlers?.onTake ?? vi.fn()}
          onRelease={handlers?.onRelease ?? vi.fn()}
        />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("MyWorkRow", () => {
  it("renders a link to routeForTarget, monospace identifier, and due time with time zone", () => {
    renderRow(makeItem());

    const link = screen.getByRole("link", { name: /SMP-2026-0042/i });
    expect(link.getAttribute("href")).toBe("/microbiology/workspace?sampleId=42&testOrderId=10&open=step");

    const idEl = screen.getByText("SMP-2026-0042");
    const fontFamily = getComputedStyle(idEl).fontFamily;
    const firstFont = monospaceFontFamily.split(",")[0].trim().replace(/^['"]|['"]$/g, "");
    expect(fontFamily).toContain(firstFont);

    // Due time includes time zone abbreviation (e.g., EET / GMT+2)
    const dueEl = screen.getByTestId("my-work-due-time");
    expect(dueEl.textContent).toMatch(/EET|GMT|UTC/i);
  });

  it("renders a locked row as plain text (not a link) with lock reason and aria-disabled", () => {
    renderRow(
      makeItem({
        locked: true,
        lockReason: "Segregation of duties: you recorded a test on this sample."
      })
    );

    expect(screen.queryByRole("link")).toBeNull();
    const row = screen.getByTestId("my-work-row-test-10");
    expect(row.getAttribute("aria-disabled")).toBe("true");
    expect(screen.getByText("Segregation of duties: you recorded a test on this sample.")).toBeTruthy();
  });

  it("renders plain text with 'Open from the workspace' when routeForTarget returns null", () => {
    renderRow(makeItem({ target: { type: "unknown-target" } }));

    expect(screen.queryByRole("link")).toBeNull();
    expect(screen.getByText("Open from the workspace")).toBeTruthy();
  });

  it("invokes onTake and onRelease when action buttons are clicked", async () => {
    const user = userEvent.setup();
    const onTake = vi.fn();
    const onRelease = vi.fn();

    renderRow(makeItem({ actions: ["take", "release"] }), { onTake, onRelease });

    await user.click(screen.getByRole("button", { name: /Take/i }));
    expect(onTake).toHaveBeenCalledTimes(1);

    await user.click(screen.getByRole("button", { name: /Release/i }));
    expect(onRelease).toHaveBeenCalledTimes(1);
  });
});
