import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AddMaterialDialog } from "./AddMaterialDialog";
import { MaterialService } from "../services/MaterialService";
import { MaterialCategoryService } from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialCategoryService";
import { MaterialMasterService } from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService";
import { ReferenceOrganismService } from "../../../laboratoryConfiguration/masterDataSimple/services/ReferenceOrganismService";
import type { MaterialItem } from "../types/materialTypes";

vi.mock("../services/MaterialService", () => ({
  MaterialService: {
    getTypeOptions: vi.fn(),
    getDefaultUnit: vi.fn(),
    create: vi.fn(),
    update: vi.fn()
  }
}));
vi.mock("../../equipment/services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: { getAll: () => Promise.resolve([]) }
}));
vi.mock("../../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService", () => ({
  MaterialMasterService: { getAll: vi.fn() }
}));
vi.mock("../../../laboratoryConfiguration/masterDataSimple/services/MaterialCategoryService", () => ({
  MaterialCategoryService: { getAll: vi.fn() }
}));
vi.mock("../../../../services/laboratorySectionService", () => ({
  getMySections: () => Promise.resolve([{ sectionId: 1, sectionCode: "MICRO", sectionName: "Micro" }])
}));
vi.mock("../../../../components/MediaProductPicker", () => ({ MediaProductPicker: () => null }));
vi.mock("../../../laboratoryConfiguration/masterDataSimple/services/ReferenceOrganismService", () => ({
  ReferenceOrganismService: { getAll: vi.fn() }
}));
// The calendar picker is covered elsewhere; a plain input keeps these tests about the receipt rules.
vi.mock("../../../../components/DateField", () => ({
  DateField: ({ label, value, onChange, required }: { label?: string; value: string; onChange: (v: string) => void; required?: boolean }) => (
    <input aria-label={label} data-required={required ? "yes" : "no"} value={value} onChange={(e) => onChange(e.target.value)} />
  )
}));

const supplementCategory = {
  id: 3, name: "Supplement", codePrefix: "SUP", defaultUnit: "Milliliter", coaRequired: true, expiryRequired: true,
  quantityTracked: true, defaultStorageCondition: "2-8 C", approvalBeforeUse: true, labelRequired: false,
  isActive: true, sectionIds: [], hasEntries: true
} as const;

const otherEntry = {
  id: 7, sectionId: 1, code: "SUP-001", name: "Egg Yolk Tellurite Emulsion", category: "Other", materialCategoryId: 3,
  materialCategoryName: "Supplement", baseUnit: "Piece", isActive: true
} as const;
const otherCategoryEntry = { ...otherEntry, id: 8, code: "FLT-001", name: "Filter Membrane 0.45", materialCategoryId: 9, materialCategoryName: "Filter" } as const;

const legacyItem = {
  id: 5, sectionId: 1, materialType: "Supplement", customType: null, materialName: "Old Supplement", manufacturerName: "",
  batchNumber: "B1", receivingDate: "2026-01-01", expiryDate: null, code: "OLD-1", location: "Room",
  mediaProductId: null, organismId: null, atccNumber: null, quantityReceived: 1, quantityRemaining: 1,
  unit: "Gram", minimumStockLevel: null, status: "InStock", version: 4
} as MaterialItem;

const ecoliStrain = {
  id: 11, code: "ORG-001", organismId: 4, organismScientificName: "Escherichia coli", cultureCollection: "ATCC", strainNumber: "8739",
  name: "Escherichia coli ATCC 8739", ownerSectionId: 1, isActive: true, canEdit: true, hasStock: false
} as const;
const retiredStrain = { ...ecoliStrain, id: 12, code: "ORG-002", name: "Escherichia coli NCTC 9001", cultureCollection: "NCTC", strainNumber: "9001", isActive: false } as const;

beforeEach(() => {
  vi.mocked(ReferenceOrganismService.getAll).mockResolvedValue([ecoliStrain as never, retiredStrain as never]);
  vi.mocked(MaterialService.getDefaultUnit).mockResolvedValue("Gram");
  vi.mocked(MaterialService.create).mockResolvedValue({});
  vi.mocked(MaterialService.update).mockResolvedValue({});
  vi.mocked(MaterialCategoryService.getAll).mockResolvedValue([supplementCategory as never]);
  vi.mocked(MaterialMasterService.getAll).mockResolvedValue([otherEntry as never, otherCategoryEntry as never]);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const typeSelect = () => screen.getByRole("combobox", { name: /Material Type/ });

describe("AddMaterialDialog initial type", () => {
  it("opens a new micro lot on the lab's first type", async () => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["DehydratedMedia", "Other"], custom: [] });
    render(<AddMaterialDialog open onClose={vi.fn()} onSuccess={vi.fn()} editingItem={null} />);
    await waitFor(() => expect(MaterialService.getDefaultUnit).toHaveBeenCalled());
    await waitFor(() => expect(typeSelect().textContent).toBe("Dehydrated Media"), { timeout: 4000 });
  }, 20000);

  it("offers what type-options returns plus the Other material path, never the legacy types or free text", async () => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({
      builtIn: ["DehydratedMedia", "Supplement", "Chemical"] as never[], custom: ["Culture Collection Media"]
    });
    render(<AddMaterialDialog open onClose={vi.fn()} onSuccess={vi.fn()} editingItem={null} />);
    await waitFor(() => expect(typeSelect().textContent).toBe("Dehydrated Media"), { timeout: 4000 });
    await userEvent.click(typeSelect());
    const options = screen.getAllByRole("option").map((o) => o.textContent);
    expect(options).toEqual(["Dehydrated Media", "Chemical", "Other material"]);
  }, 20000);
});

describe("AddMaterialDialog Other material receipt", () => {
  const fillAndSave = async () => {
    await userEvent.type(screen.getByLabelText(/Batch \/ Lot No\./), "LOT-9");
    await userEvent.click(screen.getByRole("combobox", { name: /Storage Location/ }));
    await userEvent.click(await screen.findByRole("option", { name: "Microbiology Lab" }));
    await userEvent.type(screen.getByLabelText(/Quantity Received/), "5");
    await userEvent.click(screen.getByRole("button", { name: "Add to Stock" }));
  };

  const openOther = async () => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["Other"], custom: [] });
    render(<AddMaterialDialog open onClose={vi.fn()} onSuccess={vi.fn()} editingItem={null} />);
    await waitFor(() => expect(typeSelect().textContent).toBe("Other material"), { timeout: 4000 });
    await waitFor(() => expect(MaterialCategoryService.getAll).toHaveBeenCalledWith(true, 1));
  };

  const pickCategoryAndEntry = async () => {
    await userEvent.click(screen.getByRole("combobox", { name: /Material category/ }));
    await userEvent.click(await screen.findByRole("option", { name: "Supplement" }));
    await userEvent.click(screen.getByRole("combobox", { name: /Material Master Entry/ }));
    await userEvent.click(await screen.findByRole("option", { name: /SUP-001/ }));
  };

  it("prefills unit and storage condition from the category, both still editable", async () => {
    await openOther();
    await userEvent.click(screen.getByRole("combobox", { name: /Material category/ }));
    await userEvent.click(await screen.findByRole("option", { name: "Supplement" }));

    expect(screen.getByRole("combobox", { name: /Unit/ }).textContent).toBe("Milliliter");
    const storage = screen.getByLabelText("Storage Condition") as HTMLInputElement;
    expect(storage.value).toBe("2-8 C");
    await userEvent.clear(storage);
    await userEvent.type(storage, "Room temperature");
    expect(storage.value).toBe("Room temperature");
    // The category requires an expiry date; the picker is marked required.
    expect(screen.getByLabelText("Expiry Date").getAttribute("data-required")).toBe("yes");
  }, 20000);

  it("offers only the chosen category's active entries, with name and code read-only", async () => {
    await openOther();
    await userEvent.click(screen.getByRole("combobox", { name: /Material category/ }));
    await userEvent.click(await screen.findByRole("option", { name: "Supplement" }));
    await userEvent.click(screen.getByRole("combobox", { name: /Material Master Entry/ }));
    const listbox = await screen.findByRole("listbox");
    expect(within(listbox).getAllByRole("option")).toHaveLength(1);
    await userEvent.click(within(listbox).getByRole("option", { name: /SUP-001/ }));

    expect(MaterialMasterService.getAll).toHaveBeenCalledWith("Other", true);
    const name = screen.getByLabelText(/Material Name/) as HTMLInputElement;
    expect(name.value).toBe("Egg Yolk Tellurite Emulsion");
    expect(name.disabled).toBe(true);
    expect((screen.getByLabelText(/Code \/ Catalog No\./) as HTMLInputElement).disabled).toBe(true);
  }, 20000);

  it("sends Other with the master entry and no custom type or typed name", async () => {
    await openOther();
    await pickCategoryAndEntry();
    await userEvent.type(screen.getByLabelText("Expiry Date"), "2030-01-31");
    await fillAndSave();

    await waitFor(() => expect(MaterialService.create).toHaveBeenCalledTimes(1));
    const payload = vi.mocked(MaterialService.create).mock.calls[0][0];
    expect(payload).toMatchObject({
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

  it("asks for the expiry date the category requires before sending", async () => {
    await openOther();
    await pickCategoryAndEntry();
    await fillAndSave();

    expect(await screen.findByText("Supplement lots need an expiry date.")).toBeTruthy();
    expect(MaterialService.create).not.toHaveBeenCalled();
  }, 30000);
});

describe("AddMaterialDialog editing a lot received before categories", () => {
  beforeEach(() => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["DehydratedMedia", "Other"], custom: [] });
  });

  it("shows the stored type read-only and does not change the unit", async () => {
    render(<AddMaterialDialog open onClose={vi.fn()} onSuccess={vi.fn()} editingItem={legacyItem} />);
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalledWith(1), { timeout: 4000 });
    const type = screen.getByLabelText(/Material Type/) as HTMLInputElement;
    expect(type.value).toBe("Supplement");
    expect(type.disabled).toBe(true);
    expect(MaterialService.getDefaultUnit).not.toHaveBeenCalled();
  }, 20000);

  it("still saves with the stored type, name and unit", async () => {
    const onSuccess = vi.fn();
    render(<AddMaterialDialog open onClose={vi.fn()} onSuccess={onSuccess} editingItem={legacyItem} />);
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalled());
    await userEvent.click(screen.getByRole("button", { name: "Save Changes" }));

    await waitFor(() => expect(MaterialService.update).toHaveBeenCalledTimes(1));
    const [id, payload, version] = vi.mocked(MaterialService.update).mock.calls[0];
    expect(id).toBe(5);
    expect(version).toBe(4);
    expect(payload).toMatchObject({ materialType: "Supplement", customType: null, materialName: "Old Supplement", unit: "Gram" });
    expect(onSuccess).toHaveBeenCalled();
  }, 20000);

  it("keeps the custom type of an old Other lot when saving", async () => {
    const item = { ...legacyItem, materialType: "Other", customType: "Culture Collection Media" } as MaterialItem;
    render(<AddMaterialDialog open onClose={vi.fn()} onSuccess={vi.fn()} editingItem={item} />);
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalled());
    expect((screen.getByLabelText(/Material Type/) as HTMLInputElement).value).toBe("Culture Collection Media");
    await userEvent.click(screen.getByRole("button", { name: "Save Changes" }));

    await waitFor(() => expect(MaterialService.update).toHaveBeenCalledTimes(1));
    expect(vi.mocked(MaterialService.update).mock.calls[0][1]).toMatchObject({ materialType: "Other", customType: "Culture Collection Media" });
  }, 20000);
});

describe("AddMaterialDialog lyophilized microorganism receipt", () => {
  const openLyophilized = async () => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["LyophilizedMicroorganism"], custom: [] });
    render(<AddMaterialDialog open onClose={vi.fn()} onSuccess={vi.fn()} editingItem={null} />);
    await waitFor(() => expect(typeSelect().textContent).toBe("Lyophilized Microorganism"), { timeout: 4000 });
    await waitFor(() => expect(ReferenceOrganismService.getAll).toHaveBeenCalled());
  };

  const fillAndSave = async () => {
    await userEvent.type(screen.getByLabelText(/Batch \/ Lot No\./), "LOT-9");
    await userEvent.click(screen.getByRole("combobox", { name: /Storage Location/ }));
    await userEvent.click(await screen.findByRole("option", { name: "Microbiology Lab" }));
    await userEvent.type(screen.getByLabelText(/Quantity Received/), "1");
    await userEvent.click(screen.getByRole("button", { name: "Add to Stock" }));
  };

  it("offers only active strains as 'code · name' and fills name and code read-only", async () => {
    await openLyophilized();
    await userEvent.click(screen.getByRole("combobox", { name: /Reference organism/ }));
    const options = (await screen.findAllByRole("option")).map((o) => o.textContent);
    expect(options).toEqual(["ORG-001 · Escherichia coli ATCC 8739"]);
    await userEvent.click(screen.getByRole("option"));

    const name = screen.getByLabelText(/Material Name/) as HTMLInputElement;
    expect(name.value).toBe("Escherichia coli ATCC 8739");
    expect(name.disabled).toBe(true);
    expect((screen.getByLabelText(/Code \/ Catalog No\./) as HTMLInputElement).value).toBe("ORG-001");
  }, 30000);

  it("requires a strain before sending", async () => {
    await openLyophilized();
    await fillAndSave();

    expect(await screen.findByText("Choose the reference organism for this lyophilized microorganism.")).toBeTruthy();
    expect(MaterialService.create).not.toHaveBeenCalled();
  }, 30000);

  it("sends the strain id and no organism, ATCC number or typed name", async () => {
    await openLyophilized();
    await userEvent.click(screen.getByRole("combobox", { name: /Reference organism/ }));
    await userEvent.click(await screen.findByRole("option"));
    await fillAndSave();

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
    const item = {
      ...legacyItem, materialType: "LyophilizedMicroorganism", materialName: "E. coli", code: "EC-1",
      organismId: 4, atccNumber: "ATCC 8739", organism: { id: 4, scientificName: "Escherichia coli" }
    } as MaterialItem;
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["LyophilizedMicroorganism"], custom: [] });
    render(<AddMaterialDialog open onClose={vi.fn()} onSuccess={vi.fn()} editingItem={item} />);
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalled());
    expect((screen.getByLabelText(/Material Name/) as HTMLInputElement).disabled).toBe(false);
    await userEvent.click(screen.getByRole("button", { name: "Save Changes" }));

    await waitFor(() => expect(MaterialService.update).toHaveBeenCalledTimes(1));
    expect(vi.mocked(MaterialService.update).mock.calls[0][1]).toMatchObject({
      organismId: 4, atccNumber: "ATCC 8739", referenceOrganismId: null, materialName: "E. coli"
    });
  }, 20000);
});
