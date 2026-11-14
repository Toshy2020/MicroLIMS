import type { Hint } from "../../../components/FieldHint";

// Field hints for media products, incubation conditions and lot preparation.
// Wording is taken from guide chapter 22. Hints describe meaning and effect
// only; no limits, windows or recovery values.
const MEDIA_CONFIG = { chapterId: "22-gpt-media", heading: "22.1 Media Configurations" };
const MEDIA_PREPARATION = { chapterId: "22-gpt-media", heading: "22.2 Media Preparation" };

export const hints = {
  productCode: {
    text: "The product code starts every lot number of this medium. Codes must be unique; upper and lower case are treated as the same.",
    guide: MEDIA_CONFIG
  },
  incubationMin: {
    text: "The shortest incubation time of this condition. A condition pairs a time range with a temperature range, and a condition that is already in use cannot be changed.",
    guide: MEDIA_CONFIG
  },
  totalWeight: {
    text: "The weight of dehydrated media used to make this lot. The weight is deducted from the selected stock material when the lot is saved.",
    guide: MEDIA_PREPARATION
  },
  ph: {
    text: "The pH of the prepared lot, entered by the analyst. The form needs a value before you can save.",
    guide: MEDIA_PREPARATION
  }
} satisfies Record<string, Hint>;
