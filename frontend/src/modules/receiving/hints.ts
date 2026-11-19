import type { TFunction } from "i18next";
import type { Hint } from "../../components/FieldHint";
import { i18n } from "../../i18n";

// Field hints for the sample details correction dialog. Wording is taken from
// guide chapters 11 and 12. Hints describe meaning and effect only.
const RECEIVING = {
  chapterId: "12-sample-receiving",
  heading: "12.1 Receiving Product, Raw Material, and Packaging Material Samples"
};
const CORRECTION = { chapterId: "11-sample-management", heading: "11.3 Correcting Sample Details" };

export function getReceivingHints(t: TFunction<"receiving">): Record<"batchNumber" | "controlNumber" | "storageTime" | "reasonForCorrection", Hint> {
  return {
    batchNumber: {
      text: t("editSample.hints.batchNumber"),
      guide: RECEIVING
    },
    controlNumber: {
      text: t("editSample.hints.controlNumber"),
      guide: RECEIVING
    },
    storageTime: {
      text: t("editSample.hints.storageTime"),
      guide: CORRECTION
    },
    reasonForCorrection: {
      text: t("editSample.hints.reasonForCorrection"),
      guide: CORRECTION
    }
  };
}

// The English hints: the shared hint checks (FieldHint.test.tsx) read every
// module's `hints` export, and English is the source wording. Screens call
// getReceivingHints(t) so the hint follows the interface language.
export const hints = getReceivingHints(i18n.getFixedT("en", "receiving"));
