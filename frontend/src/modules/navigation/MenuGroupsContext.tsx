import { createContext, useContext, type ReactNode } from "react";
import type { MenuGroup } from "../../routes/menuConfig";

// The signed-in user's visible menu, supplied once by MainLayout so favourites
// (star, search, sidebar) resolve against exactly what the user may open.
// Outside the layout (isolated tests) there is no menu and so no favourites.
const MenuGroupsContext = createContext<MenuGroup[] | null>(null);

export function MenuGroupsProvider({ groups, children }: { groups: MenuGroup[]; children: ReactNode }) {
  return <MenuGroupsContext.Provider value={groups}>{children}</MenuGroupsContext.Provider>;
}

export function useProvidedMenuGroups(): MenuGroup[] | null {
  return useContext(MenuGroupsContext);
}
