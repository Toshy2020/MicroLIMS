import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { i18n } from "../../../i18n";

// The myWork texts load on first use; the English file is in place before the tests query it.
beforeAll(async () => {
  await i18n.loadNamespaces("myWork");
});
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { monospaceFontFamily } from "../../../theme/palette";
import type { MyWorkItem } from "../myWorkTypes";
import { MyWorkRow } from "./MyWorkRow";
import { actionAvailabilityService } from "../../../services/actionAvailabilityService";

// Network boundary: the server's answer about take and release.
vi.mock("../../../services/actionAvailabilityService", () => ({
  actionAvailabilityService: { get: vi.fn() }
}));

beforeEach(() => {
  vi.mocked(actionAvailabilityService.get).mockReset();
  vi.mocked(actionAvailabilityService.get).mockResolvedValue([]);
});
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

  it("a refused Take shows the server's sentence, is aria-disabled and does not call onTake", async () => {
    const sentence = "You already performed another test on this sample.";
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      { action: "test.take", allowed: false, reason: sentence },
      { action: "test.release", allowed: true, reason: null }
    ]);
    const user = userEvent.setup();
    const onTake = vi.fn();
    const onRelease = vi.fn();
    renderRow(makeItem({ actions: ["take", "release"] }), { onTake, onRelease });

    const reason = await screen.findByText(sentence);
    expect(actionAvailabilityService.get).toHaveBeenCalledWith("test-order", 10, undefined, expect.any(AbortSignal));
    const take = screen.getByRole("button", { name: "Take" });
    expect(take.getAttribute("aria-disabled")).toBe("true");
    expect(document.getElementById(take.getAttribute("aria-describedby") ?? "")?.contains(reason)).toBe(true);
    await user.click(take);
    expect(onTake).not.toHaveBeenCalled();

    // Release was allowed: it still works.
    await user.click(screen.getByRole("button", { name: "Release" }));
    expect(onRelease).toHaveBeenCalledTimes(1);
  });

  it("asks nothing for a row that offers neither action", async () => {
    renderRow(makeItem({ actions: [] }));
    await Promise.resolve();
    expect(actionAvailabilityService.get).not.toHaveBeenCalled();
  });
});
