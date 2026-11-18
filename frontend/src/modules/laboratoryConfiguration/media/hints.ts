import type { TFunction } from "i18next";
import type { Hint } from "../../../components/FieldHint";
import { i18n } from "../../../i18n";

// Field hints for media products, incubation conditions and lot preparation.
// Wording is taken from guide chapter 22. Hints describe meaning and effect
// only; no limits, windows or recovery values.
const MEDIA_CONFIG = { chapterId: "22-gpt-media", heading: "22.1 Media Configurations" };
const MEDIA_PREPARATION = { chapterId: "22-gpt-media", heading: "22.2 Media Preparation" };

export function getMediaHints(t: TFunction<"labConfig">) {
  return {
    productCode: {
      text: t("media.hints.productCode"),
      guide: MEDIA_CONFIG
    },
    incubationMin: {
      text: t("media.hints.incubationMin"),
      guide: MEDIA_CONFIG
    },
    totalWeight: {
      text: t("media.hints.totalWeight"),
      guide: MEDIA_PREPARATION
    },
    ph: {
      text: t("media.hints.ph"),
      guide: MEDIA_PREPARATION
    }
  } satisfies Record<string, Hint>;
}

export const hints = getMediaHints(i18n.getFixedT("en", "labConfig"));
