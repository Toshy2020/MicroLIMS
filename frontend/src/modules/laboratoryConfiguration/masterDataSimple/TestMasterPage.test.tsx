import { describe, expect, it, vi, afterEach, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, cleanup, within } from "@testing-library/react";
import { TestMasterPage } from "./TestMasterPage";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import * as laboratorySectionService from "../../../services/laboratorySectionService";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";

const mockTestDefinitions: TestDefinitionOption[] = [
  {
    id: 1,
    code: "QUAL-01",
    displayName: "Appearance Test",
    sectionId: 1,
    workflowType: "Qualitative",
    isActive: true,
    perContainer: true,
    moistureRole: "None",
    physchemArea: "FinishedProduct"
  },
  {
    id: 2,
    code: "GRAV-01",
    displayName: "Loss on Drying Test",
    sectionId: 1,
    workflowType: "Gravimetric",
    equationType: "GravimetricLoss",
    isActive: true,
    replicateCount: 2,
    moistureRole: "LossOnDrying",
    physchemArea: "FinishedProduct"
  },
  {
    id: 3,
    code: "TIT-KF",
    displayName: "Water Determination KF",
    sectionId: 1,
    workflowType: "Titration",
    titrationType: "KarlFischer",
    titrationMode: "Direct",
    titrationCalculation: "UspFactor",
    titrationEndpoint: "Visual",
    replicateCount: 3,
    isActive: true,
    moistureRole: "None",
    physchemArea: "FinishedProduct"
  }
];

const mockAddNew = vi.fn();
const mockUpdate = vi.fn();

vi.mock("../../../hooks/useTestDefinitions", () => ({
  useTestDefinitions: () => ({
    options: mockTestDefinitions,
    addNew: mockAddNew,
    update: mockUpdate,
    setActive: vi.fn(),
    reload: vi.fn()
  })
}));

vi.mock("../../../services/laboratorySectionService", () => ({
  getSections: vi.fn(),
  getMySections: vi.fn()
}));

vi.mock("./services/HplcMethodService", () => ({
  HplcMethodService: { getAll: vi.fn().mockResolvedValue([]) }
}));

vi.mock("./services/IcpMethodService", () => ({
  IcpMethodService: { getAll: vi.fn().mockResolvedValue([]) }
}));

vi.mock("./services/SolutionMasterService", () => ({
  SolutionMasterService: { getAll: vi.fn().mockResolvedValue([]) }
}));

vi.mock("./services/MaterialMasterService", () => ({
  MaterialMasterService: { getAll: vi.fn().mockResolvedValue([]) }
}));

afterEach(cleanup);

describe("TestMasterPage flag visibility and interaction", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
      { sectionId: 1, sectionCode: "FP", sectionName: "Finished Product" }
    ] as never);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      { sectionId: 1, sectionCode: "FP", sectionName: "Finished Product" }
    ] as never);
  });

  it("shows 'Recorded per container' only for Qualitative and 'Moisture role' only for GravimetricLoss / KF Titration", async () => {
    render(
      <PinnedLightTheme>
        <TestMasterPage lab="fp" area="fp" />
      </PinnedLightTheme>
    );

    // Wait for sections to load
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Add Test/i })).toBeTruthy();
    });

    // Open Add Test dialog
    fireEvent.click(screen.getByRole("button", { name: /Add Test/i }));

    const dialog = screen.getByRole("dialog");
    expect(dialog).toBeTruthy();

    // 1. Switch to Qualitative workflow
    const wfSelect = within(dialog).getByLabelText("Workflow Type");
    fireEvent.mouseDown(wfSelect);
    const qualOption = await screen.findByRole("option", { name: /Qualitative/i });
    fireEvent.click(qualOption);

    // "Recorded per container" switch must be visible
    expect(within(dialog).getByLabelText(/Recorded per container/i)).toBeTruthy();
    // Moisture role select must NOT be visible
    expect(within(dialog).queryByLabelText(/^Moisture role/i)).toBeNull();

    // 2. Switch to Gravimetric workflow (defaults equationType to GravimetricLoss)
    fireEvent.mouseDown(within(dialog).getByLabelText("Workflow Type"));
    const gravOption = await screen.findByRole("option", { name: /Gravimetric/i });
    fireEvent.click(gravOption);

    // "Recorded per container" switch must be gone
    expect(within(dialog).queryByLabelText(/Recorded per container/i)).toBeNull();
    // "Moisture role" select must be visible
    expect(within(dialog).getByLabelText(/^Moisture role/i)).toBeTruthy();

    // 3. Change Equation Type to GravimetricResidue -> Moisture role must NOT be visible
    const eqSelect = within(dialog).getByLabelText("Equation Type");
    fireEvent.mouseDown(eqSelect);
    const residueOption = await screen.findByRole("option", { name: /Ash \/ Gravimetric residue/i });
    fireEvent.click(residueOption);

    expect(within(dialog).queryByLabelText(/^Moisture role/i)).toBeNull();

    // 4. Switch to Titration workflow
    fireEvent.mouseDown(within(dialog).getByLabelText("Workflow Type"));
    const titOption = await screen.findByRole("option", { name: /Titration/i });
    fireEvent.click(titOption);

    // Titration type defaults to AcidBase -> Moisture role must NOT be visible
    expect(within(dialog).queryByLabelText(/^Moisture role/i)).toBeNull();

    // Change Titration type to KarlFischer
    const titTypeSelect = within(dialog).getByLabelText(/Titration type/i);
    fireEvent.mouseDown(titTypeSelect);
    const kfOption = await screen.findByRole("option", { name: /Karl Fischer/i });
    fireEvent.click(kfOption);

    // Moisture role select must now be visible in KF titration section
    expect(within(dialog).getByLabelText(/^Moisture role/i)).toBeTruthy();
  });

  it("changing moistureRole on an existing titration test displays the reason for change field", async () => {
    render(
      <PinnedLightTheme>
        <TestMasterPage lab="fp" area="fp" />
      </PinnedLightTheme>
    );

    // Wait for table to load
    await waitFor(() => {
      expect(screen.getByText("Water Determination KF")).toBeTruthy();
    });

    // Open row actions menu for TIT-KF (id: 3, 3rd row)
    const actionButtons = screen.getAllByRole("button", { name: /Row actions/i });
    fireEvent.click(actionButtons[2]);

    const editMenuItem = await screen.findByRole("menuitem", { name: "Edit" });
    fireEvent.click(editMenuItem);

    const dialog = screen.getByRole("dialog");
    expect(dialog).toBeTruthy();

    // Initially no change reason field is visible
    expect(within(dialog).queryByLabelText(/Reason for Change/i)).toBeNull();

    // Change moisture role from None to Water
    const moistureSelect = within(dialog).getByLabelText(/^Moisture role/i);
    fireEvent.mouseDown(moistureSelect);
    const waterOption = await screen.findByRole("option", { name: "Water" });
    fireEvent.click(waterOption);

    // Reason for Change field must now appear because moistureRole changed on existing titration test
    expect(within(dialog).getByLabelText(/Reason for Change/i)).toBeTruthy();
  });

  it("resets perContainer when leaving Qualitative and resets moistureRole when changing to residue", async () => {
    render(
      <PinnedLightTheme>
        <TestMasterPage lab="fp" area="fp" />
      </PinnedLightTheme>
    );

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Add Test/i })).toBeTruthy();
    });

    fireEvent.click(screen.getByRole("button", { name: /Add Test/i }));
    const dialog = screen.getByRole("dialog");

    // 1. Switch to Qualitative and turn on perContainer
    const wfSelect = within(dialog).getByLabelText("Workflow Type");
    fireEvent.mouseDown(wfSelect);
    fireEvent.click(await screen.findByRole("option", { name: /Qualitative/i }));

    const perContainerSwitch = within(dialog).getByRole("switch", { name: /Recorded per container/i });
    expect((perContainerSwitch as HTMLInputElement).checked).toBe(false);
    fireEvent.click(perContainerSwitch);
    expect((perContainerSwitch as HTMLInputElement).checked).toBe(true);

    // 2. Switch away from Qualitative to Gravimetric -> switches back to Qualitative must show perContainer was reset to false
    fireEvent.mouseDown(within(dialog).getByLabelText("Workflow Type"));
    fireEvent.click(await screen.findByRole("option", { name: /Gravimetric/i }));

    // In Gravimetric, set moistureRole to Loss on drying
    const moistureSelect = within(dialog).getByLabelText(/^Moisture role/i);
    fireEvent.mouseDown(moistureSelect);
    fireEvent.click(await screen.findByRole("option", { name: /Loss on drying/i }));

    // Change equation to Residue -> moistureRole resets to None
    const eqSelect = within(dialog).getByLabelText("Equation Type");
    fireEvent.mouseDown(eqSelect);
    fireEvent.click(await screen.findByRole("option", { name: /Ash \/ Gravimetric residue/i }));

    // Change back to Loss on drying equation
    fireEvent.mouseDown(within(dialog).getByLabelText("Equation Type"));
    fireEvent.click(await screen.findByRole("option", { name: /Loss on drying \/ Gravimetric loss/i }));

    // Moisture role select reappears and its value was reset to None
    const recheckedMoisture = within(dialog).getByLabelText(/^Moisture role/i);
    expect(recheckedMoisture.textContent).toContain("None");

    // Switch back to Qualitative -> perContainer should be false
    fireEvent.mouseDown(within(dialog).getByLabelText("Workflow Type"));
    fireEvent.click(await screen.findByRole("option", { name: /Qualitative/i }));
    const recheckedSwitch = within(dialog).getByRole("switch", { name: /Recorded per container/i });
    expect((recheckedSwitch as HTMLInputElement).checked).toBe(false);
  });
});
