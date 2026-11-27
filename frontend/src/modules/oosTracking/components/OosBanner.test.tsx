import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { OosBanner } from "./OosBanner";
import type { OosInvestigationListItemDto } from "../../../services/oosInvestigationService";

afterEach(cleanup);

const openInvestigation: OosInvestigationListItemDto = {
  id: 42,
  number: "OOS-2026-0042",
  kind: "Oos",
  status: "PhaseOne",
  sampleId: 77,
  sampleReferenceNumber: "RM0926077",
  sampleDisplayName: "Microcrystalline Cellulose",
  batchNumber: "B-1",
  sectionId: 1,
  sectionName: "Microbiology",
  openedAtUtc: "2026-11-20T10:00:00Z",
  openedByName: "Reviewer One",
  phaseOneDueDate: null,
  dueDate: null,
  isOverdue: false,
  conclusion: null,
  concludedAtUtc: null,
  rootCauseCategoryName: null,
  testCodes: ["TAMC"],
  version: 1
};

describe("OosBanner", () => {
  it("renders a link to each open investigation and shows the block reason", () => {
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <OosBanner
            investigations={[openInvestigation]}
            blockReason="OOS investigation OOS-2026-0042 is open."
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const link = screen.getByRole("link", { name: /OOS-2026-0042/i });
    expect(link.getAttribute("href")).toBe("/oos-tracking/42");
    expect(screen.getByText("OOS investigation OOS-2026-0042 is open.")).toBeTruthy();
  });

  it("offers Open investigation when blocked without an investigation and surfaces server refusal", async () => {
    const user = userEvent.setup();
    const onOpen = vi.fn();

    const { rerender } = render(
      <PinnedLightTheme>
        <MemoryRouter>
          <OosBanner
            investigations={[]}
            blockReason="This sample has an out-of-specification result without an investigation."
            canOpenManually
            onOpenInvestigation={onOpen}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    await user.click(screen.getByRole("button", { name: /Open investigation/i }));
    expect(onOpen).toHaveBeenCalledTimes(1);

    rerender(
      <PinnedLightTheme>
        <MemoryRouter>
          <OosBanner
            investigations={[]}
            blockReason="This sample has an out-of-specification result without an investigation."
            canOpenManually
            onOpenInvestigation={onOpen}
            openError="Manual opening requires a reviewed failing section without an investigation."
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(
      screen.getByText("Manual opening requires a reviewed failing section without an investigation.")
    ).toBeTruthy();
  });
});
