import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { MaterialCategoriesDialog } from "./MaterialCategoriesDialog";
import { MaterialCategoryService, MaterialCategory } from "./services/MaterialCategoryService";

let canManage = true;
vi.mock("../../../contexts/AuthContext", () => ({
  useHasPermission: () => canManage,
  useAuth: () => ({ fullName: "Head One", username: "head", role: "SectionHead" })
}));
vi.mock("./services/MaterialCategoryService", () => ({
  MaterialCategoryService: { getAll: vi.fn(), create: vi.fn(), update: vi.fn(), setActive: vi.fn() }
}));

const sections = [
  { sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology", departmentId: 1, departmentCode: "QC", departmentName: "QC" },
  { sectionId: 2, sectionCode: "FP", sectionName: "Physicochemical", departmentId: 1, departmentCode: "QC", departmentName: "QC" }
];

const category = (over: Partial<MaterialCategory>): MaterialCategory => ({
  id: 1, name: "Supplement", codePrefix: "SUP", defaultUnit: "Milliliter", coaRequired: true, expiryRequired: true,
  quantityTracked: true, defaultStorageCondition: "2-8 C", approvalBeforeUse: true, labelRequired: false,
  isActive: true, sectionIds: [1], hasEntries: false, ...over
});

const renderDialog = (onChanged = vi.fn()) =>
  render(
    <PinnedLightTheme>
      <MaterialCategoriesDialog open sections={sections} onClose={vi.fn()} onChanged={onChanged} />
    </PinnedLightTheme>
  );

beforeEach(() => {
  canManage = true;
  vi.mocked(MaterialCategoryService.getAll).mockResolvedValue([
    category({ id: 1, hasEntries: false }),
    category({ id: 2, name: "Filter Membrane", codePrefix: "FLT", coaRequired: false, approvalBeforeUse: false, sectionIds: [], hasEntries: true })
  ]);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("MaterialCategoriesDialog", () => {
  it("lists categories with their rules and sections", async () => {
    renderDialog();
    expect(await screen.findByText("Filter Membrane")).toBeTruthy();
    expect(screen.getAllByText("COA required")).toHaveLength(1);
    expect(screen.getAllByText("Approval before use")).toHaveLength(1);
    expect(screen.getByText("All sections")).toBeTruthy();
    expect(screen.getByText("Microbiology")).toBeTruthy();
  });

  it("hides every write action without MaterialCategories.Manage", async () => {
    canManage = false;
    renderDialog();
    await screen.findByText("Filter Membrane");
    expect(screen.queryByRole("button", { name: /Add Category/ })).toBeNull();
  });

  it("creates a category only after a reason and an electronic signature", async () => {
    vi.mocked(MaterialCategoryService.create).mockResolvedValue(category({ id: 3, name: "PPE", codePrefix: "PPE" }));
    const onChanged = vi.fn();
    renderDialog(onChanged);
    await screen.findByText("Filter Membrane");

    await userEvent.click(screen.getByRole("button", { name: /Add Category/ }));
    const next = () => screen.getByRole("button", { name: "Continue to sign" }) as HTMLButtonElement;
    await userEvent.type(screen.getByLabelText(/^Name/), "PPE");
    await userEvent.type(screen.getByLabelText(/^Code prefix/), "ppe");
    // Name and prefix are not enough: the reason is required too.
    expect(next().disabled).toBe(true);
    await userEvent.type(screen.getByLabelText(/^Reason/), "New consumable class");
    expect(next().disabled).toBe(false);
    await userEvent.click(next());

    expect(await screen.findByText("Electronic Signature")).toBeTruthy();
    expect(MaterialCategoryService.create).not.toHaveBeenCalled();
    await userEvent.type(screen.getByLabelText(/Password/), "secret");
    await userEvent.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => expect(MaterialCategoryService.create).toHaveBeenCalledTimes(1));
    expect(MaterialCategoryService.create).toHaveBeenCalledWith(
      expect.objectContaining({ name: "PPE", codePrefix: "PPE", sectionIds: null, reason: "New consumable class", password: "secret" })
    );
    await waitFor(() => expect(onChanged).toHaveBeenCalled());
  }, 20000);

  it("rejects a prefix that is not 2 to 5 capital letters without asking for a signature", async () => {
    renderDialog();
    await screen.findByText("Filter Membrane");
    await userEvent.click(screen.getByRole("button", { name: /Add Category/ }));
    await userEvent.type(screen.getByLabelText(/^Name/), "PPE");
    await userEvent.type(screen.getByLabelText(/^Code prefix/), "P");
    await userEvent.type(screen.getByLabelText(/^Reason/), "Because");
    await userEvent.click(screen.getByRole("button", { name: "Continue to sign" }));

    expect(await screen.findByText("Use 2 to 5 capital letters.")).toBeTruthy();
    expect(screen.queryByText("Electronic Signature")).toBeNull();
  });

  it("freezes the prefix of a category that has master entries", async () => {
    renderDialog();
    await screen.findByText("Filter Membrane");

    // Filter Membrane has entries.
    await userEvent.click(screen.getByText("Filter Membrane"));
    const prefix = screen.getByLabelText(/^Code prefix/) as HTMLInputElement;
    expect(prefix.value).toBe("FLT");
    expect(prefix.disabled).toBe(true);
    expect(screen.getByText(/can no longer change/)).toBeTruthy();
  });

  it("lets the prefix of a category without entries be edited", async () => {
    renderDialog();
    await screen.findByText("Filter Membrane");
    await userEvent.click(screen.getByText("Supplement"));
    expect((screen.getByLabelText(/^Code prefix/) as HTMLInputElement).disabled).toBe(false);
  });
});
