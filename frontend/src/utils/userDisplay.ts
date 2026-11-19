import type { TFunction } from "i18next";
import { i18n } from "../i18n";

// The job title shown beside a user's name when an administrator has not set
// one: the role, prefixed with the laboratory when the user belongs to
// exactly one.
//
// roleName is the role's own name (Role.Name), which an administrator can
// rename or create. It is shown as it is, except for the built-in names,
// which keep the laboratory-prefixed wording. `role` is the base type the
// token carries; it only decides the label when no name is known yet (a
// session stored before role names were sent).
const BUILT_IN_ROLE_NAME_TYPES = new Map<string, string>([
  ["Analyst", "Analyst"],
  ["Section Head", "SectionHead"],
  ["Reviewer", "Reviewer"],
  ["System Administrator", "SystemAdministrator"],
  ["QC Manager", "QcManager"],
  ["QA", "Qa"]
]);

const ROLE_LABEL_KEYS = new Map<string, string>([
  ["Analyst", "roles.Analyst"],
  ["Reviewer", "roles.Reviewer"],
  ["SectionHead", "roles.SectionHead"],
  ["QcManager", "roles.QcManager"],
  ["Qa", "roles.Qa"],
  ["QA", "roles.Qa"],
  ["SystemAdministrator", "roles.SystemAdministrator"]
]);

export function formatRoleName(role: string | null | undefined, roleName?: string | null, t?: TFunction): string {
  const translate = t ?? i18n.getFixedT("en", "common");
  const custom = roleName?.trim();
  if (custom) {
    const key = ROLE_LABEL_KEYS.get(custom);
    return key ? translate(key, { ns: "common" }) : custom;
  }
  if (!role) return "";
  const key = ROLE_LABEL_KEYS.get(role);
  return key ? translate(key, { ns: "common" }) : role;
}

export function formatRoleFallback(role: string | null, labCodes: string[], roleName?: string | null, t?: TFunction): string {
  const translate = t ?? i18n.getFixedT("en", "common");
  const name = roleName?.trim();
  if (name && !BUILT_IN_ROLE_NAME_TYPES.has(name)) return name;
  const baseType = name ? BUILT_IN_ROLE_NAME_TYPES.get(name)! : role;
  if (!baseType) return translate("roles.Staff", { ns: "common" });
  const lab = labCodes.length === 1 ? labCodes[0] : "";
  switch (baseType) {
    case "Analyst":
      if (lab === "MICRO") return translate("roles.MicrobiologyAnalyst", { ns: "common" });
      if (lab === "FP") return translate("roles.PhysicochemicalAnalyst", { ns: "common" });
      return translate("roles.Analyst", { ns: "common" });
    case "SectionHead":
      if (lab === "MICRO") return translate("roles.MicrobiologySectionHead", { ns: "common" });
      if (lab === "FP") return translate("roles.PhysicochemicalSectionHead", { ns: "common" });
      return translate("roles.SectionHead", { ns: "common" });
    case "Reviewer": return translate("roles.QualityReviewer", { ns: "common" });
    case "SystemAdministrator": return translate("roles.SystemAdministrator", { ns: "common" });
    case "QcManager": return translate("roles.QcManager", { ns: "common" });
    case "Qa": return translate("roles.Qa", { ns: "common" });
    default: return baseType;
  }
}

export function userInitials(fullName: string | null, username: string | null): string {
  const source = (fullName ?? username ?? "").trim();
  if (!source) return "U";
  const parts = source.split(/\s+/);
  const letters = parts.length > 1 ? parts[0].charAt(0) + parts[parts.length - 1].charAt(0) : parts[0].charAt(0);
  return letters.toUpperCase();
}
