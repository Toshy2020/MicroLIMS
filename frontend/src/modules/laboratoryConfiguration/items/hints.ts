import type { Hint } from "../../../components/FieldHint";

// Field hints for the item form (AddItemDialog). Wording is taken from guide
// chapter 31. Hints describe meaning and effect only; no limits or windows.
const ITEM_MASTER = { chapterId: "31-items-test-master-specifications", heading: "31.1 Item Master and Assigned Tests" };
const PREPARATION = {
  chapterId: "31-items-test-master-specifications",
  heading: "31.2 Item Preparation Configuration and Controlled Documents"
};

export const hints = {
  itemCode: {
    text: "The unique code of the item. Item codes must be unique, and the comparison is case-sensitive.",
    guide: ITEM_MASTER
  },
  sopNumber: {
    text: "Optional. The number of the item's Standard Operating Procedure, if it has one.",
    guide: PREPARATION
  },
  category: {
    text: "Product, Raw Material or Packaging Material. Items are configured only for these categories; water, environmental monitoring, after cleaning and GPT have their own configuration pages.",
    guide: ITEM_MASTER
  },
  assignedTests: {
    text: "The tests assigned to this item. When a sample of this item is received, a test order is created for each of these tests that applies to the sample; tests declared not applicable are left out.",
    guide: ITEM_MASTER
  }
} satisfies Record<string, Hint>;
