import type { SaveMaterialPayload } from "../../services/MaterialService";
import type { MaterialItem } from "../../types/materialTypes";
import type { MaterialFormModel, MaterialModelContext } from "../materialFormModel";

// Payloads captured from the OLD Add / Edit Material dialog (characterisation,
// materialPayloadFixtures.characterisation.test.tsx). Each fixture holds what the user
// ends up with in the form (model), the screen facts the save depends on (ctx), the lot being
// edited (editing, edit cases only) and the body the dialog sent to MaterialService.

export interface MaterialPayloadFixture {
  model: MaterialFormModel;
  ctx: MaterialModelContext;
  editing?: MaterialItem;
  payload: SaveMaterialPayload;
}

const single: MaterialModelContext = { isEditing: false, sectionCount: 1, category: null };
const several: MaterialModelContext = { isEditing: false, sectionCount: 2, category: null };
const editing: MaterialModelContext = { isEditing: true, sectionCount: 1, category: null };

export const RECEIVED_ON = "2026-11-02";

const base: MaterialFormModel = {
  materialType: "DehydratedMedia",
  customType: "",
  materialName: "",
  manufacturerName: "",
  batchNumber: "",
  receivingDate: RECEIVED_ON,
  expiryDate: "",
  code: "",
  location: "Microbiology Lab",
  storageCondition: "",
  quantityReceived: "",
  unit: "Gram",
  minimumStockLevel: "",
  purity: "",
  moisturePercent: "",
  atccNumber: "",
  organismId: null,
  referenceOrganismId: null,
  mediaProductId: null,
  materialMasterEntryId: null,
  categoryId: "",
  sectionId: ""
};

const basePayload: SaveMaterialPayload = {
  materialType: "DehydratedMedia",
  customType: null,
  materialName: "",
  manufacturerName: "",
  batchNumber: "",
  receivingDate: RECEIVED_ON,
  expiryDate: null,
  code: null,
  location: "Microbiology Lab",
  storageCondition: null,
  quantityReceived: 0,
  unit: "Gram",
  minimumStockLevel: null,
  atccNumber: null,
  organismId: null,
  referenceOrganismId: null,
  mediaProductId: null,
  purity: null,
  moisturePercent: null,
  materialMasterEntryId: null
};

const lot = (over: Partial<MaterialItem>): MaterialItem => ({
  id: 5,
  version: 4,
  sectionId: 1,
  materialType: "Supplement",
  customType: null,
  materialName: "Old Supplement",
  manufacturerName: "",
  batchNumber: "B1",
  receivingDate: "2026-01-01",
  expiryDate: null,
  code: "OLD-1",
  location: "Room",
  mediaProductId: null,
  organismId: null,
  atccNumber: null,
  quantityReceived: 1,
  quantityRemaining: 1,
  unit: "Gram",
  minimumStockLevel: null,
  status: "InStock",
  ...over
});

// Lots opened for editing.
export const EDITED_LOTS = {
  legacySupplement: lot({}),
  oldLyophilized: lot({
    materialType: "LyophilizedMicroorganism",
    materialName: "E. coli",
    code: "EC-1",
    organismId: 4,
    atccNumber: "ATCC 8739",
    organism: { id: 4, scientificName: "Escherichia coli" }
  }),
  oldOtherCustomType: lot({ materialType: "Other", customType: "Culture Collection Media" }),
  referenceStandardLinked: lot({
    materialType: "ReferenceStandard",
    materialName: "Caffeine RS",
    code: "RS-001",
    materialMasterEntryId: 23,
    materialMasterEntryCode: "RS-001",
    purity: 99.8,
    moisturePercent: 0.5,
    manufacturerName: "USP"
  }),
  media: lot({ materialType: "DehydratedMedia", materialName: "Tryptic Soy Agar", code: "TSA", mediaProductId: 4 })
} as const;

// Kinds a new lot can be received under, plus every edit path the dialog has.
export const DIALOG_MATERIAL_FIXTURES: Record<string, MaterialPayloadFixture> = {
  mediaNewLot: {
    model: {
      ...base,
      materialName: "Tryptic Soy Agar",
      code: "TSA",
      manufacturerName: "Oxoid",
      batchNumber: "LOT-100",
      expiryDate: "2028-01-31",
      storageCondition: "2-8 C",
      quantityReceived: "500",
      minimumStockLevel: "100",
      mediaProductId: 4
    },
    ctx: single,
    payload: {
      ...basePayload,
      materialName: "Tryptic Soy Agar",
      code: "TSA",
      manufacturerName: "Oxoid",
      batchNumber: "LOT-100",
      expiryDate: "2028-01-31",
      storageCondition: "2-8 C",
      quantityReceived: 500,
      minimumStockLevel: 100,
      mediaProductId: 4
    }
  },
  lyophilizedNewLot: {
    model: {
      ...base,
      materialType: "LyophilizedMicroorganism",
      materialName: "Escherichia coli ATCC 8739",
      code: "ORG-001",
      batchNumber: "LYO-7",
      storageCondition: "-20 C",
      quantityReceived: "2",
      unit: "Vial",
      referenceOrganismId: 11
    },
    ctx: single,
    payload: {
      ...basePayload,
      materialType: "LyophilizedMicroorganism",
      materialName: "Escherichia coli ATCC 8739",
      code: "ORG-001",
      batchNumber: "LYO-7",
      storageCondition: "-20 C",
      quantityReceived: 2,
      unit: "Vial",
      referenceOrganismId: 11
    }
  },
  chemicalNewLot: {
    model: {
      ...base,
      materialType: "Chemical",
      materialName: "Acetone AR",
      code: "CHEM-001",
      manufacturerName: "Merck",
      batchNumber: "SUPPLIER-77",
      expiryDate: "2029-05-31",
      quantityReceived: "2.5",
      unit: "Liter",
      materialMasterEntryId: 21
    },
    ctx: single,
    payload: {
      ...basePayload,
      materialType: "Chemical",
      materialName: "Acetone AR",
      code: "CHEM-001",
      manufacturerName: "Merck",
      batchNumber: "SUPPLIER-77",
      expiryDate: "2029-05-31",
      quantityReceived: 2.5,
      unit: "Liter",
      materialMasterEntryId: 21
    }
  },
  indicatorNewLot: {
    model: {
      ...base,
      materialType: "Indicator",
      materialName: "Methyl Red",
      code: "IND-001",
      batchNumber: "IND-5",
      quantityReceived: "100",
      unit: "Milliliter",
      materialMasterEntryId: 22
    },
    ctx: single,
    payload: {
      ...basePayload,
      materialType: "Indicator",
      materialName: "Methyl Red",
      code: "IND-001",
      batchNumber: "IND-5",
      quantityReceived: 100,
      unit: "Milliliter",
      materialMasterEntryId: 22
    }
  },
  referenceStandardNewLot: {
    model: {
      ...base,
      materialType: "ReferenceStandard",
      materialName: "Caffeine RS",
      code: "RS-001",
      manufacturerName: "USP",
      batchNumber: "RS-LOT-3",
      quantityReceived: "250",
      purity: "99.8",
      moisturePercent: "0.5",
      materialMasterEntryId: 23
    },
    ctx: single,
    payload: {
      ...basePayload,
      materialType: "ReferenceStandard",
      materialName: "Caffeine RS",
      code: "RS-001",
      manufacturerName: "USP",
      batchNumber: "RS-LOT-3",
      quantityReceived: 250,
      purity: 99.8,
      moisturePercent: 0.5,
      materialMasterEntryId: 23
    }
  },
  primaryStandardNewLot: {
    model: {
      ...base,
      materialType: "PrimaryStandard",
      materialName: "Potassium Hydrogen Phthalate",
      code: "PS-001",
      batchNumber: "PS-LOT-1",
      quantityReceived: "50",
      purity: "99.95",
      materialMasterEntryId: 24
    },
    ctx: single,
    payload: {
      ...basePayload,
      materialType: "PrimaryStandard",
      materialName: "Potassium Hydrogen Phthalate",
      code: "PS-001",
      batchNumber: "PS-LOT-1",
      quantityReceived: 50,
      purity: 99.95,
      materialMasterEntryId: 24
    }
  },
  otherNewLot: {
    model: {
      ...base,
      materialType: "Other",
      materialName: "Egg Yolk Tellurite Emulsion",
      code: "SUP-001",
      batchNumber: "EY-9",
      expiryDate: "2030-01-31",
      storageCondition: "2-8 C",
      quantityReceived: "5",
      unit: "Milliliter",
      materialMasterEntryId: 7,
      categoryId: 3
    },
    ctx: { ...single, category: { name: "Supplement", expiryRequired: true } },
    payload: {
      ...basePayload,
      materialType: "Other",
      materialName: "Egg Yolk Tellurite Emulsion",
      code: "SUP-001",
      batchNumber: "EY-9",
      expiryDate: "2030-01-31",
      storageCondition: "2-8 C",
      quantityReceived: 5,
      unit: "Milliliter",
      materialMasterEntryId: 7
    }
  },
  chemicalInSecondSection: {
    model: {
      ...base,
      materialType: "Chemical",
      materialName: "Acetone AR",
      code: "CHEM-001",
      batchNumber: "FP-12",
      location: "Physicochemical Lab",
      quantityReceived: "4",
      materialMasterEntryId: 21,
      sectionId: 2
    },
    ctx: several,
    payload: {
      ...basePayload,
      materialType: "Chemical",
      materialName: "Acetone AR",
      code: "CHEM-001",
      batchNumber: "FP-12",
      location: "Physicochemical Lab",
      quantityReceived: 4,
      materialMasterEntryId: 21,
      sectionId: 2
    }
  },
  editLegacySupplement: {
    editing: EDITED_LOTS.legacySupplement,
    model: {
      ...base,
      materialType: "Supplement",
      materialName: "Old Supplement",
      code: "OLD-1",
      batchNumber: "B1-R",
      receivingDate: "2026-01-01",
      expiryDate: "2027-12-31",
      location: "Room",
      storageCondition: "Keep dry",
      quantityReceived: "3"
    },
    ctx: editing,
    payload: {
      ...basePayload,
      materialType: "Supplement",
      materialName: "Old Supplement",
      code: "OLD-1",
      batchNumber: "B1-R",
      receivingDate: "2026-01-01",
      expiryDate: "2027-12-31",
      location: "Room",
      storageCondition: "Keep dry",
      quantityReceived: 3
    }
  },
  editOldLyophilized: {
    editing: EDITED_LOTS.oldLyophilized,
    model: {
      ...base,
      materialType: "LyophilizedMicroorganism",
      materialName: "E. coli",
      code: "EC-1",
      batchNumber: "B1",
      receivingDate: "2026-01-01",
      location: "Room",
      quantityReceived: "1",
      atccNumber: "ATCC 8739",
      organismId: 4
    },
    ctx: editing,
    payload: {
      ...basePayload,
      materialType: "LyophilizedMicroorganism",
      materialName: "E. coli",
      code: "EC-1",
      batchNumber: "B1",
      receivingDate: "2026-01-01",
      location: "Room",
      quantityReceived: 1,
      atccNumber: "ATCC 8739",
      organismId: 4
    }
  },
  editOldOtherCustomType: {
    editing: EDITED_LOTS.oldOtherCustomType,
    model: {
      ...base,
      materialType: "Other",
      customType: "Culture Collection Media",
      materialName: "Old Supplement",
      code: "OLD-1",
      batchNumber: "B1",
      receivingDate: "2026-01-01",
      location: "Room",
      quantityReceived: "1"
    },
    ctx: editing,
    payload: {
      ...basePayload,
      materialType: "Other",
      customType: "Culture Collection Media",
      materialName: "Old Supplement",
      code: "OLD-1",
      batchNumber: "B1",
      receivingDate: "2026-01-01",
      location: "Room",
      quantityReceived: 1
    }
  },
  editReferenceStandardLinked: {
    editing: EDITED_LOTS.referenceStandardLinked,
    model: {
      ...base,
      materialType: "ReferenceStandard",
      materialName: "Caffeine RS",
      code: "RS-001",
      manufacturerName: "USP",
      batchNumber: "B1",
      receivingDate: "2026-01-01",
      location: "Room",
      quantityReceived: "1",
      purity: "99.8",
      moisturePercent: "0.5",
      materialMasterEntryId: 23
    },
    ctx: editing,
    payload: {
      ...basePayload,
      materialType: "ReferenceStandard",
      materialName: "Caffeine RS",
      code: "RS-001",
      manufacturerName: "USP",
      batchNumber: "B1",
      receivingDate: "2026-01-01",
      location: "Room",
      quantityReceived: 1,
      purity: 99.8,
      moisturePercent: 0.5,
      materialMasterEntryId: 23
    }
  },
  editMedia: {
    editing: EDITED_LOTS.media,
    model: {
      ...base,
      materialName: "Tryptic Soy Agar",
      code: "TSA",
      batchNumber: "B1",
      receivingDate: "2026-01-01",
      location: "Room",
      quantityReceived: "1",
      mediaProductId: 4
    },
    ctx: editing,
    payload: {
      ...basePayload,
      materialName: "Tryptic Soy Agar",
      code: "TSA",
      batchNumber: "B1",
      receivingDate: "2026-01-01",
      location: "Room",
      quantityReceived: 1,
      mediaProductId: 4
    }
  }
};
