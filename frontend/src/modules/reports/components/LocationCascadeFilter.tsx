import { useEffect, useState } from "react";
import { FormControl, InputLabel, Select, MenuItem } from "@mui/material";
import { masterDataOptions } from "../../../services/masterDataOptions";

// Only these three categories have a real Department/Area -> Sampling
// Point hierarchy in the schema:
//   Water:                  WaterDepartment    -> WaterSamplingPoint
//   EnvironmentalMonitoring: Department         -> Room
//   AfterCleaning:           Machine            -> MachinePart
// Product/RawMaterial/PackagingMaterial/GPT have no equivalent grouping
// entity - the flat Product/Item picker remains their only filter.
export type LocationHierarchyCategory = "Water" | "EnvironmentalMonitoring" | "AfterCleaning";

export function hasLocationHierarchy(category: string | null | undefined): category is LocationHierarchyCategory {
  return category === "Water" || category === "EnvironmentalMonitoring" || category === "AfterCleaning";
}

// "Machine" deliberately plays the "Department/Area" role for After
// Cleaning - a mapping decision, not a schema gap (see the 2026-08-22
// schema verification). Labels are category-specific so the slot never
// reads as a mislabeled "Department" when it's actually showing a
// machine name.
const LEVEL_LABELS: Record<LocationHierarchyCategory, { level2: string; level3: string }> = {
  Water: { level2: "Water Department", level3: "Sampling Point" },
  EnvironmentalMonitoring: { level2: "Department", level3: "Room" },
  AfterCleaning: { level2: "Machine", level3: "Machine Part" }
};

interface Level3Item {
  id: number;
  name: string;
}

interface Level2Group {
  id: number;
  name: string;
  items: Level3Item[];
}

// The ResultRecord.SubjectName each category's leaf actually gets
// projected as (ResultProjectionService) - WaterSamplingPoint.Code,
// Room.Name, MachinePart.Name - so the cascade's final selection is
// exactly what /reporting/trend's subjectName param already expects.
async function loadHierarchy(category: LocationHierarchyCategory): Promise<Level2Group[]> {
  if (category === "Water") {
    const depts = await masterDataOptions.getWaterDepartments();
    return depts.map((d: any) => ({
      id: d.id,
      name: d.name,
      items: (d.samplingPoints ?? []).map((p: any) => ({ id: p.id, name: p.code }))
    }));
  }
  if (category === "EnvironmentalMonitoring") {
    const depts = await masterDataOptions.getDepartments();
    return depts.map((d: any) => ({
      id: d.id,
      name: d.name,
      items: (d.rooms ?? []).map((r: any) => ({ id: r.id, name: r.name }))
    }));
  }
  const machines = await masterDataOptions.getMachines();
  return machines.map((m: any) => ({
    id: m.id,
    name: m.name,
    items: (m.parts ?? []).map((p: any) => ({ id: p.id, name: p.name }))
  }));
}

interface LocationCascadeFilterProps {
  category: LocationHierarchyCategory;
  subjectName: string;
  onSubjectNameChange: (name: string) => void;
}

// Two cascading selects (Level 2 -> Level 3) sourced from the existing
// master-data endpoints, each of which already returns its full
// two-level tree in one call - fetched once per category and cascaded
// client-side, no new endpoints. The Level 3 selection's name becomes
// the caller's subjectName, the same value the flat Product/Item picker
// sets for categories that don't have this hierarchy.
export function LocationCascadeFilter({ category, subjectName, onSubjectNameChange }: LocationCascadeFilterProps) {
  const [groups, setGroups] = useState<Level2Group[]>([]);
  const [level2Id, setLevel2Id] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;
    setGroups([]);
    setLevel2Id(null);

    loadHierarchy(category)
      .then((loaded) => {
        if (cancelled) return;
        setGroups(loaded);

        const alreadyValid = loaded.some((g) => g.items.some((i) => i.name === subjectName));
        if (alreadyValid) {
          const owningGroup = loaded.find((g) => g.items.some((i) => i.name === subjectName));
          if (owningGroup) setLevel2Id(owningGroup.id);
          return;
        }

        // Current subjectName doesn't belong to this category's
        // hierarchy (fresh mount, or just switched category) - default
        // to the first department/area that actually has a location
        // configured, same "default to first option" pattern the
        // Parameter/Test and Product/Item pickers already use.
        const firstWithItems = loaded.find((g) => g.items.length > 0);
        if (firstWithItems) {
          setLevel2Id(firstWithItems.id);
          onSubjectNameChange(firstWithItems.items[0].name);
        }
      })
      .catch(() => {});

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [category]);

  const selectedGroup = groups.find((g) => g.id === level2Id) ?? null;
  const labels = LEVEL_LABELS[category];

  return (
    <>
      <FormControl fullWidth size="small">
        <InputLabel>{labels.level2}</InputLabel>
        <Select
          label={labels.level2}
          value={level2Id ?? ""}
          onChange={(e) => {
            const id = Number(e.target.value);
            setLevel2Id(id);
            const group = groups.find((g) => g.id === id);
            if (group && group.items[0]) onSubjectNameChange(group.items[0].name);
          }}
        >
          {groups.map((g) => (
            <MenuItem key={g.id} value={g.id}>{g.name}</MenuItem>
          ))}
        </Select>
      </FormControl>

      <FormControl fullWidth size="small">
        <InputLabel>{labels.level3}</InputLabel>
        <Select
          label={labels.level3}
          value={subjectName}
          onChange={(e) => onSubjectNameChange(e.target.value)}
        >
          {(selectedGroup?.items ?? []).map((item) => (
            <MenuItem key={item.id} value={item.name}>{item.name}</MenuItem>
          ))}
        </Select>
      </FormControl>
    </>
  );
}
