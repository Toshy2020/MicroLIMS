import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { resetDraftDb, signInForTest } from "../../../../services/drafts/draftTestUtils";
import { changeDateField, getDateField } from "../../../../components/DateField.testUtils";
import { MaterialCategoryService } from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialCategoryService";
import { MaterialMasterService } from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService";
import { ReferenceOrganismService } from "../../../laboratoryConfiguration/masterDataSimple/services/ReferenceOrganismService";
import { MaterialService } from "../services/MaterialService";
import type { MaterialItem } from "../types/materialTypes";
import { DIALOG_MATERIAL_FIXTURES, EDITED_LOTS } from "./__fixtures__/materialPayloads";
import { MaterialWizard, type MaterialWizardProps } from "./MaterialWizard";
import { MATERIAL_REQUIRED_MESSAGE } from "./materialFormModel";
import { ECOLI_STRAIN, fillMaterial, mockMaterialLookups } from "./materialWizard.testUtils";

// Service-boundary mocks only (CLAUDE.md Rule 4.5). MaterialService has no uploadDocument here:
// the wizard must never try to upload a document while receiving a lot.
vi.mock("../services/MaterialService", () => ({
  MaterialService: { getTypeOptions: vi.fn(), getDefaultUnit: vi.fn(), getAll: vi.fn(), create: vi.fn(), update: vi.fn() }
}));
vi.mock("../../equipment/services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: { getAll: vi.fn() }
}));
vi.mock("../../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService", () => ({
  MaterialMasterService: { getAll: vi.fn() }
}));
vi.mock("../../../laboratoryConfiguration/masterDataSimple/services/MaterialCategoryService", () => ({
  MaterialCategoryService: { getAll: vi.fn() }
}));
vi.mock("../../../laboratoryConfiguration/masterDataSimple/services/ReferenceOrganismService", () => ({
  ReferenceOrganismService: { getAll: vi.fn() }
}));
vi.mock("../../../../services/laboratorySectionService", () => ({ getMySections: vi.fn() }));
vi.mock("../../../../services/masterDataOptions", () => ({ masterDataOptions: { getMediaProducts: vi.fn() } }));

type User = ReturnType<typeof userEvent.setup>;

function renderWizard(over: Partial<MaterialWizardProps> = {}) {
  const props: MaterialWizardProps = { open: true, onClose: vi.fn(), onSuccess: vi.fn(), editingItem: null, ...over };
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <MaterialWizard {...props} />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
  return props;
}

beforeEach(async () => {
  await resetDraftDb();
  signInForTest(7);
  mockMaterialLookups();
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const dialog = () => screen.getByRole("dialog");
const typeSelect = () => screen.getByRole("combobox", { name: /Material Type/ });
const next = (user: User) => user.click(within(dialog()).getByRole("button", { name: /^Next$/ }));
const unit = () => screen.getByRole("combobox", { name: /^Unit/ });

async function pick(user: User, combo: RegExp | string, option: RegExp | string) {
  await user.click(screen.getByRole("combobox", { name: combo }));
  await user.click(await screen.findByRole("option", { name: option }));
}

async function save(user: User) {
  await next(user);
  const button = await within(dialog()).findByRole("button", { name: /^Save$/ });
  await waitFor(() => expect((button as HTMLButtonElement).disabled).toBe(false));
  await user.click(button);
}

describe("MaterialWizard initial type", () => {
  it("opens a new micro lot on the lab's first type", async () => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["DehydratedMedia", "Other"], custom: [] });
    renderWizard();
    await screen.findByRole("dialog");
    await waitFor(() => expect(MaterialService.getDefaultUnit).toHaveBeenCalled());
    await waitFor(() => expect(typeSelect().textContent).toBe("Dehydrated Media"), { timeout: 4000 });
  }, 20000);

  it("offers what type-options returns plus the Other material path, never the legacy types or free text", async () => {
    const user = userEvent.setup();
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({
      builtIn: ["DehydratedMedia", "Supplement", "Chemical"],
      custom: ["Culture Collection Media"]
    });
    renderWizard();
    await screen.findByRole("dialog");
    await waitFor(() => expect(typeSelect().textContent).toBe("Dehydrated Media"), { timeout: 4000 });
    await user.click(typeSelect());
    expect(screen.getAllByRole("option").map((o) => o.textContent)).toEqual(["Dehydrated Media", "Chemical", "Other material"]);
  }, 20000);

  it("does not reset a chosen type when the user goes back to the first step", async () => {
    const user = userEvent.setup();
    renderWizard();
    await screen.findByRole("dialog");
    await waitFor(() => expect(typeSelect().textContent).toBe("Dehydrated Media"), { timeout: 4000 });
    await pick(user, /Material Type/, "Chemical");
    await waitFor(() => expect(typeSelect().textContent).toBe("Chemical"));
    await pick(user, /Master Entry/, /CHEM-001/);
    await next(user);
    await user.click(within(dialog()).getByRole("button", { name: /^Back$/ }));
    expect(typeSelect().textContent).toBe("Chemical");
    expect((screen.getByLabelText(/Material Name/) as HTMLInputElement).value).toBe("Acetone AR");
  }, 20000);
});

describe("MaterialWizard Other material receipt", () => {
  const openOther = async (user: User) => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["Other"], custom: [] });
    renderWizard();
    await screen.findByRole("dialog");
    await waitFor(() => expect(typeSelect().textContent).toBe("Other material"), { timeout: 4000 });
    await waitFor(() => expect(MaterialCategoryService.getAll).toHaveBeenCalledWith(true, 1));
    return user;
  };

  const pickCategoryAndEntry = async (user: User) => {
    await pick(user, /Material category/, "Supplement");
    await pick(user, /Material Master Entry/, /SUP-001/);
  };

  const fillLotAndGoToDates = async (user: User) => {
    await next(user);
    await user.type(screen.getByLabelText(/Batch \/ Lot No\./), "LOT-9");
    await pick(user, /Storage Location/, "Microbiology Lab");
    await user.type(screen.getByLabelText(/Quantity Received/), "5");
    await next(user);
  };

  it("prefills unit and storage condition from the category, both still editable, and marks the expiry date required", async () => {
    const user = await openOther(userEvent.setup());
    await pick(user, /Material category/, "Supplement");
    await pick(user, /Material Master Entry/, /SUP-001/);
    await next(user);

    expect(unit().textContent).toBe("Milliliter");
    const storage = screen.getByLabelText("Storage Condition") as HTMLInputElement;
    expect(storage.value).toBe("2-8 C");
    await user.clear(storage);
    await user.type(storage, "Room temperature");
    expect(storage.value).toBe("Room temperature");
    await user.type(screen.getByLabelText(/Batch \/ Lot No\./), "LOT-9");
    await pick(user, /Storage Location/, "Microbiology Lab");
    await user.type(screen.getByLabelText(/Quantity Received/), "5");
    await next(user);
    expect(getDateField(/^Expiry Date/).querySelector("input")?.required).toBe(true);
    expect(getDateField(/^Receiving Date/).querySelector("input")?.required).toBe(true);
  }, 30000);

  it("offers only the chosen category's active entries, with name and code read-only", async () => {
    const user = await openOther(userEvent.setup());
    await user.click(screen.getByRole("combobox", { name: /Material category/ }));
    await user.click(await screen.findByRole("option", { name: "Supplement" }));
    await user.click(screen.getByRole("combobox", { name: /Material Master Entry/ }));
    const listbox = await screen.findByRole("listbox");
    expect(within(listbox).getAllByRole("option")).toHaveLength(1);
    await user.click(within(listbox).getByRole("option", { name: /SUP-001/ }));

    expect(MaterialMasterService.getAll).toHaveBeenCalledWith("Other", true);
    const name = screen.getByLabelText(/Material Name/) as HTMLInputElement;
    expect(name.value).toBe("Egg Yolk Tellurite Emulsion");
    expect(name.disabled).toBe(true);
    expect((screen.getByLabelText(/Code \/ Catalog No\./) as HTMLInputElement).disabled).toBe(true);
  }, 20000);

  it("sends Other with the master entry and no custom type or typed name", async () => {
    const user = await openOther(userEvent.setup());
    await pickCategoryAndEntry(user);
    await fillLotAndGoToDates(user);
    changeDateField(/^Expiry Date/, "2030-01-31");
    await save(user);

    await waitFor(() => expect(MaterialService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(MaterialService.create).mock.calls[0][0]).toMatchObject({
      materialType: "Other",
      customType: null,
      materialMasterEntryId: 7,
      materialName: "Egg Yolk Tellurite Emulsion",
      code: "SUP-001",
      unit: "Milliliter",
      storageCondition: "2-8 C",
      expiryDate: "2030-01-31"
    });
  }, 30000);

  it("asks for the expiry date the category requires before going on, and sends nothing", async () => {
    const user = await openOther(userEvent.setup());
    await pickCategoryAndEntry(user);
    await fillLotAndGoToDates(user);
    changeDateField(/^Receiving Date/, "2026-11-02");
    await next(user);

    expect(await screen.findByText("Supplement lots need an expiry date.")).toBeTruthy();
    expect(within(dialog()).queryByRole("button", { name: /^Save$/ })).toBeNull();
    expect(MaterialService.create).not.toHaveBeenCalled();
  }, 30000);
});

describe("MaterialWizard editing a lot received before categories", () => {
  beforeEach(() => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["DehydratedMedia", "Other"], custom: [] });
  });

  const legacyItem: MaterialItem = EDITED_LOTS.legacySupplement;

  it("shows the stored type read-only and does not change the unit", async () => {
    renderWizard({ editingItem: legacyItem });
    await screen.findByRole("dialog");
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalledWith(1), { timeout: 4000 });
    const type = screen.getByLabelText(/Material Type/) as HTMLInputElement;
    expect(type.value).toBe("Supplement");
    expect(type.disabled).toBe(true);
    expect(MaterialService.getDefaultUnit).not.toHaveBeenCalled();
  }, 20000);

  it("still saves with the stored type, name and unit", async () => {
    const user = userEvent.setup();
    const props = renderWizard({ editingItem: legacyItem });
    await screen.findByRole("dialog");
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalled());
    await next(user);
    await next(user);
    await save(user);

    await waitFor(() => expect(MaterialService.update).toHaveBeenCalledTimes(1));
    const [id, payload, version] = vi.mocked(MaterialService.update).mock.calls[0];
    expect(id).toBe(5);
    expect(version).toBe(4);
    expect(payload).toMatchObject({ materialType: "Supplement", customType: null, materialName: "Old Supplement", unit: "Gram" });
    expect(props.onSuccess).toHaveBeenCalledWith("Material stock updated successfully.");
    expect(props.onClose).toHaveBeenCalled();
  }, 20000);

  it("keeps the custom type of an old Other lot when saving", async () => {
    const user = userEvent.setup();
    renderWizard({ editingItem: EDITED_LOTS.oldOtherCustomType });
    await screen.findByRole("dialog");
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalled());
    expect((screen.getByLabelText(/Material Type/) as HTMLInputElement).value).toBe("Culture Collection Media");
    await next(user);
    await next(user);
    await save(user);

    await waitFor(() => expect(MaterialService.update).toHaveBeenCalledTimes(1));
    expect(vi.mocked(MaterialService.update).mock.calls[0][1]).toMatchObject({
      materialType: "Other",
      customType: "Culture Collection Media"
    });
  }, 20000);
});

describe("MaterialWizard lyophilized microorganism receipt", () => {
  const openLyophilized = async () => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["LyophilizedMicroorganism"], custom: [] });
    renderWizard();
    await screen.findByRole("dialog");
    await waitFor(() => expect(typeSelect().textContent).toBe("Lyophilized Microorganism"), { timeout: 4000 });
    await waitFor(() => expect(ReferenceOrganismService.getAll).toHaveBeenCalled());
  };

  const fillLot = async (user: User) => {
    await user.type(screen.getByLabelText(/Batch \/ Lot No\./), "LOT-9");
    await pick(user, /Storage Location/, "Microbiology Lab");
    await user.type(screen.getByLabelText(/Quantity Received/), "1");
  };

  it("offers only active strains as 'code · name' and fills name and code read-only", async () => {
    const user = userEvent.setup();
    vi.mocked(ReferenceOrganismService.getAll).mockResolvedValue([ECOLI_STRAIN, { ...ECOLI_STRAIN, id: 12, code: "ORG-002", name: "Retired", isActive: false }]);
    await openLyophilized();
    await user.click(screen.getByRole("combobox", { name: /Reference organism/ }));
    const options = (await screen.findAllByRole("option")).map((o) => o.textContent);
    expect(options).toEqual(["ORG-001 · Escherichia coli ATCC 8739"]);
    await user.click(screen.getByRole("option"));

    const name = screen.getByLabelText(/Material Name/) as HTMLInputElement;
    expect(name.value).toBe("Escherichia coli ATCC 8739");
    expect(name.disabled).toBe(true);
    expect((screen.getByLabelText(/Code \/ Catalog No\./) as HTMLInputElement).value).toBe("ORG-001");
  }, 30000);

  it("requires a strain before going on, and sends nothing", async () => {
    const user = userEvent.setup();
    await openLyophilized();
    await next(user);

    expect(await screen.findByText("Choose the reference organism for this lyophilized microorganism.")).toBeTruthy();
    expect(screen.getByLabelText(/Material Type/)).toBeTruthy();
    expect(MaterialService.create).not.toHaveBeenCalled();
  }, 30000);

  it("sends the strain id and no organism, ATCC number or typed name", async () => {
    const user = userEvent.setup();
    await openLyophilized();
    await user.click(screen.getByRole("combobox", { name: /Reference organism/ }));
    await user.click(await screen.findByRole("option"));
    await next(user);
    await fillLot(user);
    await next(user);
    await save(user);

    await waitFor(() => expect(MaterialService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(MaterialService.create).mock.calls[0][0]).toMatchObject({
      materialType: "LyophilizedMicroorganism",
      referenceOrganismId: 11,
      organismId: null,
      atccNumber: null,
      materialName: "Escherichia coli ATCC 8739",
      code: "ORG-001"
    });
  }, 30000);

  it("still saves an old lot that has no strain, with its stored organism and ATCC number", async () => {
    const user = userEvent.setup();
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["LyophilizedMicroorganism"], custom: [] });
    renderWizard({ editingItem: EDITED_LOTS.oldLyophilized });
    await screen.findByRole("dialog");
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalled());
    expect((screen.getByLabelText(/Material Name/) as HTMLInputElement).disabled).toBe(false);
    await next(user);
    await next(user);
    await save(user);

    await waitFor(() => expect(MaterialService.update).toHaveBeenCalledTimes(1));
    expect(vi.mocked(MaterialService.update).mock.calls[0][1]).toMatchObject({
      organismId: 4,
      atccNumber: "ATCC 8739",
      referenceOrganismId: null,
      materialName: "E. coli"
    });
  }, 20000);
});

describe("MaterialWizard step validation", () => {
  it("blocks Next on the batch step with the old required-fields message, then lets the user through once filled", async () => {
    const user = userEvent.setup();
    renderWizard();
    await screen.findByRole("dialog");
    await waitFor(() => expect(MaterialService.getDefaultUnit).toHaveBeenCalled());
    await pick(user, /Material Type/, "Chemical");
    await waitFor(() => expect(typeSelect().textContent).toBe("Chemical"));
    await next(user);
    expect(await screen.findByText("Choose the material master entry for this lot.")).toBeTruthy();

    await pick(user, /Master Entry/, /CHEM-001/);
    await next(user);
    await next(user);
    expect(await screen.findByText(MATERIAL_REQUIRED_MESSAGE)).toBeTruthy();

    await user.type(screen.getByLabelText(/Batch \/ Lot No\./), "B-1");
    await pick(user, /Storage Location/, "Microbiology Lab");
    await user.type(screen.getByLabelText(/Quantity Received/), "0");
    await next(user);
    expect(await screen.findByText("Quantity received must be greater than zero.")).toBeTruthy();
    expect(MaterialService.create).not.toHaveBeenCalled();
  }, 40000);

  it("asks for the laboratory section when the user has several", async () => {
    const user = userEvent.setup();
    mockMaterialLookups([
      { sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology", departmentId: 1, departmentCode: "QC", departmentName: "Quality Control" },
      { sectionId: 2, sectionCode: "FP", sectionName: "Physicochemical", departmentId: 1, departmentCode: "QC", departmentName: "Quality Control" }
    ]);
    renderWizard();
    await screen.findByRole("dialog");
    await user.click(screen.getByRole("combobox", { name: /Laboratory Section/ }));
    await user.click(await screen.findByRole("option", { name: /Select Laboratory Section/ }));
    await next(user);
    expect(await screen.findByText("Laboratory section is required.")).toBeTruthy();
  }, 20000);
});

describe("MaterialWizard review", () => {
  it("shows the quantity with its unit and the dates as dates, then saves from Review", async () => {
    const user = userEvent.setup();
    const fixture = DIALOG_MATERIAL_FIXTURES.chemicalNewLot;
    const props = renderWizard();
    await screen.findByRole("dialog");
    await fillMaterial(user, fixture, { next: () => next(user) });
    await next(user);

    const review = within(dialog());
    expect(await review.findByText("2.5 Liter")).toBeTruthy();
    expect(review.getByText("02/11/2026")).toBeTruthy();
    expect(review.getByText("31/05/2029")).toBeTruthy();
    expect(review.getByText("Acetone AR")).toBeTruthy();
    // Receiving and expiry are calendar dates, not instants: no time zone is shown for them.
    expect(review.queryByText(/UTC|GMT/)).toBeNull();
    expect(MaterialService.create).not.toHaveBeenCalled();

    await user.click(review.getByRole("button", { name: /^Save$/ }));
    await waitFor(() => expect(MaterialService.create).toHaveBeenCalledTimes(1));
    expect(props.onSuccess).toHaveBeenCalledWith("Material added to stock as STK-91.", {
      id: 91,
      stockId: "STK-91",
      materialName: "Created lot"
    });
    expect(props.onClose).toHaveBeenCalled();
  }, 40000);

  it("shows the server's message and stays open when the save fails", async () => {
    const user = userEvent.setup();
    const { default: axios } = await import("axios");
    vi.mocked(MaterialService.create).mockRejectedValue(
      Object.assign(new axios.AxiosError("Request failed"), { response: { status: 400, data: { message: "Batch already received." }, headers: {}, config: {}, statusText: "" } })
    );
    const props = renderWizard();
    await screen.findByRole("dialog");
    await fillMaterial(user, DIALOG_MATERIAL_FIXTURES.chemicalNewLot, { next: () => next(user) });
    await save(user);

    expect(await screen.findByText("Batch already received.")).toBeTruthy();
    expect(props.onClose).not.toHaveBeenCalled();
    expect(props.onSuccess).not.toHaveBeenCalled();
  }, 40000);
});

describe("MaterialWizard documents", () => {
  it("does not upload documents while receiving a lot and says where to add them", async () => {
    const user = userEvent.setup();
    renderWizard();
    await screen.findByRole("dialog");
    await fillMaterial(user, DIALOG_MATERIAL_FIXTURES.chemicalNewLot, { next: () => next(user) });

    expect(screen.getByText(/uploaded from the lot's details and documents/)).toBeTruthy();
    expect(screen.queryByLabelText(/file/i)).toBeNull();
    await next(user);
    await user.click(within(dialog()).getByRole("button", { name: /^Save$/ }));
    await waitFor(() => expect(MaterialService.create).toHaveBeenCalledTimes(1));
    expect(Object.keys(MaterialService)).not.toContain("uploadDocument");
  }, 40000);
});

describe("MaterialWizard when lists fail to load", () => {
  it("names the failed lists instead of showing them as empty", async () => {
    vi.mocked(ReferenceOrganismService.getAll).mockRejectedValue(new Error("down"));
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["LyophilizedMicroorganism"], custom: [] });
    renderWizard();
    await screen.findByRole("dialog");
    expect(await screen.findByText(/Could not load reference organisms/)).toBeTruthy();
  }, 20000);

  it("after 409 conflict, reloads version 8 and sends version 8 on retry", async () => {
    const user = userEvent.setup();
    const conflictErr = new AxiosError("Conflict", "ERR_BAD_REQUEST", undefined, undefined, {
      status: 409,
      statusText: "Conflict",
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: { message: "Modified by another user" },
    });
    const legacyItem = EDITED_LOTS.legacySupplement;
    vi.mocked(MaterialService.update)
      .mockRejectedValueOnce(conflictErr)
      .mockResolvedValueOnce({ ...legacyItem, version: 9 });
    vi.mocked(MaterialService.getAll).mockResolvedValue([
      { ...legacyItem, version: 8, location: "Shelf Z9" },
    ]);

    const props = renderWizard({ editingItem: legacyItem });
    await screen.findByRole("dialog");
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalled());
    await next(user);
    await next(user);
    await save(user);

    await screen.findByRole("alert");
    await user.click(screen.getByRole("button", { name: "Review and save again" }));
    const saveButton = await within(dialog()).findByRole("button", { name: /^Save$/ });
    await user.click(saveButton);

    await waitFor(() => expect(MaterialService.update).toHaveBeenCalledTimes(2));
    expect(vi.mocked(MaterialService.update).mock.calls[0][2]).toBe(4);
    expect(vi.mocked(MaterialService.update).mock.calls[1][2]).toBe(8);
    expect(props.onSuccess).toHaveBeenCalledWith("Material stock updated successfully.");
  }, 20000);
});
