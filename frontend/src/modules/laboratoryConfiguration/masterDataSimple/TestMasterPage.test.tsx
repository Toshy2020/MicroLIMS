import { describe, expect, it, vi, afterEach, beforeEach } from "vitest";
import { render, screen, waitFor, cleanup, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { TestMasterPage } from "./TestMasterPage";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import * as laboratorySectionService from "../../../services/laboratorySectionService";
import { masterDataOptions } from "../../../services/masterDataOptions";
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

// Only the HTTP service layer is mocked; the real useTestDefinitions hook,
// page and dialog run (CLAUDE.md Rule 4.5).
vi.mock("../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/masterDataOptions")>();
  return {
    ...actual,
    masterDataOptions: {
      ...actual.masterDataOptions,
      getTestDefinitions: vi.fn(),
      createTestDefinition: vi.fn(),
      updateTestDefinition: vi.fn(),
      getTestWorkflowSteps: vi.fn().mockResolvedValue([]),
      getOrganisms: vi.fn().mockResolvedValue([]),
      getMediaIncubationConditions: vi.fn().mockResolvedValue([]),
      getMediaProducts: vi.fn().mockResolvedValue([])
    }
  };
});

vi.mock("../../../services/laboratorySectionService", () => ({
  getSections: vi.fn(),
  getMySections: vi.fn()
}));

vi.mock("./services/HplcMethodService", () => ({
  HplcMethodService: { getAll: vi.fn().mockResolvedValue([]) }
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
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue(mockTestDefinitions);
  });

  it("shows 'Recorded per container' only for Qualitative and 'Moisture role' only for GravimetricLoss / KF Titration", async () => {
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <TestMasterPage lab="fp" area="fp" />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    // Wait for sections to load
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Add Test/i })).toBeTruthy();
    });

    // Open Add Test dialog
    await user.click(screen.getByRole("button", { name: /Add Test/i }));

    const dialog = screen.getByRole("dialog");
    expect(dialog).toBeTruthy();

    // 1. Switch to Qualitative workflow
    const wfSelect = within(dialog).getByLabelText("Workflow Type");
    await user.click(wfSelect);
    const qualOption = await screen.findByRole("option", { name: /Qualitative/i });
    await user.click(qualOption);

    // "Recorded per container" switch must be visible
    expect(within(dialog).getByLabelText(/Recorded per container/i)).toBeTruthy();
    // Moisture role select must NOT be visible
    expect(within(dialog).queryByLabelText(/^Moisture role/i)).toBeNull();

    // 2. Switch to Gravimetric workflow (defaults equationType to GravimetricLoss)
    await user.click(within(dialog).getByLabelText("Workflow Type"));
    const gravOption = await screen.findByRole("option", { name: /Gravimetric/i });
    await user.click(gravOption);

    // "Recorded per container" switch must be gone
    expect(within(dialog).queryByLabelText(/Recorded per container/i)).toBeNull();
    // "Moisture role" select must be visible
    expect(within(dialog).getByLabelText(/^Moisture role/i)).toBeTruthy();

    // 3. Change Equation Type to GravimetricResidue -> Moisture role must NOT be visible
    const eqSelect = within(dialog).getByLabelText("Equation Type");
    await user.click(eqSelect);
    const residueOption = await screen.findByRole("option", { name: /Ash \/ Gravimetric residue/i });
    await user.click(residueOption);

    expect(within(dialog).queryByLabelText(/^Moisture role/i)).toBeNull();

    // 4. Switch to Titration workflow
    await user.click(within(dialog).getByLabelText("Workflow Type"));
    const titOption = await screen.findByRole("option", { name: /Titration/i });
    await user.click(titOption);

    // Titration type defaults to AcidBase -> Moisture role must NOT be visible
    expect(within(dialog).queryByLabelText(/^Moisture role/i)).toBeNull();

    // Change Titration type to KarlFischer
    const titTypeSelect = within(dialog).getByLabelText(/Titration type/i);
    await user.click(titTypeSelect);
    const kfOption = await screen.findByRole("option", { name: /Karl Fischer/i });
    await user.click(kfOption);

    // Moisture role select must now be visible in KF titration section
    expect(within(dialog).getByLabelText(/^Moisture role/i)).toBeTruthy();
  });

  it("changing moistureRole on an existing titration test displays the reason for change field", async () => {
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <TestMasterPage lab="fp" area="fp" />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    // Wait for table to load
    await waitFor(() => {
      expect(screen.getByText("Water Determination KF")).toBeTruthy();
    });

    // Open row actions menu for TIT-KF (id: 3, 3rd row)
    const actionButtons = screen.getAllByRole("button", { name: /Row actions/i });
    await user.click(actionButtons[2]);

    const editMenuItem = await screen.findByRole("menuitem", { name: "Edit" });
    await user.click(editMenuItem);

    const dialog = screen.getByRole("dialog");
    expect(dialog).toBeTruthy();

    // Initially no change reason field is visible
    expect(within(dialog).queryByLabelText(/Reason for Change/i)).toBeNull();

    // Change moisture role from None to Water
    const moistureSelect = within(dialog).getByLabelText(/^Moisture role/i);
    await user.click(moistureSelect);
    const waterOption = await screen.findByRole("option", { name: "Water" });
    await user.click(waterOption);

    // Reason for Change field must now appear because moistureRole changed on existing titration test
    expect(within(dialog).getByLabelText(/Reason for Change/i)).toBeTruthy();
  });

  it("resets perContainer when leaving Qualitative and resets moistureRole when changing to residue", async () => {
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <TestMasterPage lab="fp" area="fp" />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Add Test/i })).toBeTruthy();
    });

    await user.click(screen.getByRole("button", { name: /Add Test/i }));
    const dialog = screen.getByRole("dialog");

    // 1. Switch to Qualitative and turn on perContainer
    const wfSelect = within(dialog).getByLabelText("Workflow Type");
    await user.click(wfSelect);
    await user.click(await screen.findByRole("option", { name: /Qualitative/i }));

    const perContainerSwitch = within(dialog).getByRole("switch", { name: /Recorded per container/i });
    expect((perContainerSwitch as HTMLInputElement).checked).toBe(false);
    await user.click(perContainerSwitch);
    expect((perContainerSwitch as HTMLInputElement).checked).toBe(true);

    // 2. Switch away from Qualitative to Gravimetric -> switches back to Qualitative must show perContainer was reset to false
    await user.click(within(dialog).getByLabelText("Workflow Type"));
    await user.click(await screen.findByRole("option", { name: /Gravimetric/i }));

    // In Gravimetric, set moistureRole to Loss on drying
    const moistureSelect = within(dialog).getByLabelText(/^Moisture role/i);
    await user.click(moistureSelect);
    await user.click(await screen.findByRole("option", { name: /Loss on drying/i }));

    // Change equation to Residue -> moistureRole resets to None
    const eqSelect = within(dialog).getByLabelText("Equation Type");
    await user.click(eqSelect);
    await user.click(await screen.findByRole("option", { name: /Ash \/ Gravimetric residue/i }));

    // Change back to Loss on drying equation
    await user.click(within(dialog).getByLabelText("Equation Type"));
    await user.click(await screen.findByRole("option", { name: /Loss on drying \/ Gravimetric loss/i }));

    // Moisture role select reappears and its value was reset to None
    const recheckedMoisture = within(dialog).getByLabelText(/^Moisture role/i);
    expect(recheckedMoisture.textContent).toContain("None");

    // Switch back to Qualitative -> perContainer should be false
    await user.click(within(dialog).getByLabelText("Workflow Type"));
    await user.click(await screen.findByRole("option", { name: /Qualitative/i }));
    const recheckedSwitch = within(dialog).getByRole("switch", { name: /Recorded per container/i });
    expect((recheckedSwitch as HTMLInputElement).checked).toBe(false);
  });
});

describe("TestMasterPage - saving with Enter", () => {
  const createdDefinition: TestDefinitionOption = {
    id: 10,
    code: "TAMC-01",
    displayName: "Total Aerobic Count",
    sectionId: 2,
    workflowType: "Observation",
    equationType: "None",
    isActive: true
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
      { sectionId: 1, sectionCode: "FP", sectionName: "Finished Product" },
      { sectionId: 2, sectionCode: "MICRO", sectionName: "Microbiology" }
    ] as never);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      { sectionId: 2, sectionCode: "MICRO", sectionName: "Microbiology" }
    ] as never);
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([]);
    vi.mocked(masterDataOptions.createTestDefinition).mockResolvedValue(createdDefinition as never);
  });

  async function openAddDialog(user: ReturnType<typeof userEvent.setup>) {
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <TestMasterPage lab="micro" />
        </MemoryRouter>
      </PinnedLightTheme>
    );
    await user.click(await screen.findByRole("button", { name: /Add Test/i }));
    const dialog = await screen.findByRole("dialog");
    // The single user section is auto-filled once the section lists have loaded.
    await waitFor(() => {
      expect(within(dialog).getByRole("combobox", { name: "Section" }).textContent).toContain("Microbiology");
    });
    return dialog;
  }

  it("Enter in the Display Name field adds the test", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user);

    await user.type(within(dialog).getByLabelText(/^Code/), "TAMC-01");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Total Aerobic Count{Enter}");

    await waitFor(() => {
      expect(masterDataOptions.createTestDefinition).toHaveBeenCalledTimes(1);
    });
    const payload = vi.mocked(masterDataOptions.createTestDefinition).mock.calls[0][0];
    expect(payload).toMatchObject({
      code: "TAMC-01",
      displayName: "Total Aerobic Count",
      sectionId: 2,
      workflowType: "Observation",
      equationType: "None",
      coaDisplayOrder: null
    });

    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
    });
    expect(screen.getByText('Test "TAMC-01" added to the Test Master.')).toBeTruthy();
  });

  it("Enter with a required field missing shows the reason and saves nothing", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user);

    await user.type(within(dialog).getByLabelText(/^Display Name/), "Total Aerobic Count{Enter}");

    expect(await within(dialog).findByText("Both Code and Display Name are required.")).toBeTruthy();
    expect(masterDataOptions.createTestDefinition).not.toHaveBeenCalled();
    expect(screen.getByRole("dialog")).toBeTruthy();
  });

  it("shows the server's refusal and keeps the dialog open", async () => {
    vi.mocked(masterDataOptions.createTestDefinition).mockRejectedValue({
      response: { data: { message: "A test with this code already exists." } }
    });
    const user = userEvent.setup();
    const dialog = await openAddDialog(user);

    await user.type(within(dialog).getByLabelText(/^Code/), "TAMC-01");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Total Aerobic Count{Enter}");

    expect(await within(dialog).findByText("A test with this code already exists.")).toBeTruthy();
    expect(masterDataOptions.createTestDefinition).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("dialog")).toBeTruthy();
    expect((within(dialog).getByLabelText(/^Code/) as HTMLInputElement).value).toBe("TAMC-01");
  });

  it("Enter while a save is in flight does not send a second request", async () => {
    let resolveCreate: (value: never) => void = () => {};
    vi.mocked(masterDataOptions.createTestDefinition).mockReturnValue(
      new Promise<never>((resolve) => {
        resolveCreate = resolve;
      })
    );
    const user = userEvent.setup();
    const dialog = await openAddDialog(user);

    await user.type(within(dialog).getByLabelText(/^Code/), "TAMC-01");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Total Aerobic Count{Enter}");
    await waitFor(() => {
      expect(masterDataOptions.createTestDefinition).toHaveBeenCalledTimes(1);
    });

    await user.type(within(dialog).getByLabelText(/^Display Name/), "{Enter}");
    expect(masterDataOptions.createTestDefinition).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("dialog")).toBeTruthy();

    resolveCreate(createdDefinition as never);
    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
    });
    expect(masterDataOptions.createTestDefinition).toHaveBeenCalledTimes(1);
  });
});

// Where the page puts the opened test: a probe printing the router's location is
// a test helper, not a mock.
function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location">{`${location.pathname}${location.search}`}</div>;
}

function renderInRouter(page: ReactElement, initialEntries: string[] = ["/x"]) {
  return render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={initialEntries}>
        <LocationProbe />
        {page}
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

const locationText = () => screen.getByTestId("location").textContent ?? "";

describe("TestMasterPage - workflow steps after adding, duplicate and deep link", () => {
  const microTest: TestDefinitionOption = {
    id: 20,
    code: "TAMC-01",
    displayName: "Total Aerobic Count",
    sectionId: 2,
    workflowType: "Observation",
    equationType: "None",
    isActive: true
  };
  const newMicroTest: TestDefinitionOption = { ...microTest, id: 31, code: "NEW-MICRO", displayName: "New Micro Test" };
  const gravimetric: TestDefinitionOption = {
    id: 21,
    code: "GRAV-DUP",
    displayName: "Loss on Drying (oven)",
    sectionId: 1,
    workflowType: "Gravimetric",
    equationType: "GravimetricLoss",
    isActive: true,
    replicateCount: 4,
    usesTare: true,
    conditionFields: "105 C for 3 h",
    moistureRole: "LossOnDrying",
    coaDisplayOrder: 5,
    physchemArea: "FinishedProduct"
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
      { sectionId: 1, sectionCode: "FP", sectionName: "Finished Product" },
      { sectionId: 2, sectionCode: "MICRO", sectionName: "Microbiology" }
    ] as never);
    vi.mocked(masterDataOptions.getTestWorkflowSteps).mockResolvedValue([]);
  });

  function asMicroUser() {
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      { sectionId: 2, sectionCode: "MICRO", sectionName: "Microbiology" }
    ] as never);
  }

  function asFpUser() {
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      { sectionId: 1, sectionCode: "FP", sectionName: "Finished Product" }
    ] as never);
  }

  it("opens the workflow steps of a newly added microbiology test", async () => {
    asMicroUser();
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValueOnce([]).mockResolvedValue([newMicroTest]);
    vi.mocked(masterDataOptions.createTestDefinition).mockResolvedValue(newMicroTest as never);
    const user = userEvent.setup();
    renderInRouter(<TestMasterPage lab="micro" />);

    await user.click(await screen.findByRole("button", { name: /Add Test/i }));
    const dialog = await screen.findByRole("dialog");
    await waitFor(() => {
      expect(within(dialog).getByRole("combobox", { name: "Section" }).textContent).toContain("Microbiology");
    });
    await user.type(within(dialog).getByLabelText(/^Code/), "NEW-MICRO");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "New Micro Test");
    await user.click(within(dialog).getByRole("button", { name: /^Add Test$/ }));

    expect(await screen.findByText(/Details & Workflow Steps: NEW-MICRO/)).toBeTruthy();
    await waitFor(() => {
      expect(masterDataOptions.getTestWorkflowSteps).toHaveBeenCalledWith(31);
    });
    expect(screen.getByText('Test "NEW-MICRO" added to the Test Master.')).toBeTruthy();
    expect(locationText()).toBe("/x?test=31");
  });

  it("does not open workflow steps after adding a physicochemical test", async () => {
    asFpUser();
    const created: TestDefinitionOption = { ...gravimetric, id: 32, code: "NEW-FP", displayName: "New FP Test" };
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValueOnce([]).mockResolvedValue([created]);
    vi.mocked(masterDataOptions.createTestDefinition).mockResolvedValue(created as never);
    const user = userEvent.setup();
    renderInRouter(<TestMasterPage lab="fp" area="fp" />);

    await user.click(await screen.findByRole("button", { name: /Add Test/i }));
    const dialog = await screen.findByRole("dialog");
    await waitFor(() => {
      expect(within(dialog).getByRole("combobox", { name: "Section" }).textContent).toContain("Finished Product");
    });
    await user.click(within(dialog).getByLabelText("Workflow Type"));
    await user.click(await screen.findByRole("option", { name: /Qualitative/i }));
    await user.type(within(dialog).getByLabelText(/^Code/), "NEW-FP");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "New FP Test");
    await user.click(within(dialog).getByRole("button", { name: /^Add Test$/ }));

    expect(await screen.findByText('Test "NEW-FP" added to the Test Master.')).toBeTruthy();
    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
    });
    expect(screen.getByText("New FP Test")).toBeTruthy();
    expect(screen.queryByText(/Details & Workflow Steps/)).toBeNull();
    expect(masterDataOptions.getTestWorkflowSteps).not.toHaveBeenCalled();
    expect(locationText()).toBe("/x");
  });

  it("Duplicate opens the Add dialog prefilled except code, name and CoA order, and saves a new test", async () => {
    asFpUser();
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([gravimetric]);
    vi.mocked(masterDataOptions.createTestDefinition).mockResolvedValue({ ...gravimetric, id: 40 } as never);
    const user = userEvent.setup();
    renderInRouter(<TestMasterPage lab="fp" area="fp" />);

    await user.click((await screen.findAllByRole("button", { name: "Row actions" }))[0]);
    await user.click(await screen.findByRole("menuitem", { name: "Duplicate" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("Add Test (copy of GRAV-DUP)")).toBeTruthy();
    expect((within(dialog).getByLabelText(/^Code/) as HTMLInputElement).value).toBe("");
    expect((within(dialog).getByLabelText(/^Display Name/) as HTMLInputElement).value).toBe("Loss on Drying (oven) (copy)");
    expect((within(dialog).getByLabelText(/^CoA order/) as HTMLInputElement).value).toBe("");
    expect(within(dialog).getByLabelText("Workflow Type").textContent).toContain("Gravimetric");
    expect(within(dialog).getByRole("combobox", { name: "Section" }).textContent).toContain("Finished Product");
    expect(within(dialog).getByRole("button", { name: /^Add Test$/ })).toBeTruthy();
    expect(masterDataOptions.createTestDefinition).not.toHaveBeenCalled();

    await user.type(within(dialog).getByLabelText(/^Code/), "GRAV-NEW");
    await user.click(within(dialog).getByRole("button", { name: /^Add Test$/ }));

    await waitFor(() => {
      expect(masterDataOptions.createTestDefinition).toHaveBeenCalledTimes(1);
    });
    expect(masterDataOptions.updateTestDefinition).not.toHaveBeenCalled();
    const payload = vi.mocked(masterDataOptions.createTestDefinition).mock.calls[0][0];
    expect(payload).toMatchObject({
      code: "GRAV-NEW",
      displayName: "Loss on Drying (oven) (copy)",
      sectionId: 1,
      workflowType: "Gravimetric",
      equationType: "GravimetricLoss",
      replicateCount: 4,
      usesTare: true,
      conditionFields: "105 C for 3 h",
      moistureRole: "LossOnDrying",
      physchemArea: "FinishedProduct",
      coaDisplayOrder: null
    });
    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
    });
  });

  it("Duplicate then Cancel sends nothing", async () => {
    asFpUser();
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([gravimetric]);
    const user = userEvent.setup();
    renderInRouter(<TestMasterPage lab="fp" area="fp" />);

    await user.click((await screen.findAllByRole("button", { name: "Row actions" }))[0]);
    await user.click(await screen.findByRole("menuitem", { name: "Duplicate" }));
    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: "Cancel" }));

    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
    });
    expect(masterDataOptions.createTestDefinition).not.toHaveBeenCalled();
    expect(masterDataOptions.updateTestDefinition).not.toHaveBeenCalled();

    // A plain Add afterwards starts empty again, not from the copied test.
    await user.click(screen.getByRole("button", { name: /Add Test/i }));
    const addDialog = await screen.findByRole("dialog");
    expect(within(addDialog).queryByText(/copy of/)).toBeNull();
    expect((within(addDialog).getByLabelText(/^Display Name/) as HTMLInputElement).value).toBe("");
  });

  it("shows the server's refusal of a duplicate and keeps the dialog open with the typed code", async () => {
    asFpUser();
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([gravimetric]);
    vi.mocked(masterDataOptions.createTestDefinition).mockRejectedValue({
      response: { data: { message: "A test with this code already exists." } }
    });
    const user = userEvent.setup();
    renderInRouter(<TestMasterPage lab="fp" area="fp" />);

    await user.click((await screen.findAllByRole("button", { name: "Row actions" }))[0]);
    await user.click(await screen.findByRole("menuitem", { name: "Duplicate" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^Code/), "GRAV-DUP");
    await user.click(within(dialog).getByRole("button", { name: /^Add Test$/ }));

    expect(await within(dialog).findByText("A test with this code already exists.")).toBeTruthy();
    expect(masterDataOptions.createTestDefinition).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("dialog")).toBeTruthy();
    expect((within(dialog).getByLabelText(/^Code/) as HTMLInputElement).value).toBe("GRAV-DUP");
  });

  it("opens the details dialog named by ?test= and removes the parameter on close, keeping others", async () => {
    asMicroUser();
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([microTest]);
    const user = userEvent.setup();
    renderInRouter(<TestMasterPage lab="micro" />, ["/x?test=20&keep=1"]);

    expect(await screen.findByText(/Details & Workflow Steps: TAMC-01/)).toBeTruthy();
    expect(masterDataOptions.getTestWorkflowSteps).toHaveBeenCalledWith(20);
    expect(locationText()).toBe("/x?test=20&keep=1");

    await user.click(screen.getByRole("button", { name: "Close dialog" }));

    await waitFor(() => {
      expect(screen.queryByText(/Details & Workflow Steps/)).toBeNull();
    });
    expect(locationText()).toBe("/x?keep=1");
  });

  it("clicking a row puts its id in the URL", async () => {
    asMicroUser();
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([microTest]);
    const user = userEvent.setup();
    renderInRouter(<TestMasterPage lab="micro" />);

    await user.click(await screen.findByText("Total Aerobic Count"));

    expect(await screen.findByText(/Details & Workflow Steps: TAMC-01/)).toBeTruthy();
    expect(locationText()).toBe("/x?test=20");
  });

  it("ignores a ?test= that matches no test on this page and removes it", async () => {
    asMicroUser();
    // gravimetric belongs to the FP laboratory, so the micro page does not list it.
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([microTest, gravimetric]);
    renderInRouter(<TestMasterPage lab="micro" />, ["/x?test=21"]);

    expect(await screen.findByText("Total Aerobic Count")).toBeTruthy();
    await waitFor(() => {
      expect(locationText()).toBe("/x");
    });
    expect(screen.queryByText(/Details & Workflow Steps/)).toBeNull();
    expect(masterDataOptions.getTestWorkflowSteps).not.toHaveBeenCalled();
  });
});
