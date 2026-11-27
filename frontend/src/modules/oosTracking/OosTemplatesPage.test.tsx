import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { laboratorySectionService } from "../../services/laboratorySectionService";
import { oosInvestigationService } from "../../services/oosInvestigationService";
import { OosTemplatesPage } from "./OosTemplatesPage";

vi.mock("../../services/laboratorySectionService", () => ({
  laboratorySectionService: {
    getMySections: vi.fn()
  }
}));

vi.mock("../../services/oosInvestigationService", () => ({
  oosInvestigationService: {
    getTemplate: vi.fn(),
    saveTemplate: vi.fn(),
    listCategories: vi.fn(),
    addCategory: vi.fn(),
    saveCategory: vi.fn()
  }
}));

function renderPage(permissions: string[] = [PERMISSIONS.OOS_MANAGE, PERMISSIONS.MASTER_DATA_MANAGE]) {
  localStorage.setItem("microlims_permissions", JSON.stringify(permissions));
  return render(
    <AuthProvider>
      <PinnedLightTheme>
        <MemoryRouter>
          <OosTemplatesPage />
        </MemoryRouter>
      </PinnedLightTheme>
    </AuthProvider>
  );
}

beforeEach(() => {
  localStorage.clear();
  vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
    {
      sectionId: 1,
      sectionCode: "MICRO",
      sectionName: "Microbiology",
      departmentId: 1,
      departmentCode: "QC",
      departmentName: "Quality Control"
    }
  ]);
  vi.mocked(oosInvestigationService.getTemplate).mockResolvedValue({
    version: 3,
    phaseOneDueWorkingDays: 3,
    totalDueWorkingDays: 10,
    alertOpensExcursionReview: true,
    items: [
      { key: "k1", phase: 1, order: 1, text: "Check equipment calibration", isActive: true },
      { key: "k2", phase: 1, order: 2, text: "Verify media lot GPT", isActive: true }
    ]
  });
  vi.mocked(oosInvestigationService.saveTemplate).mockResolvedValue({
    version: 4,
    phaseOneDueWorkingDays: 3,
    totalDueWorkingDays: 10,
    alertOpensExcursionReview: true,
    items: [
      { key: "k2", phase: 1, order: 1, text: "Verify media lot GPT", isActive: true },
      { key: "k1", phase: 1, order: 2, text: "Check equipment calibration", isActive: false },
      { key: "k3", phase: 1, order: 3, text: "Inspect glassware", isActive: true }
    ]
  });
  vi.mocked(oosInvestigationService.listCategories).mockResolvedValue({
    items: [
      { id: 1, name: "Analyst error", isActive: true, isNoCauseFound: false, version: 2 },
      { id: 2, name: "No cause found", isActive: true, isNoCauseFound: true, version: 1 }
    ],
    totalCount: 2,
    page: 1,
    pageSize: 100
  });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("OosTemplatesPage", () => {
  it("adds, reorders by keyboard, deactivates checklist items and saves with expected version", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => expect(screen.getByDisplayValue("Check equipment calibration")).toBeTruthy());

    // Add a new item
    const newItemInput = screen.getByLabelText(/New checklist item text/i);
    await user.type(newItemInput, "Inspect glassware");
    await user.click(screen.getByRole("button", { name: /Add item/i }));
    expect(screen.getByDisplayValue("Inspect glassware")).toBeTruthy();

    // Reorder second item up by keyboard
    const moveUpBtn = screen.getByRole("button", { name: /Move Verify media lot GPT up/i });
    moveUpBtn.focus();
    await user.keyboard("{Enter}");

    // Deactivate Check equipment calibration (now at index 1)
    const activeSwitch = screen.getByRole("switch", { name: /Active: Check equipment calibration/i });
    await user.click(activeSwitch);

    // Save with expected version 3
    await user.click(screen.getByRole("button", { name: /Save Template/i }));

    await waitFor(() => expect(oosInvestigationService.saveTemplate).toHaveBeenCalledTimes(1));
    const [sectionId, kind, payload, version] = vi.mocked(oosInvestigationService.saveTemplate).mock.calls[0];
    expect(sectionId).toBe(1);
    expect(kind).toBe("Oos");
    expect(version).toBe(3);
    expect(payload.items).toHaveLength(3);
    expect(payload.items[0].text).toBe("Verify media lot GPT");
    expect(payload.items[0].order).toBe(1);
    expect(payload.items[1].text).toBe("Check equipment calibration");
    expect(payload.items[1].isActive).toBe(false);
    expect(payload.items[2].text).toBe("Inspect glassware");
  });

  it("shows server refusal when saving template fails", async () => {
    const user = userEvent.setup();
    vi.mocked(oosInvestigationService.saveTemplate).mockRejectedValue({
      response: { data: { message: "This record changed. Reload it and try again." } }
    });

    renderPage();
    await waitFor(() => expect(screen.getByDisplayValue("Check equipment calibration")).toBeTruthy());

    await user.click(screen.getByRole("button", { name: /Save Template/i }));

    await waitFor(() =>
      expect(screen.getByText("This record changed. Reload it and try again.")).toBeTruthy()
    );
  });

  it("locks the fixed No cause found category from rename or deactivation and allows editing others", async () => {
    const user = userEvent.setup();
    vi.mocked(oosInvestigationService.saveCategory).mockResolvedValue({
      id: 1,
      name: "Analyst training gap",
      isActive: true,
      isNoCauseFound: false,
      version: 3
    });

    renderPage();
    await waitFor(() => expect(screen.getByRole("tab", { name: /Cause Categories/i })).toBeTruthy());
    await user.click(screen.getByRole("tab", { name: /Cause Categories/i }));

    await waitFor(() => expect(screen.getByText("No cause found")).toBeTruthy());

    const fixedRow = screen.getByText("No cause found").closest("tr");
    expect(fixedRow).toBeTruthy();
    const fixedEditBtn = within(fixedRow!).queryByRole("button", { name: /Edit/i });
    expect(fixedEditBtn?.hasAttribute("disabled")).toBe(true);
    expect(within(fixedRow!).getByText(/Locked/i)).toBeTruthy();

    const editableRow = screen.getByText("Analyst error").closest("tr");
    expect(editableRow).toBeTruthy();
    await user.click(within(editableRow!).getByRole("button", { name: /Edit Analyst error/i }));

    const dialog = screen.getByRole("dialog");
    const nameField = within(dialog).getByLabelText(/Category name/i);
    await user.clear(nameField);
    await user.type(nameField, "Analyst training gap");
    await user.type(within(dialog).getByLabelText(/Reason for change/i), "Clarify wording");
    await user.click(within(dialog).getByRole("button", { name: /Save Category/i }));

    await waitFor(() =>
      expect(oosInvestigationService.saveCategory).toHaveBeenCalledWith(
        1,
        { name: "Analyst training gap", isActive: true, reason: "Clarify wording" },
        2
      )
    );
  });

  it("keeps focus on the moved row control across three consecutive keyboard moves including top boundary and announces position", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => expect(screen.getByDisplayValue("Check equipment calibration")).toBeTruthy());

    // Add two new items so we have 4 rows and can move a new item up 3 times to index 0
    const newItemInput = screen.getByLabelText(/New checklist item text/i);
    await user.type(newItemInput, "Inspect glassware");
    await user.click(screen.getByRole("button", { name: /Add item/i }));
    await user.type(newItemInput, "Verify balance");
    await user.click(screen.getByRole("button", { name: /Add item/i }));

    expect(screen.getByText(/Alt\+Up \/ Alt\+Down/i)).toBeTruthy();

    const moveUpBtn = screen.getByRole("button", { name: /Move Verify balance up/i });
    expect(moveUpBtn.getAttribute("aria-keyshortcuts")).toBe("Alt+ArrowUp");
    moveUpBtn.focus();

    await user.keyboard("{Enter}");
    expect(document.activeElement).toBe(moveUpBtn);
    expect(screen.getByRole("status").textContent).toContain("Item moved to position 3");

    await user.keyboard("{Enter}");
    expect(document.activeElement).toBe(moveUpBtn);
    expect(screen.getByRole("status").textContent).toContain("Item moved to position 2");

    await user.keyboard("{Enter}");
    expect(document.activeElement).toBe(moveUpBtn);
    expect(moveUpBtn.getAttribute("aria-disabled")).toBe("true");
    expect(screen.getByRole("status").textContent).toContain("Item moved to position 1");

    await user.click(screen.getByRole("button", { name: /Save Template/i }));
    await waitFor(() => expect(oosInvestigationService.saveTemplate).toHaveBeenCalledTimes(1));
    const [, , payload] = vi.mocked(oosInvestigationService.saveTemplate).mock.calls[0];
    expect(payload.items.map((i) => i.text)).toEqual([
      "Verify balance",
      "Check equipment calibration",
      "Verify media lot GPT",
      "Inspect glassware"
    ]);
  });

  it("gates add/edit category actions on MasterData.Manage and shows edit error once inside dialog", async () => {
    const user = userEvent.setup();

    renderPage([PERMISSIONS.OOS_MANAGE]);
    await waitFor(() => expect(screen.getByRole("tab", { name: /Cause Categories/i })).toBeTruthy());
    await user.click(screen.getByRole("tab", { name: /Cause Categories/i }));

    await waitFor(() => expect(screen.getByText("Analyst error")).toBeTruthy());
    expect(screen.getByText(/MasterData\.Manage/i)).toBeTruthy();
    expect(screen.getByRole("button", { name: /Add Category/i }).hasAttribute("disabled")).toBe(true);
    expect(screen.getByRole("button", { name: /Edit Analyst error/i }).hasAttribute("disabled")).toBe(true);

    cleanup();
    vi.mocked(oosInvestigationService.saveCategory).mockRejectedValue({
      response: { data: { message: "Category name already exists." } }
    });

    renderPage([PERMISSIONS.OOS_MANAGE, PERMISSIONS.MASTER_DATA_MANAGE]);
    await waitFor(() => expect(screen.getByRole("tab", { name: /Cause Categories/i })).toBeTruthy());
    await user.click(screen.getByRole("tab", { name: /Cause Categories/i }));
    await waitFor(() => expect(screen.getByText("Analyst error")).toBeTruthy());

    await user.click(screen.getByRole("button", { name: /Edit Analyst error/i }));
    const dialog = screen.getByRole("dialog");
    await user.type(within(dialog).getByLabelText(/Reason for change/i), "Fix typo");
    await user.click(within(dialog).getByRole("button", { name: /Save Category/i }));

    await waitFor(() => expect(screen.getAllByText("Category name already exists.")).toHaveLength(1));
  });
});
