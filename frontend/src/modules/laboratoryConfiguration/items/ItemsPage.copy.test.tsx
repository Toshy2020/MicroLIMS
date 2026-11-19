import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { ItemsPage } from "./ItemsPage";
import { ItemService, type Item } from "./services/ItemService";
import { ItemDocumentService } from "./services/ItemDocumentService";
import { ItemPreparationConfigurationService } from "../../testPreparation/services/ItemPreparationConfigurationService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { documentControlService } from "../../documentControl/services/documentControlService";

// Only the HTTP service layer is mocked; the real ItemsPage, AddItemDialog
// and AuthProvider run (CLAUDE.md Rule 4.5).
vi.mock("./services/ItemService", () => ({
  ItemService: { getAll: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), freeze: vi.fn(), unfreeze: vi.fn() }
}));

vi.mock("./services/ItemDocumentService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./services/ItemDocumentService")>();
  return { ...actual, ItemDocumentService: { getDocumentsForItem: vi.fn() } };
});

vi.mock("../../testPreparation/services/ItemPreparationConfigurationService", () => ({
  ItemPreparationConfigurationService: { get: vi.fn(), getUnmapped: vi.fn() }
}));

vi.mock("../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/masterDataOptions")>();
  return {
    ...actual,
    masterDataOptions: { ...actual.masterDataOptions, getTestDefinitions: vi.fn() }
  };
});

vi.mock("../../documentControl/services/documentControlService", () => ({
  documentControlService: { getLibrary: vi.fn() }
}));

function seedToken(permissions: string[]) {
  const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "SectionHead", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const sourceItem: Item = {
  id: 42,
  version: 1,
  name: "Paracetamol Granules",
  code: "RM-PARA",
  category: "RawMaterial",
  sopNumber: "SOP-RM-042",
  isActive: true,
  assignedTests: [
    { testCode: "PH", displayName: "pH" },
    { testCode: "TAMC", displayName: "Total Aerobic Microbial Count" }
  ],
  specifications: [],
  minSampleQuantity: 25,
  minRetestSampleQuantity: 50,
  minSampleQuantityUnit: "g"
};

const createdItem: Item = {
  ...sourceItem,
  id: 99,
  name: "Paracetamol Granules Grade B",
  code: "RM-PARA-B",
  sopNumber: "SOP-RM-099"
};

function renderItemsPage() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={["/laboratory-configuration/items"]}>
          <ItemsPage />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

describe("ItemsPage — New from existing", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedToken(["Items.Manage"]);
    vi.mocked(ItemService.getAll).mockResolvedValue([sourceItem]);
    vi.mocked(ItemService.create).mockResolvedValue(createdItem);
    vi.mocked(ItemDocumentService.getDocumentsForItem).mockResolvedValue([]);
    vi.mocked(ItemPreparationConfigurationService.get).mockResolvedValue(null);
    vi.mocked(ItemPreparationConfigurationService.getUnmapped).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([
      { id: 1, code: "PH", displayName: "pH", isActive: true, workflowType: "Measurement" },
      { id: 2, code: "TAMC", displayName: "Total Aerobic Microbial Count", isActive: true, workflowType: "CountTest" }
    ]);
    vi.mocked(documentControlService.getLibrary).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0
    });
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("New from existing item form shows the two tick boxes and empty code/name/SOP and sends copyFrom", async () => {
    const user = userEvent.setup();
    renderItemsPage();

    await user.click(await screen.findByRole("button", { name: "New from existing" }));

    const dialog = screen.getByRole("dialog");
    const ui = within(dialog);

    const copySpecsBox = ui.getByRole("checkbox", { name: "Copy specifications" }) as HTMLInputElement;
    const copyTestsBox = ui.getByRole("checkbox", { name: "Copy auto-assigned tests" }) as HTMLInputElement;
    expect(copySpecsBox).toBeTruthy();
    expect(copyTestsBox).toBeTruthy();

    // Pick the existing source item.
    await user.click(ui.getByRole("combobox", { name: "Copy from existing item" }));
    await user.click(await screen.findByRole("option", { name: "Paracetamol Granules (RM-PARA)" }));

    // Code, name and SOP number must remain empty; other item fields are pre-filled.
    const nameInput = ui.getByRole("textbox", { name: /Item Name/ }) as HTMLInputElement;
    const codeInput = ui.getByRole("textbox", { name: /Item Code/ }) as HTMLInputElement;
    const sopInput = ui.getByRole("combobox", { name: "SOP Number" }) as HTMLInputElement;
    expect(nameInput.value).toBe("");
    expect(codeInput.value).toBe("");
    expect(sopInput.value).toBe("");

    expect((ui.getByRole("spinbutton", { name: "Min Sample Qty" }) as HTMLInputElement).value).toBe("25");
    expect((ui.getByRole("spinbutton", { name: "Min Retest Qty" }) as HTMLInputElement).value).toBe("50");
    expect((ui.getByRole("textbox", { name: "Quantity Unit" }) as HTMLInputElement).value).toBe("g");

    // Toggle Copy specifications off, keep Copy auto-assigned tests on.
    expect(copySpecsBox.checked).toBe(true);
    expect(copyTestsBox.checked).toBe(true);
    await user.click(copySpecsBox);
    expect(copySpecsBox.checked).toBe(false);

    await user.type(nameInput, "Paracetamol Granules Grade B");
    await user.type(codeInput, "RM-PARA-B");
    await user.type(sopInput, "SOP-RM-099");

    await user.click(ui.getByRole("button", { name: "Save Item" }));

    await waitFor(() => expect(ItemService.create).toHaveBeenCalledTimes(1));
    expect(ItemService.create).toHaveBeenCalledWith({
      name: "Paracetamol Granules Grade B",
      code: "RM-PARA-B",
      category: "RawMaterial",
      sopNumber: "SOP-RM-099",
      assignedTests: [],
      minSampleQuantity: 25,
      minRetestSampleQuantity: 50,
      minSampleQuantityUnit: "g",
      copyFrom: {
        sourceItemId: 42,
        specifications: false,
        autoAssignedTests: true
      }
    });
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });

  it("requires an assigned test when Copy auto-assigned tests is unticked, and sends false flags when both are unticked", async () => {
    const user = userEvent.setup();
    renderItemsPage();

    await user.click(await screen.findByRole("button", { name: "New from existing" }));
    const ui = within(screen.getByRole("dialog"));

    await user.click(ui.getByRole("combobox", { name: "Copy from existing item" }));
    await user.click(await screen.findByRole("option", { name: "Paracetamol Granules (RM-PARA)" }));

    await user.click(ui.getByRole("checkbox", { name: "Copy specifications" }));
    await user.click(ui.getByRole("checkbox", { name: "Copy auto-assigned tests" }));

    await user.type(ui.getByRole("textbox", { name: /Item Name/ }), "Paracetamol Granules Grade C");
    await user.type(ui.getByRole("textbox", { name: /Item Code/ }), "RM-PARA-C");
    await user.click(ui.getByRole("button", { name: "Save Item" }));

    expect(await ui.findByText("Item Name, Item Code, and at least one assigned test are required.")).toBeTruthy();
    expect(ItemService.create).not.toHaveBeenCalled();

    await user.click(ui.getByRole("combobox", { name: "Assigned Tests" }));
    await user.click(await screen.findByRole("option", { name: "PH: pH" }));
    await user.click(ui.getByRole("button", { name: "Save Item" }));

    await waitFor(() => expect(ItemService.create).toHaveBeenCalledTimes(1));
    expect(ItemService.create).toHaveBeenCalledWith({
      name: "Paracetamol Granules Grade C",
      code: "RM-PARA-C",
      category: "RawMaterial",
      sopNumber: "",
      assignedTests: [{ testCode: "PH", displayName: "PH" }],
      minSampleQuantity: 25,
      minRetestSampleQuantity: 50,
      minSampleQuantityUnit: "g",
      copyFrom: {
        sourceItemId: 42,
        specifications: false,
        autoAssignedTests: false
      }
    });
  });

  it("hides New from existing when the user cannot manage items", async () => {
    seedToken(["tests.not-applicable.final-approve"]);
    renderItemsPage();

    await screen.findByText("Paracetamol Granules");
    expect(screen.queryByRole("button", { name: "New from existing" })).toBeNull();
  });
});
