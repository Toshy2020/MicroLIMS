import { describe, expect, it, vi, afterEach, beforeEach } from "vitest";
import { render, screen, waitFor, cleanup, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { TestMasterPage } from "./TestMasterPage";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { resetDraftDb, signInForTest } from "../../../services/drafts/draftTestUtils";
import * as laboratorySectionService from "../../../services/laboratorySectionService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { masterDataUsageService } from "../../../services/masterDataUsageService";
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
// page and wizard run (CLAUDE.md Rule 4.5).
vi.mock("../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/masterDataOptions")>();
  return {
    ...actual,
    masterDataOptions: {
      ...actual.masterDataOptions,
      getTestDefinitions: vi.fn(),
      createTestDefinition: vi.fn(),
      createTestWithSteps: vi.fn(),
      updateTestDefinition: vi.fn(),
      validateTestDefinition: vi.fn(),
      freezeTestDefinition: vi.fn(),
      unfreezeTestDefinition: vi.fn(),
      getTestWorkflowSteps: vi.fn().mockResolvedValue([]),
      getTestDefinitionStageReplicates: vi.fn().mockResolvedValue([]),
      getOrganisms: vi.fn().mockResolvedValue([]),
      getMediaIncubationConditions: vi.fn().mockResolvedValue([]),
      getMediaProducts: vi.fn().mockResolvedValue([])
    }
  };
});

vi.mock("../../../services/masterDataUsageService", () => ({
  masterDataUsageService: {
    getUsage: vi.fn()
  }
}));

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

type User = ReturnType<typeof userEvent.setup>;

async function clickNextStep(user: User, dialog: HTMLElement) {
  await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));
}

async function clickBackStep(user: User, dialog: HTMLElement) {
  await user.click(within(dialog).getByRole("button", { name: /^Back$/i }));
}

async function advanceWizardAndSave(user: User, dialog: HTMLElement) {
  while (within(dialog).queryByRole("button", { name: /^Next$/i })) {
    await clickNextStep(user, dialog);
  }
  const saveBtn = (await within(dialog).findByRole("button", { name: /^Save$/i })) as HTMLButtonElement;
  await waitFor(() => expect(saveBtn.disabled).toBe(false));
  await user.click(saveBtn);
}

function setupCommonMocks() {
  vi.mocked(masterDataOptions.validateTestDefinition).mockResolvedValue([]);
  vi.mocked(masterDataOptions.getTestWorkflowSteps).mockResolvedValue([]);
  vi.mocked(masterDataOptions.getTestDefinitionStageReplicates).mockResolvedValue([]);
  vi.mocked(masterDataUsageService.getUsage).mockResolvedValue({
    type: "TestDefinition",
    id: 1,
    openWorkCount: 0,
    historicalCount: 0,
    lines: []
  });
}

afterEach(cleanup);

describe("TestMasterPage flag visibility and interaction", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    await resetDraftDb();
    signInForTest(7);
    setupCommonMocks();
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
        <AuthProvider>
          <MemoryRouter>
            <TestMasterPage lab="fp" area="fp" />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Add Test/i })).toBeTruthy();
    });

    await user.click(screen.getByRole("button", { name: /Add Test/i }));
    const dialog = await screen.findByRole("dialog");

    // 1. Switch to Qualitative workflow on Basics and fill required identity
    await user.type(within(dialog).getByLabelText(/^Code/), "Q1");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Qualitative One");
    await user.click(within(dialog).getByLabelText("Workflow Type"));
    await user.click(await screen.findByRole("option", { name: /Qualitative/i }));
    await clickNextStep(user, dialog);

    // "Recorded per container" switch must be visible
    expect(within(dialog).getByLabelText(/Recorded per container/i)).toBeTruthy();
    expect(within(dialog).queryByLabelText(/^Moisture role/i)).toBeNull();

    // 2. Go back to Basics and switch to Gravimetric workflow (defaults to GravimetricLoss)
    await clickBackStep(user, dialog);
    await user.click(within(dialog).getByLabelText("Workflow Type"));
    await user.click(await screen.findByRole("option", { name: /Gravimetric/i }));
    await clickNextStep(user, dialog);

    expect(within(dialog).queryByLabelText(/Recorded per container/i)).toBeNull();
    expect(within(dialog).getByLabelText(/^Moisture role/i)).toBeTruthy();

    // 3. Change Equation Type to GravimetricResidue -> Moisture role must NOT be visible
    await user.click(within(dialog).getByLabelText("Equation Type"));
    await user.click(await screen.findByRole("option", { name: /Ash \/ Gravimetric residue/i }));
    expect(within(dialog).queryByLabelText(/^Moisture role/i)).toBeNull();

    // 4. Go back to Basics and switch to Titration workflow
    await clickBackStep(user, dialog);
    await user.click(within(dialog).getByLabelText("Workflow Type"));
    await user.click(await screen.findByRole("option", { name: /Titration/i }));
    await clickNextStep(user, dialog);

    expect(within(dialog).queryByLabelText(/^Moisture role/i)).toBeNull();

    // Change Titration type to KarlFischer -> Moisture role appears
    await user.click(within(dialog).getByLabelText(/Titration type/i));
    await user.click(await screen.findByRole("option", { name: /Karl Fischer/i }));
    expect(within(dialog).getByLabelText(/^Moisture role/i)).toBeTruthy();
  });

  it("changing moistureRole on an existing titration test displays the reason for change field", async () => {
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter>
            <TestMasterPage lab="fp" area="fp" />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    await waitFor(() => {
      expect(screen.getByText("Water Determination KF")).toBeTruthy();
    });

    const actionButtons = screen.getAllByRole("button", { name: /Row actions/i });
    await user.click(actionButtons[2]);
    await user.click(await screen.findByRole("menuitem", { name: "Edit" }));

    const dialog = await screen.findByRole("dialog");
    await clickNextStep(user, dialog);

    expect(within(dialog).queryByLabelText(/Reason for Change/i)).toBeNull();

    await user.click(within(dialog).getByLabelText(/^Moisture role/i));
    await user.click(await screen.findByRole("option", { name: "Water" }));

    expect(within(dialog).getByLabelText(/Reason for Change/i)).toBeTruthy();
  });

  it("resets perContainer when leaving Qualitative and resets moistureRole when changing to residue", async () => {
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter>
            <TestMasterPage lab="fp" area="fp" />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Add Test/i })).toBeTruthy();
    });

    await user.click(screen.getByRole("button", { name: /Add Test/i }));
    const dialog = await screen.findByRole("dialog");

    await user.type(within(dialog).getByLabelText(/^Code/), "Q-RESET");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Qualitative Reset");
    await user.click(within(dialog).getByLabelText("Workflow Type"));
    await user.click(await screen.findByRole("option", { name: /Qualitative/i }));
    await clickNextStep(user, dialog);

    const perContainerSwitch = within(dialog).getByRole("switch", { name: /Recorded per container/i });
    expect((perContainerSwitch as HTMLInputElement).checked).toBe(false);
    await user.click(perContainerSwitch);
    expect((perContainerSwitch as HTMLInputElement).checked).toBe(true);

    // Switch away to Gravimetric, select Loss on drying, switch equation to Residue and back
    await clickBackStep(user, dialog);
    await user.click(within(dialog).getByLabelText("Workflow Type"));
    await user.click(await screen.findByRole("option", { name: /Gravimetric/i }));
    await clickNextStep(user, dialog);

    await user.click(within(dialog).getByLabelText(/^Moisture role/i));
    await user.click(await screen.findByRole("option", { name: /Loss on drying/i }));

    await user.click(within(dialog).getByLabelText("Equation Type"));
    await user.click(await screen.findByRole("option", { name: /Ash \/ Gravimetric residue/i }));

    await user.click(within(dialog).getByLabelText("Equation Type"));
    await user.click(await screen.findByRole("option", { name: /Loss on drying \/ Gravimetric loss/i }));

    expect(within(dialog).getByLabelText(/^Moisture role/i).textContent).toContain("None");

    // Switch back to Qualitative -> perContainer should be false
    await clickBackStep(user, dialog);
    await user.click(within(dialog).getByLabelText("Workflow Type"));
    await user.click(await screen.findByRole("option", { name: /Qualitative/i }));
    await clickNextStep(user, dialog);
    const recheckedSwitch = within(dialog).getByRole("switch", { name: /Recorded per container/i });
    expect((recheckedSwitch as HTMLInputElement).checked).toBe(false);
  });
});

describe("TestMasterPage - saving, validation and server refusal", () => {
  const createdDefinition: TestDefinitionOption = {
    id: 10,
    code: "TAMC-01",
    displayName: "Total Aerobic Count",
    sectionId: 2,
    workflowType: "Observation",
    equationType: "None",
    isActive: true
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    await resetDraftDb();
    signInForTest(7);
    setupCommonMocks();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
      { sectionId: 1, sectionCode: "FP", sectionName: "Finished Product" },
      { sectionId: 2, sectionCode: "MICRO", sectionName: "Microbiology" }
    ] as never);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      { sectionId: 2, sectionCode: "MICRO", sectionName: "Microbiology" }
    ] as never);
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([]);
    vi.mocked(masterDataOptions.createTestWithSteps).mockResolvedValue(createdDefinition as never);
  });

  async function openAddDialog(user: User) {
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter>
            <TestMasterPage lab="micro" />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );
    await user.click(await screen.findByRole("button", { name: /Add Test/i }));
    const dialog = await screen.findByRole("dialog");
    await waitFor(() => {
      expect(within(dialog).getByRole("combobox", { name: "Section" }).textContent).toContain("Microbiology");
    });
    return dialog;
  }

  it("saves the test through the wizard and calls createTestWithSteps", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user);

    await user.type(within(dialog).getByLabelText(/^Code/), "TAMC-01");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Total Aerobic Count");
    await advanceWizardAndSave(user, dialog);

    await waitFor(() => {
      expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1);
    });
    const payload = vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0];
    expect(payload).toMatchObject({
      test: {
        code: "TAMC-01",
        displayName: "Total Aerobic Count",
        sectionId: 2,
        workflowType: "Observation",
        equationType: "None",
        coaDisplayOrder: null
      },
      stages: [],
      steps: []
    });

    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
    });
    expect(screen.getByText('Test "TAMC-01" added to the Test Master.')).toBeTruthy();
  });

  it("blocks advancing when a required field is missing and saves nothing", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user);

    await user.type(within(dialog).getByLabelText(/^Display Name/), "Total Aerobic Count");
    await clickNextStep(user, dialog);

    expect((await within(dialog).findAllByText("Both Code and Display Name are required.")).length).toBeGreaterThan(0);
    expect(masterDataOptions.createTestWithSteps).not.toHaveBeenCalled();
    expect(screen.getByRole("dialog")).toBeTruthy();
  });

  it("shows the server's refusal and keeps the dialog open", async () => {
    vi.mocked(masterDataOptions.createTestWithSteps).mockRejectedValue({
      isAxiosError: true,
      response: { data: { message: "A test with this code already exists." } }
    });
    const user = userEvent.setup();
    const dialog = await openAddDialog(user);

    await user.type(within(dialog).getByLabelText(/^Code/), "TAMC-01");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Total Aerobic Count");
    await advanceWizardAndSave(user, dialog);

    expect(await within(dialog).findByText("A test with this code already exists.")).toBeTruthy();
    expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("dialog")).toBeTruthy();
    await clickBackStep(user, dialog);
    await clickBackStep(user, dialog);
    expect((within(dialog).getByLabelText(/^Code/) as HTMLInputElement).value).toBe("TAMC-01");
  });

  it("disables Save while a save is in flight and does not send a second request", async () => {
    let resolveCreate: (value: never) => void = () => {};
    vi.mocked(masterDataOptions.createTestWithSteps).mockReturnValue(
      new Promise<never>((resolve) => {
        resolveCreate = resolve;
      })
    );
    const user = userEvent.setup();
    const dialog = await openAddDialog(user);

    await user.type(within(dialog).getByLabelText(/^Code/), "TAMC-01");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Total Aerobic Count");
    await clickNextStep(user, dialog);
    await clickNextStep(user, dialog);

    const saveBtn = (await within(dialog).findByRole("button", { name: /^Save$/i })) as HTMLButtonElement;
    await waitFor(() => expect(saveBtn.disabled).toBe(false));
    await user.click(saveBtn);

    await waitFor(() => {
      expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1);
    });
    const savingBtn = within(dialog).getByRole("button", { name: /Saving/i }) as HTMLButtonElement;
    expect(savingBtn.disabled).toBe(true);
    expect(screen.getByRole("dialog")).toBeTruthy();

    resolveCreate(createdDefinition as never);
    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
    });
    expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1);
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
      <AuthProvider>
        <MemoryRouter initialEntries={initialEntries}>
          <LocationProbe />
          {page}
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const locationText = () => screen.getByTestId("location").textContent ?? "";

describe("TestMasterPage - workflow steps after adding, new from existing and deep link", () => {
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

  beforeEach(async () => {
    vi.clearAllMocks();
    await resetDraftDb();
    signInForTest(7);
    setupCommonMocks();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
      { sectionId: 1, sectionCode: "FP", sectionName: "Finished Product" },
      { sectionId: 2, sectionCode: "MICRO", sectionName: "Microbiology" }
    ] as never);
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
    vi.mocked(masterDataOptions.createTestWithSteps).mockResolvedValue(newMicroTest as never);
    const user = userEvent.setup();
    renderInRouter(<TestMasterPage lab="micro" />);

    await user.click(await screen.findByRole("button", { name: /Add Test/i }));
    const dialog = await screen.findByRole("dialog");
    await waitFor(() => {
      expect(within(dialog).getByRole("combobox", { name: "Section" }).textContent).toContain("Microbiology");
    });
    await user.type(within(dialog).getByLabelText(/^Code/), "NEW-MICRO");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "New Micro Test");
    await advanceWizardAndSave(user, dialog);

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
    vi.mocked(masterDataOptions.createTestWithSteps).mockResolvedValue(created as never);
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
    await advanceWizardAndSave(user, dialog);

    expect(await screen.findByText('Test "NEW-FP" added to the Test Master.')).toBeTruthy();
    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
    });
    expect(screen.getByText("New FP Test")).toBeTruthy();
    expect(screen.queryByText(/Details & Workflow Steps/)).toBeNull();
    expect(masterDataOptions.getTestWorkflowSteps).not.toHaveBeenCalled();
    expect(locationText()).toBe("/x");
  });

  it("New from existing opens the wizard prefilled except code, display name and CoA order, and saves a new test", async () => {
    asFpUser();
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([gravimetric]);
    vi.mocked(masterDataOptions.createTestWithSteps).mockResolvedValue({ ...gravimetric, id: 40 } as never);
    const user = userEvent.setup();
    renderInRouter(<TestMasterPage lab="fp" area="fp" />);

    await user.click((await screen.findAllByRole("button", { name: /Row actions/i }))[0]);
    await user.click(await screen.findByRole("menuitem", { name: "New from existing" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText('New Test from "GRAV-DUP"')).toBeTruthy();
    expect((within(dialog).getByLabelText(/^Code/) as HTMLInputElement).value).toBe("");
    expect((within(dialog).getByLabelText(/^Display Name/) as HTMLInputElement).value).toBe("");
    expect((within(dialog).getByLabelText(/^CoA order/) as HTMLInputElement).value).toBe("");
    expect(within(dialog).getByLabelText("Workflow Type").textContent).toContain("Gravimetric");
    expect(within(dialog).getByRole("combobox", { name: "Section" }).textContent).toContain("Finished Product");
    expect(masterDataOptions.createTestWithSteps).not.toHaveBeenCalled();

    await user.type(within(dialog).getByLabelText(/^Code/), "GRAV-NEW");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Loss on Drying (copy)");
    await advanceWizardAndSave(user, dialog);

    await waitFor(() => {
      expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1);
    });
    expect(masterDataOptions.updateTestDefinition).not.toHaveBeenCalled();
    const payload = vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test;
    expect(payload).toMatchObject({
      code: "GRAV-NEW",
      displayName: "Loss on Drying (copy)",
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

  it("New from existing then Close sends nothing", async () => {
    asFpUser();
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([gravimetric]);
    const user = userEvent.setup();
    renderInRouter(<TestMasterPage lab="fp" area="fp" />);

    await user.click((await screen.findAllByRole("button", { name: /Row actions/i }))[0]);
    await user.click(await screen.findByRole("menuitem", { name: "New from existing" }));
    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: /^Close$/i }));

    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
    });
    expect(masterDataOptions.createTestWithSteps).not.toHaveBeenCalled();
    expect(masterDataOptions.updateTestDefinition).not.toHaveBeenCalled();

    // A plain Add afterwards starts empty again, not from the copied test.
    await user.click(screen.getByRole("button", { name: /Add Test/i }));
    const addDialog = await screen.findByRole("dialog");
    expect(within(addDialog).queryByText(/New Test from/)).toBeNull();
    expect((within(addDialog).getByLabelText(/^Display Name/) as HTMLInputElement).value).toBe("");
  });

  it("shows the server's refusal of a copy and keeps the dialog open with the typed code", async () => {
    asFpUser();
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([gravimetric]);
    vi.mocked(masterDataOptions.createTestWithSteps).mockRejectedValue({
      isAxiosError: true,
      response: { data: { message: "A test with this code already exists." } }
    });
    const user = userEvent.setup();
    renderInRouter(<TestMasterPage lab="fp" area="fp" />);

    await user.click((await screen.findAllByRole("button", { name: /Row actions/i }))[0]);
    await user.click(await screen.findByRole("menuitem", { name: "New from existing" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^Code/), "GRAV-DUP");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Loss on Drying Copy");
    await advanceWizardAndSave(user, dialog);

    expect(await within(dialog).findByText("A test with this code already exists.")).toBeTruthy();
    expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("dialog")).toBeTruthy();
    await user.click(within(dialog).getByRole("button", { name: "Basics" }));
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
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([microTest]);
    renderInRouter(<TestMasterPage lab="micro" />, ["/x?test=999"]);

    expect(await screen.findByText("Total Aerobic Count")).toBeTruthy();
    await waitFor(() => {
      expect(locationText()).toBe("/x");
    });
    expect(screen.queryByText(/Details & Workflow Steps/)).toBeNull();
  });
});

describe("TestMasterPage - method acceptance fields are required, never defaulted on save", () => {
  const dissolutionDefaults = {
    dissolutionS1Offset: 5,
    dissolutionS2MinOffset: 15,
    dissolutionS3MinOffset: 25,
    dissolutionS3MaxBelowS2Min: 2
  };
  const disintegrationDefaults = {
    disintegrationStage1Units: 6,
    disintegrationStage2Units: 12,
    disintegrationMaxStage1Failures: 2,
    disintegrationMinPassTotal: 16
  };
  const weightVariationDefaults = {
    wvUnitCount: 20,
    wvTabletBand1MaxMg: 130,
    wvTabletBand1Percent: 10,
    wvTabletBand2MaxMg: 324,
    wvTabletBand2Percent: 7.5,
    wvTabletBand3Percent: 5,
    wvTabletMaxOutside: 2,
    wvCapsuleInnerPercent: 10,
    wvCapsuleOuterPercent: 25,
    wvCapsuleS1MaxOutside: 2,
    wvCapsuleS1MaxForRetest: 6,
    wvCapsuleS2ExtraUnits: 40,
    wvCapsuleS2MaxOutside: 6
  };

  const storedDissolution: TestDefinitionOption = {
    id: 50,
    code: "DISS-01",
    displayName: "Dissolution Test",
    sectionId: 1,
    workflowType: "Dissolution",
    equationType: "Dissolution",
    methodAbbreviation: "DISS-A",
    isActive: true,
    physchemArea: "FinishedProduct",
    dissolutionS1Offset: 6,
    dissolutionS2MinOffset: 16,
    dissolutionS3MinOffset: 26,
    dissolutionS3MaxBelowS2Min: 3
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    await resetDraftDb();
    signInForTest(7);
    setupCommonMocks();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
      { sectionId: 1, sectionCode: "FP", sectionName: "Finished Product" }
    ] as never);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      { sectionId: 1, sectionCode: "FP", sectionName: "Finished Product" }
    ] as never);
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([]);
    vi.mocked(masterDataOptions.createTestWithSteps).mockResolvedValue({ ...storedDissolution, id: 60 } as never);
    vi.mocked(masterDataOptions.updateTestDefinition).mockResolvedValue(storedDissolution as never);
  });

  async function openAddDialogFor(user: User, workflowOption: RegExp) {
    renderInRouter(<TestMasterPage lab="fp" area="fp" />);
    await user.click(await screen.findByRole("button", { name: /Add Test/i }));
    const dialog = await screen.findByRole("dialog");
    await waitFor(() => {
      expect(within(dialog).getByRole("combobox", { name: "Section" }).textContent).toContain("Finished Product");
    });
    await user.click(within(dialog).getByLabelText("Workflow Type"));
    await user.click(await screen.findByRole("option", { name: workflowOption }));
    await user.type(within(dialog).getByLabelText(/^Code/), "NEW-METHOD");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "New Method Test");
    await clickNextStep(user, dialog);
    return dialog;
  }

  async function openAddDissolution(user: User) {
    const dialog = await openAddDialogFor(user, /^Dissolution/);
    await user.type(within(dialog).getByPlaceholderText("e.g. VIT-C"), "DISS-A");
    return dialog;
  }

  const numberField = (dialog: HTMLElement, label: RegExp) => within(dialog).getByLabelText(label) as HTMLInputElement;

  it("rejects a cleared dissolution offset, names the field and sends nothing", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDissolution(user);

    await user.clear(numberField(dialog, /^S2: no unit/));
    await clickNextStep(user, dialog);

    expect((await within(dialog).findAllByText('Dissolution: "S2: no unit < Q −" is required.')).length).toBeGreaterThan(0);
    expect(masterDataOptions.createTestWithSteps).not.toHaveBeenCalled();
    expect(screen.getByRole("dialog")).toBeTruthy();
  });

  it("saves the dissolution offsets exactly as shown once the cleared field is typed back", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDissolution(user);

    const s2 = numberField(dialog, /^S2: no unit/);
    await user.clear(s2);
    await clickNextStep(user, dialog);
    expect((await within(dialog).findAllByText(/is required\./)).length).toBeGreaterThan(0);

    await user.type(s2, "15");
    await advanceWizardAndSave(user, dialog);

    await waitFor(() => {
      expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1);
    });
    const payload = vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test;
    expect(payload).toMatchObject({
      workflowType: "Dissolution",
      methodAbbreviation: "DISS-A",
      ...dissolutionDefaults,
      disintegrationStage1Units: null,
      wvUnitCount: null
    });
  });

  it("sends the value the user changed, not the prefill", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDissolution(user);

    const s1 = numberField(dialog, /^S1: each unit/);
    await user.clear(s1);
    await user.type(s1, "7");
    await advanceWizardAndSave(user, dialog);

    await waitFor(() => {
      expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1);
    });
    const payload = vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test;
    expect(payload).toMatchObject({ ...dissolutionDefaults, dissolutionS1Offset: 7 });
  });

  it("treats text a number field cannot hold as blank", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDissolution(user);

    const s3 = numberField(dialog, /^S3: no unit/);
    await user.clear(s3);
    await user.type(s3, "abc");
    expect(s3.value).toBe("");
    await clickNextStep(user, dialog);

    expect((await within(dialog).findAllByText('Dissolution: "S3: no unit < Q −" is required.')).length).toBeGreaterThan(0);
    expect(masterDataOptions.createTestWithSteps).not.toHaveBeenCalled();
  });

  it("keeps the dissolution range check for a negative offset", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDissolution(user);

    const s1 = numberField(dialog, /^S1: each unit/);
    await user.clear(s1);
    await user.type(s1, "-3");
    await clickNextStep(user, dialog);

    expect((await within(dialog).findAllByText("Dissolution stage offsets must be greater than or equal to zero.")).length).toBeGreaterThan(0);
    expect(masterDataOptions.createTestWithSteps).not.toHaveBeenCalled();
  });

  it("rejects a cleared disintegration field, then saves all four values once filled", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialogFor(user, /^Disintegration/);

    const units = numberField(dialog, /^Stage 1 Units/);
    await user.clear(units);
    await clickNextStep(user, dialog);

    expect((await within(dialog).findAllByText('Disintegration: "Stage 1 Units" is required.')).length).toBeGreaterThan(0);
    expect(masterDataOptions.createTestWithSteps).not.toHaveBeenCalled();
    expect(screen.getByRole("dialog")).toBeTruthy();

    await user.type(units, "6");
    await advanceWizardAndSave(user, dialog);

    await waitFor(() => {
      expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1);
    });
    const payload = vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test;
    expect(payload).toMatchObject({
      workflowType: "Disintegration",
      ...disintegrationDefaults,
      dissolutionS1Offset: null,
      wvUnitCount: null
    });
  });

  it("rejects a cleared weight variation field, then saves all thirteen values once filled", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialogFor(user, /^Weight Variation/);

    const band3 = numberField(dialog, /^Band 3 Deviation/);
    await user.clear(band3);
    await clickNextStep(user, dialog);

    expect((await within(dialog).findAllByText('Weight variation: "Band 3 Deviation (%)" is required.')).length).toBeGreaterThan(0);
    expect(masterDataOptions.createTestWithSteps).not.toHaveBeenCalled();
    expect(screen.getByRole("dialog")).toBeTruthy();

    await user.type(band3, "5");
    await advanceWizardAndSave(user, dialog);

    await waitFor(() => {
      expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1);
    });
    const payload = vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test;
    expect(payload).toMatchObject({
      workflowType: "WeightVariation",
      ...weightVariationDefaults,
      dissolutionS1Offset: null,
      disintegrationStage1Units: null
    });
  });

  it("names the first blank weight variation field in on-screen order", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialogFor(user, /^Weight Variation/);

    await user.clear(numberField(dialog, /^Stage 2 Max Outside/));
    await user.clear(numberField(dialog, /^Band 1 Max \(mg\)/));
    await clickNextStep(user, dialog);

    expect((await within(dialog).findAllByText('Weight variation: "Band 1 Max (mg)" is required.')).length).toBeGreaterThan(0);
    expect(masterDataOptions.createTestWithSteps).not.toHaveBeenCalled();
  });

  it("keeps the weight variation consistency check", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialogFor(user, /^Weight Variation/);

    const band1 = numberField(dialog, /^Band 1 Max \(mg\)/);
    await user.clear(band1);
    await user.type(band1, "400");
    await clickNextStep(user, dialog);

    expect((await within(dialog).findAllByText("Weight variation Tablet Band 1 Max Mg must be less than Band 2 Max Mg.")).length).toBeGreaterThan(0);
    expect(masterDataOptions.createTestWithSteps).not.toHaveBeenCalled();
  });

  it("editing a stored dissolution test: a cleared field is rejected and nothing is updated", async () => {
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([storedDissolution]);
    const user = userEvent.setup();
    renderInRouter(<TestMasterPage lab="fp" area="fp" />);

    await user.click((await screen.findAllByRole("button", { name: /Row actions/i }))[0]);
    await user.click(await screen.findByRole("menuitem", { name: "Edit" }));
    const dialog = await screen.findByRole("dialog");
    await clickNextStep(user, dialog);

    expect(numberField(dialog, /^S1: each unit/).value).toBe("6");

    await user.clear(numberField(dialog, /^S3: max units/));
    await clickNextStep(user, dialog);

    expect((await within(dialog).findAllByText('Dissolution: "S3: max units < Q − S2 value" is required.')).length).toBeGreaterThan(0);
    expect(masterDataOptions.updateTestDefinition).not.toHaveBeenCalled();
    expect(screen.getByRole("dialog")).toBeTruthy();

    await user.type(numberField(dialog, /^S3: max units/), "3");
    await advanceWizardAndSave(user, dialog);

    await waitFor(() => {
      expect(masterDataOptions.updateTestDefinition).toHaveBeenCalledTimes(1);
    });
    const [id, payload] = vi.mocked(masterDataOptions.updateTestDefinition).mock.calls[0];
    expect(id).toBe(50);
    expect(payload).toMatchObject({
      dissolutionS1Offset: 6,
      dissolutionS2MinOffset: 16,
      dissolutionS3MinOffset: 26,
      dissolutionS3MaxBelowS2Min: 3
    });
  });

  it("prompts for a reason when freezing and unfreezing a test definition", async () => {
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([
      mockTestDefinitions[0],
      { ...mockTestDefinitions[1], isActive: false }
    ]);
    vi.mocked(masterDataOptions.freezeTestDefinition).mockResolvedValue({ id: 1, version: 2 } as never);
    vi.mocked(masterDataOptions.unfreezeTestDefinition).mockResolvedValue({ id: 2, version: 3 } as never);
    const user = userEvent.setup();
    renderInRouter(<TestMasterPage lab="fp" area="fp" />);

    const menus = await screen.findAllByRole("button", { name: /Row actions/i });
    await user.click(menus[0]);
    await user.click(await screen.findByRole("menuitem", { name: "Freeze" }));
    await user.type(await screen.findByLabelText(/Reason for change/i), "Method retired from SOP");
    await user.click(screen.getByRole("button", { name: "Freeze" }));

    await waitFor(() =>
      expect(masterDataOptions.freezeTestDefinition).toHaveBeenCalledWith(1, undefined, "Method retired from SOP")
    );

    const menusAfter = await screen.findAllByRole("button", { name: /Row actions/i });
    await user.click(menusAfter[1]);
    await user.click(await screen.findByRole("menuitem", { name: "Unfreeze" }));
    await user.type(await screen.findByLabelText(/Reason for change/i), "Restored for stability run");
    await user.click(screen.getByRole("button", { name: "Unfreeze" }));

    await waitFor(() =>
      expect(masterDataOptions.unfreezeTestDefinition).toHaveBeenCalledWith(2, undefined, "Restored for stability run")
    );
  });
});
