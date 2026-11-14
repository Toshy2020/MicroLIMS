import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { BenchModeProvider } from "../../../theme/BenchModeContext";
import { HelpPanelProvider } from "../../help/HelpPanelContext";
import { SharedTsbEnrichmentPanel } from "./SharedTsbEnrichmentPanel";
import { hints } from "../hints";
import type { PathogenTestingSessionDto } from "../types/pathogenSessionTypes";

vi.mock("../../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getReleasedMedia: vi.fn(async () => [
      { id: 1, lotNumber: "TSB/01/26", materialName: "TSB", expiryDate: "2030-01-01T00:00:00Z" }
    ]),
    getEquipment: vi.fn(async () => [{ id: 7, code: "INC-1", name: "Incubator", location: "Lab" }])
  }
}));

vi.mock("../services/PathogenSessionService", () => ({
  PathogenSessionService: { startSharedTsb: vi.fn(), getSession: vi.fn() }
}));

const session = {
  sampleId: 1,
  totalLocations: 1,
  assignedTests: [{ testCode: "T1", displayName: "Test one", requiresTsb: true }],
  sharedTsb: {
    isStarted: false,
    isIncubating: false,
    isCompleted: false,
    windowNotConfigured: false,
    requiredTemperatureRange: "range",
    requiredDurationRange: "range"
  }
} as unknown as PathogenTestingSessionDto;

function descriptionOf(element: HTMLElement): string {
  return (element.getAttribute("aria-describedby") ?? "")
    .split(/\s+/)
    .map((id) => document.getElementById(id)?.textContent ?? "")
    .join(" ");
}

describe("SharedTsbEnrichmentPanel field hints", () => {
  it("keeps each field's helper text described alongside its hint", async () => {
    render(
      <BenchModeProvider>
        <PinnedLightTheme>
          <MemoryRouter>
            <HelpPanelProvider>
              <SharedTsbEnrichmentPanel session={session} onUpdated={() => {}} onNext={() => {}} />
            </HelpPanelProvider>
          </MemoryRouter>
        </PinnedLightTheme>
      </BenchModeProvider>
    );

    const lot = await screen.findByRole("combobox", { name: /TSB Media Lot/ });
    expect(descriptionOf(lot)).toContain("Only active, unexpired, GPT-released lots are permitted");
    expect(descriptionOf(lot)).toContain(hints.tsbMediaLot.text);

    const incubator = screen.getByRole("combobox", { name: /Incubator Equipment/ });
    expect(descriptionOf(incubator)).toContain("Select an operational, calibrated incubator");
    expect(descriptionOf(incubator)).toContain(hints.incubator.text);

    const temperature = screen.getByRole("textbox", { name: /Required Incubation Temperature/ });
    expect(descriptionOf(temperature)).toContain("Each test's own TSB medium");
    expect(descriptionOf(temperature)).toContain(hints.requiredTemperature.text);

    const time = screen.getByRole("textbox", { name: /Required Incubation Time/ });
    expect(descriptionOf(time)).toContain("Each test keeps its own duration");
    expect(descriptionOf(time)).toContain(hints.requiredTime.text);
  });
});
