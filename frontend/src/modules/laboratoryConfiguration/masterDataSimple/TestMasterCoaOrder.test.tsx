import { describe, expect, it, vi, afterEach, beforeEach } from "vitest";
import { render, screen, waitFor, cleanup, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { TestMasterPage } from "./TestMasterPage";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import * as laboratorySectionService from "../../../services/laboratorySectionService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";

// "CoA order" on the Test Master dialog: the real page, hook and dialog are
// mounted; only the HTTP service modules are mocked (CLAUDE.md Rule 4.5).

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
      getOrganisms: vi.fn().mockResolvedValue([])
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
      <TestMasterPage lab="micro" />
    </PinnedLightTheme>
  );
}

describe("Test Master CoA order", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
      { sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology" }
    ] as never);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      { sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology" }
    ] as never);
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([salmonella]);
    vi.mocked(masterDataOptions.createTestDefinition).mockResolvedValue({ ...salmonella, id: 8 });
    vi.mocked(masterDataOptions.updateTestDefinition).mockResolvedValue(salmonella);
  });

  it("sends the entered CoA order when a test is created", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: /Add Test/i }));
    const dialog = screen.getByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^Code/i), "ECOLI");
    await user.type(within(dialog).getByLabelText(/^Display Name/i), "Escherichia coli");
    await user.type(within(dialog).getByLabelText(/^CoA order/i), "2");
    await user.click(within(dialog).getByRole("button", { name: /^Add Test$/i }));

    await waitFor(() => expect(masterDataOptions.createTestDefinition).toHaveBeenCalledTimes(1));
    const payload = vi.mocked(masterDataOptions.createTestDefinition).mock.calls[0][0];
    expect(payload).toMatchObject({ code: "ECOLI", coaDisplayOrder: 2 });
  });

  it("shows the saved order on edit and sends 0 to clear it", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click((await screen.findAllByRole("button", { name: /^Row actions for / }))[0]);
    await user.click(await screen.findByRole("menuitem", { name: "Edit" }));
    const dialog = screen.getByRole("dialog");
    const field = within(dialog).getByLabelText(/^CoA order/i) as HTMLInputElement;
    expect(field.value).toBe("3");

    await user.clear(field);
    await user.click(within(dialog).getByRole("button", { name: /Save Changes/i }));

    await waitFor(() => expect(masterDataOptions.updateTestDefinition).toHaveBeenCalledTimes(1));
    const [id, payload] = vi.mocked(masterDataOptions.updateTestDefinition).mock.calls[0];
    expect(id).toBe(7);
    expect(payload).toMatchObject({ coaDisplayOrder: 0 });
  });

  it("refuses a CoA order that is not a whole number from 1 to 9999", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: /Add Test/i }));
    const dialog = screen.getByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^Code/i), "ECOLI");
    await user.type(within(dialog).getByLabelText(/^Display Name/i), "Escherichia coli");
    await user.type(within(dialog).getByLabelText(/^CoA order/i), "0");
    await user.click(within(dialog).getByRole("button", { name: /^Add Test$/i }));

    expect(await within(dialog).findByText(/CoA order must be a whole number from 1 to 9999/i)).toBeTruthy();
    expect(masterDataOptions.createTestDefinition).not.toHaveBeenCalled();
  });
});
