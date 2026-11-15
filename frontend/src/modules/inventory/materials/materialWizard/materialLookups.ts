import { useCallback, useEffect, useRef, useState } from "react";
import { useLoadFailures } from "../../../../hooks/useLoadFailures";
import { getMySections, type LaboratorySection } from "../../../../services/laboratorySectionService";
import { EquipmentInventoryService } from "../../equipment/services/EquipmentInventoryService";
import {
  MaterialCategoryService,
  type MaterialCategory
} from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialCategoryService";
import {
  MaterialMasterService,
  type MaterialMasterEntry
} from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService";
import {
  ReferenceOrganismService,
  type ReferenceOrganism
} from "../../../laboratoryConfiguration/masterDataSimple/services/ReferenceOrganismService";
import { MaterialService } from "../services/MaterialService";
import type { MaterialItem, MaterialType, MaterialUnit } from "../types/materialTypes";
import {
  createEmptyMaterialFormModel,
  fromMaterial,
  getMasterCategoryForType,
  isMasterLinkedType,
  newLotTypesFor,
  withMaterialType,
  type MaterialFormModel
} from "./materialFormModel";
import type { StorageEquipmentOption } from "./materialStorage";

export interface TypeOptions {
  builtIn: MaterialType[];
  custom: string[];
}

// What the first step is looking at: the lot's laboratory section and type decide which lists load.
export interface MaterialDemand {
  sectionId: number | null;
  type: MaterialType;
}

export interface MaterialLookups {
  // The form the wizard opens with, ready once the user's sections (and, for a lab with one
  // section, its first type and unit) are known. Null while loading.
  initial: MaterialFormModel | null;
  sections: LaboratorySection[];
  equipment: StorageEquipmentOption[];
  equipmentLoading: boolean;
  // Lists that could not be loaded; shown instead of an empty dropdown.
  failed: string[];
  typeOptions: TypeOptions | null;
  categories: MaterialCategory[];
  categoriesLoading: boolean;
  masterEntries: MaterialMasterEntry[];
  masterEntriesLoading: boolean;
  strains: ReferenceOrganism[];
  // Set once a new lot has been given its lab's first type, so going back never resets a chosen type.
  initialTypeApplied: { current: boolean };
  setDemand(demand: MaterialDemand): void;
}

// Loads everything the add / edit material wizard reads, and the lists that depend on what the
// first step shows. Moved unchanged from the old dialog's effects.
export function useMaterialLookups(open: boolean, editing: MaterialItem | null): MaterialLookups {
  const { failed, fail, reset: resetFailures } = useLoadFailures();
  const [initial, setInitial] = useState<MaterialFormModel | null>(null);
  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [equipment, setEquipment] = useState<StorageEquipmentOption[]>([]);
  const [equipmentLoading, setEquipmentLoading] = useState(false);
  const [typeOptions, setTypeOptions] = useState<TypeOptions | null>(null);
  const [categories, setCategories] = useState<MaterialCategory[]>([]);
  const [categoriesLoading, setCategoriesLoading] = useState(false);
  const [masterEntries, setMasterEntries] = useState<MaterialMasterEntry[]>([]);
  const [masterEntriesLoading, setMasterEntriesLoading] = useState(false);
  const [strains, setStrains] = useState<ReferenceOrganism[]>([]);
  const [demand, setDemandState] = useState<MaterialDemand | null>(null);
  const initialTypeApplied = useRef(false);
  const typeOptionsFor = useRef<number | null>(null);

  const setDemand = useCallback((next: MaterialDemand) => {
    setDemandState((prev) => (prev && prev.sectionId === next.sectionId && prev.type === next.type ? prev : next));
  }, []);

  // Opening: the user's sections, the storage equipment, and the form the wizard starts from.
  useEffect(() => {
    setInitial(null);
    setDemandState(null);
    initialTypeApplied.current = false;
    typeOptionsFor.current = null;
    if (!open) return;
    let canceled = false;
    resetFailures();

    setEquipmentLoading(true);
    EquipmentInventoryService.getAll()
      .then((data: StorageEquipmentOption[]) => {
        if (!canceled) setEquipment(data || []);
      })
      .catch(fail("storage equipment", () => setEquipment([])))
      .finally(() => {
        if (!canceled) setEquipmentLoading(false);
      });

    void (async () => {
      let mine: LaboratorySection[] = [];
      try {
        mine = await getMySections();
      } catch {
        fail("your laboratory sections", () => undefined)();
      }
      if (canceled) return;
      if (editing) {
        setSections(mine);
        setInitial(fromMaterial(editing));
        return;
      }
      // When creating, the initial type is the first type of the lab, not "DehydratedMedia".
      const onlySection = mine.length === 1 ? mine[0].sectionId : null;
      let model = createEmptyMaterialFormModel(new Date().toISOString().slice(0, 10), {
        sectionId: onlySection ?? ""
      });
      if (onlySection !== null) {
        let options: TypeOptions | null;
        try {
          options = await MaterialService.getTypeOptions(onlySection);
        } catch {
          options = null;
        }
        if (canceled) return;
        const first = options ? newLotTypesFor(options)[0] : undefined;
        if (options && first) {
          let defaultUnit: MaterialUnit | null = null;
          try {
            defaultUnit = (await MaterialService.getDefaultUnit(first)) as MaterialUnit;
          } catch {
            // Keep the unit already chosen.
          }
          if (canceled) return;
          model = withMaterialType(model, first, defaultUnit, false);
          initialTypeApplied.current = true;
          typeOptionsFor.current = onlySection;
          setTypeOptions(options);
        }
      }
      setSections(mine);
      setInitial(model);
    })();

    return () => {
      canceled = true;
    };
  }, [open, editing, fail, resetFailures]);

  const hasDemand = demand !== null;
  const demandSection = demand?.sectionId ?? null;
  const demandType = demand?.type ?? null;

  // Types the lab can receive.
  useEffect(() => {
    if (!open) {
      setTypeOptions(null);
      return;
    }
    if (!hasDemand) return;
    if (demandSection === null) {
      typeOptionsFor.current = null;
      setTypeOptions(null);
      return;
    }
    if (typeOptionsFor.current === demandSection) return;
    let canceled = false;
    MaterialService.getTypeOptions(demandSection)
      .then((data) => {
        if (canceled) return;
        typeOptionsFor.current = demandSection;
        setTypeOptions(data);
      })
      .catch(() => {
        if (!canceled) setTypeOptions(null);
      });
    return () => {
      canceled = true;
    };
  }, [open, hasDemand, demandSection]);

  // Other materials: the categories offered to the lot's lab (the server filters by lab and active state).
  useEffect(() => {
    if (!open || editing || demandType !== "Other" || demandSection === null) {
      setCategories([]);
      return;
    }
    let canceled = false;
    setCategoriesLoading(true);
    MaterialCategoryService.getAll(true, demandSection)
      .then((data) => {
        if (!canceled) setCategories(data);
      })
      .catch(() => {
        if (!canceled) setCategories([]);
      })
      .finally(() => {
        if (!canceled) setCategoriesLoading(false);
      });
    return () => {
      canceled = true;
    };
  }, [open, editing, demandType, demandSection]);

  // Master entries of the lot's kind. An Other lot already received shows its category and entry
  // read-only: nothing to pick.
  useEffect(() => {
    if (!open || !demandType || !isMasterLinkedType(demandType) || (demandType === "Other" && editing)) {
      setMasterEntries([]);
      return;
    }
    const category = getMasterCategoryForType(demandType);
    if (!category) return;
    let canceled = false;
    setMasterEntriesLoading(true);
    const activeOnly = !(editing && editing.materialMasterEntryId);
    MaterialMasterService.getAll(category, activeOnly)
      .then((data) => {
        if (!canceled) setMasterEntries(Array.isArray(data) ? data : []);
      })
      .catch((err) => {
        console.error("Failed to load material master entries:", err);
        if (!canceled) setMasterEntries([]);
      })
      .finally(() => {
        if (!canceled) setMasterEntriesLoading(false);
      });
    return () => {
      canceled = true;
    };
  }, [open, demandType, editing]);

  // Lyophilized lots are received against a reference strain.
  const lyophilized = demandType === "LyophilizedMicroorganism";
  useEffect(() => {
    if (!open || !lyophilized) return;
    ReferenceOrganismService.getAll()
      .then(setStrains)
      .catch(fail("reference organisms", () => setStrains([])));
  }, [open, lyophilized, fail]);

  return {
    initial,
    sections,
    equipment,
    equipmentLoading,
    failed,
    typeOptions,
    categories,
    categoriesLoading,
    masterEntries,
    masterEntriesLoading,
    strains,
    initialTypeApplied,
    setDemand
  };
}
