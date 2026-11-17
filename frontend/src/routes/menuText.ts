import { useMemo } from "react";
import { useTranslation } from "../i18n";

// Menu labels in menuConfig.ts stay English: they are the stable identity of an
// entry (open/closed state, favourites, route matching) and what English users
// read. What the user reads in another language comes from the "navigation"
// namespace, under a key made from the English label: "RM & PM Workspace" ->
// item.rmPmWorkspace. A label with no key yet simply shows in English.
export function menuKey(label: string): string {
  const words = label.replace(/[^A-Za-z0-9]+/g, " ").trim().split(" ").filter(Boolean);
  return words.map((w, i) => (i === 0 ? w.toLowerCase() : w[0].toUpperCase() + w.slice(1).toLowerCase())).join("");
}

export interface MenuText {
  label: (label: string) => string;
  group: (groupName: string) => string;
}

export function useMenuText(): MenuText {
  const { t } = useTranslation("navigation");
  return useMemo(
    () => ({
      label: (label) => t(`item.${menuKey(label)}`, { defaultValue: label }),
      group: (groupName) => t(`group.${menuKey(groupName)}`, { defaultValue: groupName })
    }),
    [t]
  );
}
