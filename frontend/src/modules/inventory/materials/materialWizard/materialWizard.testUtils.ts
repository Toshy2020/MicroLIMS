import { expect, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import type userEvent from "@testing-library/user-event";
import { changeDateField } from "../../../../components/DateField.testUtils";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { getMySections, type LaboratorySection } from "../../../../services/laboratorySectionService";
import { EquipmentInventoryService } from "../../equipment/services/EquipmentInventoryService";
import { MaterialCategoryService, type MaterialCategory } from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialCategoryService";
import {
  MaterialMasterService,
  type MaterialMasterEntry
} from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService";
import { ReferenceOrganismService } from "../../../laboratoryConfiguration/masterDataSimple/services/ReferenceOrganismService";
import { MaterialService } from "../services/MaterialService";
import { MATERIAL_TYPE_LABELS, type MaterialType } from "../types/materialTypes";
import type { MaterialPayloadFixture } from "./__fixtures__/materialPayloads";

export type User = ReturnType<typeof userEvent.setup>;

// How the driver moves between the groups of fields: the old dialog shows them all at once,
// the wizard needs Next between them.
export interface Navigation {
  next(): Promise<void>;
}

export const micro: LaboratorySection = {
  sectionId: 1,
  sectionCode: "MICRO",
  sectionName: "Microbiology",
  departmentId: 1,
  departmentCode: "QC",
  departmentName: "Quality Control"
};
export const physchem: LaboratorySection = {
  ...micro,
  sectionId: 2,
  sectionCode: "FP",
  sectionName: "Physicochemical"
};

const entry = (over: Partial<MaterialMasterEntry>): MaterialMasterEntry => ({
  id: 0,
  sectionId: 1,
  code: "",
  name: "",
  category: "Reagent",
  baseUnit: "Gram",
  isActive: true,
  ...over
});

export const MASTER_ENTRIES: MaterialMasterEntry[] = [
  entry({ id: 21, code: "CHEM-001", name: "Acetone AR", category: "Reagent", baseUnit: "Gram" }),
  entry({ id: 22, code: "IND-001", name: "Methyl Red", category: "Indicator", baseUnit: "Milliliter" }),
  entry({ id: 23, code: "RS-001", name: "Caffeine RS", category: "ReferenceStandard", baseUnit: "Gram" }),
  entry({ id: 24, code: "PS-001", name: "Potassium Hydrogen Phthalate", category: "PrimaryStandard", baseUnit: "Gram" }),
  entry({
    id: 7,
    code: "SUP-001",
    name: "Egg Yolk Tellurite Emulsion",
    category: "Other",
    materialCategoryId: 3,
    materialCategoryName: "Supplement",
    baseUnit: "Piece"
  })
];

export const SUPPLEMENT_CATEGORY: MaterialCategory = {
  id: 3,
  name: "Supplement",
  codePrefix: "SUP",
  defaultUnit: "Milliliter",
  coaRequired: true,
  expiryRequired: true,
  quantityTracked: true,
  defaultStorageCondition: "2-8 C",
  approvalBeforeUse: true,
  labelRequired: false,
  isActive: true,
  sectionIds: [],
  hasEntries: true
};

export const ECOLI_STRAIN = {
  id: 11,
  code: "ORG-001",
  organismId: 4,
  organismScientificName: "Escherichia coli",
  cultureCollection: "ATCC",
  strainNumber: "8739",
  name: "Escherichia coli ATCC 8739",
  ownerSectionId: 1,
  isActive: true,
  canEdit: true,
  hasStock: false
} as const;

const ALL_TYPES: MaterialType[] = [
  "DehydratedMedia",
  "LyophilizedMicroorganism",
  "Chemical",
  "Indicator",
  "ReferenceStandard",
  "PrimaryStandard",
  "Other"
];

// Service-boundary answers for every list the material dialog and wizard load.
export function mockMaterialLookups(sections: LaboratorySection[] = [micro]) {
  vi.mocked(getMySections).mockResolvedValue(sections);
  vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ALL_TYPES, custom: [] });
  vi.mocked(MaterialService.getDefaultUnit).mockResolvedValue("Gram");
  vi.mocked(MaterialService.create).mockResolvedValue({ id: 91, stockId: "STK-91", materialName: "Created lot" });
  vi.mocked(MaterialService.update).mockResolvedValue({});
  vi.mocked(EquipmentInventoryService.getAll).mockResolvedValue([]);
  vi.mocked(MaterialMasterService.getAll).mockImplementation(async (category) =>
    MASTER_ENTRIES.filter((e) => e.category === category)
  );
  vi.mocked(MaterialCategoryService.getAll).mockResolvedValue([SUPPLEMENT_CATEGORY]);
  vi.mocked(ReferenceOrganismService.getAll).mockResolvedValue([ECOLI_STRAIN]);
  vi.mocked(masterDataOptions.getMediaProducts).mockResolvedValue([
    {
      id: 4,
      name: "Tryptic Soy Agar",
      code: "TSA",
      isActive: true,
      hasStock: false,
      configurationCount: 0,
      batchCount: 0,
      incubationConditionCount: 0
    }
  ]);
}

async function setText(user: User, label: RegExp | string, value: string) {
  const input = screen.getByLabelText(label);
  await user.clear(input);
  if (value) await user.type(input, value);
}

async function choose(user: User, combo: RegExp | string, option: RegExp | string) {
  await user.click(screen.getByRole("combobox", { name: combo }));
  await user.click(await screen.findByRole("option", { name: option }));
}

const typeCombo = () => screen.getByRole("combobox", { name: /Material Type/ });

async function chooseType(user: User, type: MaterialType) {
  const label = type === "Other" ? "Other material" : MATERIAL_TYPE_LABELS[type];
  if (typeCombo().textContent === label) return;
  await choose(user, /Material Type/, label);
  await waitFor(() => expect(typeCombo().textContent).toBe(label));
}

async function fillMaterialGroup(user: User, fixture: MaterialPayloadFixture) {
  const { model, ctx } = fixture;
  if (ctx.isEditing) return;
  if (ctx.sectionCount > 1) {
    const defaultUnitCalls = vi.mocked(MaterialService.getDefaultUnit).mock.calls.length;
    await choose(user, /Laboratory Section/, /Physicochemical/);
    await waitFor(() => expect(MaterialService.getDefaultUnit).toHaveBeenCalledTimes(defaultUnitCalls + 1), { timeout: 4000 });
  } else {
    await waitFor(() => expect(MaterialService.getDefaultUnit).toHaveBeenCalled(), { timeout: 4000 });
  }
  await chooseType(user, model.materialType);

  if (model.materialType === "DehydratedMedia") await choose(user, /Media Product/, /TSA/);
  if (model.materialType === "LyophilizedMicroorganism") await choose(user, /Reference organism/, /ORG-001/);
  if (model.materialType === "Other") await choose(user, /Material category/, "Supplement");
  if (model.materialMasterEntryId !== null) {
    const picked = MASTER_ENTRIES.find((e) => e.id === model.materialMasterEntryId);
    await choose(user, /Master Entry/, new RegExp(picked?.code ?? "$^"));
  }
  if (model.manufacturerName) await setText(user, "Manufacturer", model.manufacturerName);
  if (model.purity) await setText(user, /^Purity/, String(model.purity));
  if (model.moisturePercent) await setText(user, /^Moisture Content/, String(model.moisturePercent));
}

async function fillLotGroup(user: User, fixture: MaterialPayloadFixture) {
  const { model, ctx } = fixture;
  await setText(user, /Batch \/ Lot No\./, model.batchNumber);
  if (!ctx.isEditing) await choose(user, /Storage Location/, model.location);
  await setText(user, /Quantity Received/, String(model.quantityReceived));
  const unit = screen.getByRole("combobox", { name: /^Unit/ });
  if (!unit.textContent?.includes(model.unit)) await choose(user, /^Unit/, model.unit);
  await setText(user, /Min\. Stock Level/, String(model.minimumStockLevel));
  await setText(user, /^Storage Condition/, model.storageCondition);
}

function fillDatesGroup(fixture: MaterialPayloadFixture) {
  const { model } = fixture;
  changeDateField(/^Receiving Date/, model.receivingDate);
  changeDateField(/^Expiry Date/, model.expiryDate);
}

// Enters the fixture's values group by group; stops after the last group.
export async function fillMaterial(user: User, fixture: MaterialPayloadFixture, nav: Navigation) {
  await fillMaterialGroup(user, fixture);
  await nav.next();
  await fillLotGroup(user, fixture);
  await nav.next();
  fillDatesGroup(fixture);
}
