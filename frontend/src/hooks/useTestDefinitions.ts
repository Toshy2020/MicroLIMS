import { useEffect, useState } from "react";
import { masterDataOptions } from "../services/masterDataOptions";

export interface TestDefinitionOption {
  id: number;
  code: string;
  displayName: string;
  isActive: boolean;
  workflowType: string;
}

// Backs every TestCode picker in the app (Items, Water Sampling Points,
// Room Test Configurations, Machine Part Configurations) with the one
// canonical Test Master list, instead of each screen free-typing codes
// by hand. See backend TestDefinition.cs for why this exists.
//
// `options` is the full list (active + frozen) - TestMasterPage needs
// to see everything. `activeOptions` is what the pickers should offer
// for a *new* selection; frozen tests are deliberately excluded there
// but a picker's already-selected value is looked up against the full
// `options` list so an existing assignment to a since-frozen test still
// renders correctly.
export function useTestDefinitions() {
  const [options, setOptions] = useState<TestDefinitionOption[]>([]);
  const [loading, setLoading] = useState(true);

  const reload = () => masterDataOptions.getTestDefinitions().then((data: TestDefinitionOption[]) => {
    setOptions(data);
    setLoading(false);
  });

  useEffect(() => { reload(); }, []);

  // Adds a brand-new test to the Test Master (used when the analyst
  // types a code that doesn't exist yet) and returns it so the caller
  // can select it immediately.
  const addNew = async (code: string, displayName: string) => {
    const created = await masterDataOptions.createTestDefinition(code, displayName);
    await reload();
    return created as TestDefinitionOption;
  };

  const update = async (id: number, code: string, displayName: string) => {
    const updated = await masterDataOptions.updateTestDefinition(id, code, displayName);
    await reload();
    return updated as TestDefinitionOption;
  };

  const setActive = async (id: number, isActive: boolean) => {
    if (isActive) await masterDataOptions.unfreezeTestDefinition(id);
    else await masterDataOptions.freezeTestDefinition(id);
    await reload();
  };

  return { options, activeOptions: options.filter((o) => o.isActive), loading, addNew, update, setActive, reload };
}
