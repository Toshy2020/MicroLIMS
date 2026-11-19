import type { TFunction } from "i18next";
import { DEFAULT_LANGUAGE, i18n } from "../../../i18n";

// KPI names and definitions arrive from the server in English. In English the server text is
// shown as received; in another language the client translates it by the KPI's stable key and
// falls back to the server text for a key it does not know.
export function kpiName(t: TFunction, key: string, serverName: string): string {
  if (i18n.language === DEFAULT_LANGUAGE) return serverName;
  return t(`kpiNames.${key}`, { defaultValue: serverName });
}

export function kpiDefinition(t: TFunction, key: string, serverDefinition: string): string {
  if (i18n.language === DEFAULT_LANGUAGE) return serverDefinition;
  return t(`kpiDefinitions.${key}`, { defaultValue: serverDefinition });
}
