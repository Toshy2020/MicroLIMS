import { i18n } from "../../../../i18n";

// Texts for the wizard step factories, which are plain functions (not components) and so cannot call useTranslation.
export function wizardText(key: string, options?: Record<string, unknown>): string {
  return i18n.t(key, { ns: "inventory", ...options });
}

export function typeText(type: string): string {
  return wizardText(type === "Other" ? "types.OtherMaterial" : `types.${type}`);
}
