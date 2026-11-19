import type { LaboratorySection } from "../../../../services/laboratorySectionService";

export interface StorageEquipmentOption {
  id: number;
  code: string;
  instrumentType?: string | null;
  manufacturerName?: string | null;
  location?: string | null;
  status?: string | number | null;
  sectionId?: number | null;
}

// The stored location for a material kept in a piece of equipment, e.g.
// "Refrigerator, Thermo (EQ-12)". Records saved before October 2026 used an
// em dash between type and manufacturer; those still match their equipment,
// and are only rewritten if someone changes the location.
export function equipmentLocationTag(eq: StorageEquipmentOption): string {
  return `${eq.instrumentType}, ${eq.manufacturerName} (${eq.code})`;
}

export function isEquipmentLocation(value: string | undefined, eq: StorageEquipmentOption): boolean {
  return value === equipmentLocationTag(eq) || value === `${eq.instrumentType} — ${eq.manufacturerName} (${eq.code})`;
}

export interface StorageChoices {
  activeSection: LaboratorySection | undefined;
  roomStorageOption: string | null;
  refrigerators: StorageEquipmentOption[];
  deepFreezers: StorageEquipmentOption[];
  freezers: StorageEquipmentOption[];
  selectedEquipment: StorageEquipmentOption | undefined;
  isFallbackLocation: boolean;
  hasOtherOptions: boolean;
}

// What the lot can be stored in: in-service equipment of the lot's lab, and the lab's room storage.
export function storageChoices(
  equipment: StorageEquipmentOption[],
  sections: LaboratorySection[],
  activeSectionId: number | undefined,
  location: string
): StorageChoices {
  const activeSection = sections.find((s) => s.sectionId === activeSectionId);
  const roomStorageOption = activeSection
    ? activeSection.sectionCode === "MICRO"
      ? "Microbiology Lab"
      : activeSection.sectionCode === "FP"
        ? "Physicochemical Lab"
        : activeSection.sectionName
    : null;

  // Eligible storage equipment: Status = InService (filtered by active lab when known)
  const inService = equipment.filter(
    (e) =>
      (e.status === "InService" || e.status === 0 || e.status === "0") &&
      (activeSectionId != null ? e.sectionId === activeSectionId || e.sectionId == null : true)
  );
  const typeOf = (e: StorageEquipmentOption) => (e.instrumentType || "").toLowerCase();

  const selectedEquipment = inService.find(
    (eq) => isEquipmentLocation(location, eq) || location === eq.code || (location && location.includes(eq.code))
  );
  const isFallbackLocation = Boolean(
    location && location !== roomStorageOption && !inService.some((eq) => isEquipmentLocation(location, eq))
  );

  return {
    activeSection,
    roomStorageOption,
    refrigerators: inService.filter((e) => typeOf(e).includes("refrigerator")),
    deepFreezers: inService.filter((e) => typeOf(e).includes("deep freezer")),
    freezers: inService.filter((e) => typeOf(e).includes("freezer") && !typeOf(e).includes("deep freezer")),
    selectedEquipment,
    isFallbackLocation,
    hasOtherOptions: Boolean(roomStorageOption || isFallbackLocation)
  };
}
