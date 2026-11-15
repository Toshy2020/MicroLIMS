import type {
  CreateEquipmentRequest,
  UpdateEquipmentRequest
} from "../../services/EquipmentConfigurationService";
import type { EquipmentFormModel, EquipmentModelContext } from "../equipmentFormModel";

// Payloads captured from the OLD Add/Edit Equipment dialog (characterisation,
// equipmentPayloadFixtures.characterisation.test.tsx). Each fixture holds what
// the user enters (model), the screen facts the save depends on (ctx) and the
// body the dialog sent to EquipmentConfigurationService.

export interface EquipmentPayloadFixture {
  model: EquipmentFormModel;
  ctx: EquipmentModelContext;
  payload: CreateEquipmentRequest | UpdateEquipmentRequest;
}

const single: EquipmentModelContext = { isEditing: false, sectionCount: 1 };
const several: EquipmentModelContext = { isEditing: false, sectionCount: 2 };
const editing: EquipmentModelContext = { isEditing: true, sectionCount: 1 };

const base: EquipmentFormModel = {
  name: "",
  code: "",
  type: "Incubator",
  location: "",
  vendor: "",
  cdsSoftware: "",
  connectionSettings: "",
  setPointTemperature: "",
  calibrationDueDate: "",
  equipmentInventoryId: null,
  sectionId: ""
};

// Types the Add/Edit dialog can reach (Micro laboratory types).
export const DIALOG_EQUIPMENT_FIXTURES = {
  incubatorWithSetPoint: {
    model: {
      ...base,
      name: "Incubator 2",
      code: "INC-02",
      type: "Incubator",
      location: "Room 102",
      vendor: "Binder",
      setPointTemperature: "32.5",
      calibrationDueDate: "2027-03-15",
      equipmentInventoryId: 42
    },
    ctx: single,
    payload: {
      name: "Incubator 2",
      code: "INC-02",
      type: "Incubator",
      location: "Room 102",
      vendor: "Binder",
      cdsSoftware: null,
      connectionSettings: null,
      setPointTemperature: 32.5,
      calibrationDueDate: "2027-03-15",
      equipmentInventoryId: 42
    }
  },
  autoclave: {
    model: {
      ...base,
      name: "Autoclave 1",
      code: "AC-01",
      type: "Autoclave",
      location: "Media prep",
      vendor: "Tuttnauer",
      connectionSettings: "COM3"
    },
    ctx: single,
    payload: {
      name: "Autoclave 1",
      code: "AC-01",
      type: "Autoclave",
      location: "Media prep",
      vendor: "Tuttnauer",
      cdsSoftware: null,
      connectionSettings: "COM3",
      setPointTemperature: null,
      calibrationDueDate: null,
      equipmentInventoryId: null
    }
  },
  lafCabinet: {
    model: { ...base, name: "LAF 1", code: "LAF-01", type: "LafCabinet", calibrationDueDate: "2027-01-31" },
    ctx: single,
    payload: {
      name: "LAF 1",
      code: "LAF-01",
      type: "LafCabinet",
      location: null,
      vendor: null,
      cdsSoftware: null,
      connectionSettings: null,
      setPointTemperature: null,
      calibrationDueDate: "2027-01-31",
      equipmentInventoryId: null
    }
  },
  biologicalSafetyCabinet: {
    model: { ...base, name: "BSC 1", code: "BSC-01", type: "BiologicalSafetyCabinet", vendor: "Esco" },
    ctx: single,
    payload: {
      name: "BSC 1",
      code: "BSC-01",
      type: "BiologicalSafetyCabinet",
      location: null,
      vendor: "Esco",
      cdsSoftware: null,
      connectionSettings: null,
      setPointTemperature: null,
      calibrationDueDate: null,
      equipmentInventoryId: null
    }
  },
  waterBathWithSetPoint: {
    model: { ...base, name: "Water bath 1", code: "WB-01", type: "WaterBath", setPointTemperature: "45" },
    ctx: single,
    payload: {
      name: "Water bath 1",
      code: "WB-01",
      type: "WaterBath",
      location: null,
      vendor: null,
      cdsSoftware: null,
      connectionSettings: null,
      setPointTemperature: 45,
      calibrationDueDate: null,
      equipmentInventoryId: null
    }
  },
  otherMinimal: {
    model: { ...base, name: "  Stirrer  ", code: " STR-01 ", type: "Other" },
    ctx: single,
    payload: {
      name: "Stirrer",
      code: "STR-01",
      type: "Other",
      location: null,
      vendor: null,
      cdsSoftware: null,
      connectionSettings: null,
      setPointTemperature: null,
      calibrationDueDate: null,
      equipmentInventoryId: null
    }
  },
  incubatorSeveralSections: {
    model: { ...base, name: "Incubator 3", code: "INC-03", type: "Incubator", sectionId: 2 },
    ctx: several,
    payload: {
      name: "Incubator 3",
      code: "INC-03",
      type: "Incubator",
      location: null,
      vendor: null,
      cdsSoftware: null,
      connectionSettings: null,
      setPointTemperature: null,
      calibrationDueDate: null,
      equipmentInventoryId: null,
      sectionId: 2
    }
  },
  // Edit of the seeded Incubator 1 (id 10, version 3): name, vendor and set
  // point changed; the section is not sent on edit.
  incubatorEdit: {
    model: {
      ...base,
      name: "Incubator 1A",
      code: "INC-01",
      type: "Incubator",
      location: "Room 101",
      vendor: "Memmert",
      setPointTemperature: "35",
      sectionId: 1
    },
    ctx: editing,
    payload: {
      name: "Incubator 1A",
      code: "INC-01",
      type: "Incubator",
      location: "Room 101",
      vendor: "Memmert",
      cdsSoftware: null,
      connectionSettings: null,
      setPointTemperature: 35,
      calibrationDueDate: null,
      equipmentInventoryId: null
    }
  }
} satisfies Record<string, EquipmentPayloadFixture>;

// FP-laboratory types are not offered by this dialog (they are configured on
// the FP Instruments page), so these cannot be driven through the old screen.
// They pin the model's rule: CDS software is sent only for HPLC.
export const MODEL_ONLY_EQUIPMENT_FIXTURES = {
  hplcWithCds: {
    model: {
      ...base,
      name: "HPLC 1",
      code: "HPLC-01",
      type: "Hplc",
      cdsSoftware: "WatersEmpower3",
      connectionSettings: "192.168.1.50",
      vendor: "Waters"
    },
    ctx: single,
    payload: {
      name: "HPLC 1",
      code: "HPLC-01",
      type: "Hplc",
      location: null,
      vendor: "Waters",
      cdsSoftware: "WatersEmpower3",
      connectionSettings: "192.168.1.50",
      setPointTemperature: null,
      calibrationDueDate: null,
      equipmentInventoryId: null
    }
  },
  balance: {
    model: { ...base, name: "Balance 1", code: "BAL-01", type: "Balance", calibrationDueDate: "2027-02-01" },
    ctx: single,
    payload: {
      name: "Balance 1",
      code: "BAL-01",
      type: "Balance",
      location: null,
      vendor: null,
      cdsSoftware: null,
      connectionSettings: null,
      setPointTemperature: null,
      calibrationDueDate: "2027-02-01",
      equipmentInventoryId: null
    }
  }
} satisfies Record<string, EquipmentPayloadFixture>;
