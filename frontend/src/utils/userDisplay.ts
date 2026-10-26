// The job title shown beside a user's name when an administrator has not set
// one: the role, prefixed with the laboratory when the user belongs to
// exactly one.
//
// roleName is the role's own name (Role.Name), which an administrator can
// rename or create. It is shown as it is, except for the four built-in names,
// which keep the laboratory-prefixed wording. `role` is the base type the
// token carries; it only decides the label when no name is known yet (a
// session stored before role names were sent).
const BUILT_IN_ROLE_NAME_TYPES = new Map<string, string>([
  ["Analyst", "Analyst"],
  ["Section Head", "SectionHead"],
  ["Reviewer", "Reviewer"],
  ["System Administrator", "SystemAdministrator"]
]);

export function formatRoleFallback(role: string | null, labCodes: string[], roleName?: string | null): string {
  const name = roleName?.trim();
  if (name && !BUILT_IN_ROLE_NAME_TYPES.has(name)) return name;
  const baseType = name ? BUILT_IN_ROLE_NAME_TYPES.get(name)! : role;
  if (!baseType) return "Staff";
  let prefix = "";
  if (labCodes.length === 1) {
    if (labCodes[0] === "MICRO") prefix = "Microbiology";
    else if (labCodes[0] === "FP") prefix = "Physicochemical";
  }
  switch (baseType) {
    case "Analyst": return prefix ? `${prefix} Analyst` : "Analyst";
    case "SectionHead": return prefix ? `${prefix} Section Head` : "Section Head";
    case "Reviewer": return "Quality Reviewer";
    case "SystemAdministrator": return "System Administrator";
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
