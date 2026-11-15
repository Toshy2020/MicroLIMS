import { describe, expect, it, vi, afterEach, beforeEach } from "vitest";
import { render, screen, waitFor, cleanup, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { TestMasterPage } from "./TestMasterPage";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { resetDraftDb, signInForTest } from "../../../services/drafts/draftTestUtils";
import * as laboratorySectionService from "../../../services/laboratorySectionService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { masterDataUsageService } from "../../../services/masterDataUsageService";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";

// "CoA order" on the Test Master wizard: the real page, hook and wizard are
// mounted; only the HTTP service modules are mocked (CLAUDE.md Rule 4.5).

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

afterEach(cleanup);

const salmonella: TestDefinitionOption = {
  id: 7,
  code: "SALM",
  displayName: "Salmonella",
  sectionId: 1,
  workflowType: "Observation",
  isActive: true,
  coaDisplayOrder: 3
};

function renderPage() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <TestMasterPage lab="micro" />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

async function advanceWizardAndSave(user: ReturnType<typeof userEvent.setup>, dialog: HTMLElement) {
  while (within(dialog).queryByRole("button", { name: /^Next$/i })) {
    await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));
  }
  const saveBtn = (await within(dialog).findByRole("button", { name: /^Save$/i })) as HTMLButtonElement;
  await waitFor(() => expect(saveBtn.disabled).toBe(false));
  await user.click(saveBtn);
}

describe("Test Master CoA order", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    await resetDraftDb();
    signInForTest(7);
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
      { sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology" }
    ] as never);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      { sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology" }
    ] as never);
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([salmonella]);
    vi.mocked(masterDataOptions.validateTestDefinition).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getTestWorkflowSteps).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getTestDefinitionStageReplicates).mockResolvedValue([]);
    vi.mocked(masterDataUsageService.getUsage).mockResolvedValue({
      type: "TestDefinition",
      id: 7,
      openWorkCount: 0,
      historicalCount: 0,
      lines: []
    });
    vi.mocked(masterDataOptions.createTestDefinition).mockResolvedValue({ ...salmonella, id: 8 });
    vi.mocked(masterDataOptions.createTestWithSteps).mockResolvedValue({ ...salmonella, id: 8 });
    vi.mocked(masterDataOptions.updateTestDefinition).mockResolvedValue(salmonella);
  });

  it("sends the entered CoA order when a test is created", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: /Add Test/i }));
    const dialog = await screen.findByRole("dialog");
    await user.type(await within(dialog).findByLabelText(/^Code/i), "ECOLI");
    await user.type(within(dialog).getByLabelText(/^Display Name/i), "Escherichia coli");
    await user.type(within(dialog).getByLabelText(/^CoA order/i), "2");
    await advanceWizardAndSave(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    const payload = vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test;
    expect(payload).toMatchObject({ code: "ECOLI", coaDisplayOrder: 2 });
  });

  it("shows the saved order on edit and sends 0 to clear it", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click((await screen.findAllByRole("button", { name: /^Row actions for / }))[0]);
    await user.click(await screen.findByRole("menuitem", { name: "Edit" }));
    const dialog = await screen.findByRole("dialog");
    const field = (await within(dialog).findByLabelText(/^CoA order/i)) as HTMLInputElement;
    expect(field.value).toBe("3");

    await user.clear(field);
    await advanceWizardAndSave(user, dialog);

    await waitFor(() => expect(masterDataOptions.updateTestDefinition).toHaveBeenCalledTimes(1));
    const [id, payload] = vi.mocked(masterDataOptions.updateTestDefinition).mock.calls[0];
    expect(id).toBe(7);
    expect(payload).toMatchObject({ coaDisplayOrder: 0 });
  });

  it("refuses a CoA order that is not a whole number from 1 to 9999", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: /Add Test/i }));
    const dialog = await screen.findByRole("dialog");
    await user.type(await within(dialog).findByLabelText(/^Code/i), "ECOLI");
    await user.type(within(dialog).getByLabelText(/^Display Name/i), "Escherichia coli");
    await user.type(within(dialog).getByLabelText(/^CoA order/i), "0");
    await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));

    expect((await within(dialog).findAllByText(/CoA order must be a whole number from 1 to 9999/i)).length).toBeGreaterThan(0);
    expect(masterDataOptions.createTestWithSteps).not.toHaveBeenCalled();
  });
});
