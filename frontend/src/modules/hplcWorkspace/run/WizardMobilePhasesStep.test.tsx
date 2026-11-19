import { useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { MemoryRouter } from "react-router-dom";
import { lightTheme } from "../../../theme";
import {
  labelService,
  type ScanMatch,
  type ScanResolveResponse
} from "../../../services/labelService";
import type { HplcMethodResponse } from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";
import type { SolutionPreparationListItem } from "../../solutionPreparation/types";
import { WizardMobilePhasesStep } from "./WizardMobilePhasesStep";

vi.mock("../../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

const sampleMethod: HplcMethodResponse = {
  id: 5,
  sectionId: 1,
  name: "Paracetamol Assay",
  abbreviation: "PARA",
  effectiveDate: "2026-01-01",
  isActive: true,
  columnDesignation: "C18",
  columnLengthMm: 150,
  columnInternalDiameterMm: 4.6,
  elutionMode: "Isocratic",
  flowRateMlPerMin: 1.0,
  detectorType: "UV",
  injectionVolumeUl: 20,
  runTimeMin: 10,
  diluentSolutionId: 1,
  mobilePhases: [
    {
      id: 1,
      channel: "A",
      solutionMasterId: 10,
      solutionMasterName: "Methanol 80%",
      ratioPercent: 100
    }
  ],
  gradientSteps: [],
  analytes: []
};

const offeredPreparations: Record<string, SolutionPreparationListItem[]> = {
  A: [
    {
      id: 201,
      code: "MP-A 2026-001",
      type: "MobilePhase",
      solutionMasterName: "Methanol 80%",
      hplcMethodAbbreviation: "PARA",
      effectiveStatus: "Prepared",
      preparedAt: "2026-11-10T08:00:00Z",
      expiresAt: "2026-12-10T08:00:00Z",
      preparedByUserName: "Analyst One",
      approvalStatus: "Approved"
    },
    {
      id: 202,
      code: "MP-A 2026-002",
      type: "MobilePhase",
      solutionMasterName: "Methanol 80%",
      hplcMethodAbbreviation: "PARA",
      effectiveStatus: "Prepared",
      preparedAt: "2026-11-11T08:00:00Z",
      expiresAt: "2026-12-11T08:00:00Z",
      preparedByUserName: "Analyst Two",
      approvalStatus: "Approved"
    }
  ]
};

function makeResolveResponse(match: Partial<ScanMatch>): ScanResolveResponse {
  return {
    outcome: "Found",
    layoutCorrected: false,
    normalizedValue: match.code ?? "",
    match: {
      entityType: "SolutionPreparation",
      id: 202,
      code: "MP-A 2026-002",
      displayName: "Methanol 80%",
      status: "Prepared",
      usable: true,
      blockedReason: null,
      navigationTarget: "/solutions/202",
      linkedEquipmentId: null,
      ...match
    },
    candidates: [],
    hint: null
  };
}

function renderMobilePhasesStep(initialSelected: Record<string, number> = {}) {
  const onSelectSpy = vi.fn();

  function Wrapper() {
    const [selected, setSelected] = useState<Record<string, number>>(initialSelected);
    return (
      <WizardMobilePhasesStep
        method={sampleMethod}
        availablePreparations={offeredPreparations}
        selectedMobilePhases={selected}
        loadingPreps={false}
        onSelectPreparation={(channel, prepId) => {
          onSelectSpy(channel, prepId);
          setSelected((prev) => ({ ...prev, [channel]: prepId }));
        }}
        onRefreshPreparations={vi.fn()}
      />
    );
  }

  render(
    <ThemeProvider theme={lightTheme}>
      <MemoryRouter>
        <Wrapper />
      </MemoryRouter>
    </ThemeProvider>
  );

  return { onSelectSpy };
}

describe("WizardMobilePhasesStep", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it("scanning an offered mobile phase preparation selects it", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({ id: 202, code: "MP-A 2026-002" })
    );

    const { onSelectSpy } = renderMobilePhasesStep();
    await user.type(screen.getByLabelText("Scan mobile phase (Channel A)"), "SP:MP-A 2026-002{Enter}");

    await waitFor(() => {
      expect(onSelectSpy).toHaveBeenCalledWith("A", 202);
    });
    const radio = screen.getByRole("radio", { name: /MP-A 2026-002/ }) as HTMLInputElement;
    expect(radio.checked).toBe(true);
    expect(screen.getByRole("status").textContent).toContain("Selected MP-A 2026-002");
  });

  it("scanning a mobile phase preparation that is not offered leaves the selection unchanged", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({ id: 999, code: "MP-A 2026-999" })
    );

    const { onSelectSpy } = renderMobilePhasesStep({ A: 201 });
    await user.type(screen.getByLabelText("Scan mobile phase (Channel A)"), "SP:MP-A 2026-999{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("MP-A 2026-999 is not available for this step.");
    expect(onSelectSpy).not.toHaveBeenCalled();

    const firstRadio = screen.getByRole("radio", { name: /MP-A 2026-001/ }) as HTMLInputElement;
    const secondRadio = screen.getByRole("radio", { name: /MP-A 2026-002/ }) as HTMLInputElement;
    expect(firstRadio.checked).toBe(true);
    expect(secondRadio.checked).toBe(false);
  });
});
