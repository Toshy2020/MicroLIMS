import type { TFunction } from "i18next";
import type { Hint } from "../../../components/FieldHint";
import { i18n } from "../../../i18n";

// Field hints for the item form (AddItemDialog). Wording is taken from guide
// chapter 31. Hints describe meaning and effect only; no limits or windows.
const ITEM_MASTER = { chapterId: "31-items-test-master-specifications", heading: "31.1 Item Master and Assigned Tests" };
const PREPARATION = {
  chapterId: "31-items-test-master-specifications",
  heading: "31.2 Item Preparation Configuration and Controlled Documents"
};

export function getItemHints(t: TFunction<"labConfig">) {
  return {
    itemCode: {
      text: t("items.hints.itemCode"),
      guide: ITEM_MASTER
    },
    sopNumber: {
      text: t("items.hints.sopNumber"),
      guide: PREPARATION
    },
    category: {
      text: t("items.hints.category"),
      guide: ITEM_MASTER
    },
    assignedTests: {
      text: t("items.hints.assignedTests"),
      guide: ITEM_MASTER
    }
  } satisfies Record<string, Hint>;
}

export const hints = getItemHints(i18n.getFixedT("en", "labConfig"));
