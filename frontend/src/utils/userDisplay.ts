// The job title shown beside a user's name when an administrator has not set
// one: the role, prefixed with the laboratory when the user belongs to
// exactly one.
export function formatRoleFallback(role: string | null, labCodes: string[]): string {
  if (!role) return "Staff";
  let prefix = "";
  if (labCodes.length === 1) {
    if (labCodes[0] === "MICRO") prefix = "Microbiology";
    else if (labCodes[0] === "FP") prefix = "Physicochemical";
  }
  switch (role) {
    case "Analyst": return prefix ? `${prefix} Analyst` : "Analyst";
    case "SectionHead": return prefix ? `${prefix} Section Head` : "Section Head";
    case "Reviewer": return "Quality Reviewer";
    case "SystemAdministrator": return "System Administrator";
    default: return role;
  }
}

export function userInitials(fullName: string | null, username: string | null): string {
  const source = (fullName ?? username ?? "").trim();
  if (!source) return "U";
  const parts = source.split(/\s+/);
  const letters = parts.length > 1 ? parts[0].charAt(0) + parts[parts.length - 1].charAt(0) : parts[0].charAt(0);
  return letters.toUpperCase();
}
