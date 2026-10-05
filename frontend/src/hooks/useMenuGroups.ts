import { useMemo } from "react";
import { useAuth } from "../contexts/AuthContext";
import { getGroupedMenu } from "../routes/menuConfig";
import { useMyLabs } from "./useMyLabs";

// The sidebar menu for the signed-in user: permissions, lab membership and
// (for the physicochemical lab) the areas the user may open.
export function useMenuGroups() {
  const { role, permissions } = useAuth();
  const { codes: labCodes, physchemAreas } = useMyLabs();
  return useMemo(
    () => getGroupedMenu({ role, permissions, labCodes, physchemAreas }),
    [role, permissions, labCodes, physchemAreas]
  );
}
