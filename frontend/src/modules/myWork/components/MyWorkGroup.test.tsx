import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { i18n } from "../../../i18n";

// The myWork texts load on first use; the English file is in place before the tests query it.
beforeAll(async () => {
  await i18n.loadNamespaces("myWork");
});
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { MyWorkGroup as MyWorkGroupDto } from "../myWorkTypes";
import { MyWorkGroup } from "./MyWorkGroup";

afterEach(cleanup);

function makeGroup(over: Partial<MyWorkGroupDto> = {}): MyWorkGroupDto {
  return {
    key: "review",
    title: "Awaiting my review",
    count: 2,
    lockedCount: 0,
    hasMore: false,
    items: [
      {
        key: "rev-1",
        groupKey: "review",
        identifier: "SMP-101",
        title: "Sample SMP-101",
        urgency: "Today",
        locked: false,
        target: { type: "sample-review", sampleId: 101 },
        actions: ["review"],
        lab: "MICRO"
      }
    ],
    ...over
  };
}

describe("MyWorkGroup", () => {
  it("renders heading and count", () => {
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <MyWorkGroup group={makeGroup()} timeZoneId="UTC" onTake={vi.fn()} onRelease={vi.fn()} />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(screen.getByRole("heading", { name: /Awaiting my review/i })).toBeTruthy();
    expect(screen.getByText("2")).toBeTruthy();
  });

  it("omits empty groups", () => {
    const { container } = render(
      <PinnedLightTheme>
        <MemoryRouter>
          <MyWorkGroup group={makeGroup({ count: 0, items: [] })} timeZoneId="UTC" onTake={vi.fn()} onRelease={vi.fn()} />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(container.textContent).toBe("");
  });

  it("renders 'Show all {n}' link to filtered workspace list when hasMore is true", () => {
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <MyWorkGroup
            group={makeGroup({ count: 75, hasMore: true })}
            timeZoneId="UTC"
            onTake={vi.fn()}
            onRelease={vi.fn()}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const link = screen.getByRole("link", { name: /Show all 75/i });
    expect(link.getAttribute("href")).toContain("status=UnderReview");
  });
});
