export type CleaningTechnique = "UV" | "HPLC" | "ICP";

export const CLEANING_TECHNIQUES: CleaningTechnique[] = ["UV", "HPLC", "ICP"];

/**
 * Maps a test's workflowType to its cleaning technique:
 * - HplcMethodAssay -> HPLC
 * - IcpMethodAssay -> ICP
 * - any other physicochemical test -> UV
 */
export function cleaningTechniqueOf(workflowType?: string | null): CleaningTechnique {
  if (workflowType === "HplcMethodAssay") return "HPLC";
  if (workflowType === "IcpMethodAssay") return "ICP";
  return "UV";
}
