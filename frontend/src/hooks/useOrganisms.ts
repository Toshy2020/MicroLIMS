import { useEffect, useState } from "react";
import { masterDataOptions } from "../services/masterDataOptions";

export interface OrganismOption {
  id: number;
  scientificName: string;
  atccNumber: string | null;
  commonName: string | null;
  description: string | null;
}

// Backs every organism picker in the app (Media Challenge Specs, Material
// form, Cryovial/Media Evaluation displays) with the one canonical
// Organism list, instead of each screen free-typing an organism name by
// hand. See backend Organism.cs for why this exists.
export function useOrganisms() {
  const [options, setOptions] = useState<OrganismOption[]>([]);
  const [loading, setLoading] = useState(true);

  const reload = () => masterDataOptions.getOrganisms().then((data: OrganismOption[]) => {
    setOptions(data);
    setLoading(false);
  });

  useEffect(() => { reload(); }, []);

  // Adds a brand-new organism to the master list (used when the analyst
  // types a scientific name that doesn't exist yet) and returns it so the
  // caller can select it immediately.
  const addNew = async (scientificName: string, atccNumber?: string | null, commonName?: string | null, description?: string | null) => {
    const created = await masterDataOptions.createOrganism(scientificName, atccNumber ?? null, commonName ?? null, description ?? null);
    await reload();
    return created as OrganismOption;
  };

  const update = async (id: number, scientificName: string, atccNumber?: string | null, commonName?: string | null, description?: string | null) => {
    const updated = await masterDataOptions.updateOrganism(id, scientificName, atccNumber ?? null, commonName ?? null, description ?? null);
    await reload();
    return updated as OrganismOption;
  };

  const remove = async (id: number) => {
    await masterDataOptions.deleteOrganism(id);
    await reload();
  };

  return { options, loading, addNew, update, remove, reload };
}
