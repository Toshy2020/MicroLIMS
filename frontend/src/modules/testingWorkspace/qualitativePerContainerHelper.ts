export interface QualitativeContainerEntry {
  index: number;
  conforms: boolean;
  observation?: string;
}

/**
 * Returns the exact conclusion line mirroring the backend server wording:
 * - all pass -> "All {n} containers comply"
 * - one fails -> "Container {i} of {n} does not comply"
 * - several fail -> "Containers {i, j} of {n} do not comply" (indexes ascending, joined with ", ")
 */
export function getConclusionLine(
  containerCount: number,
  containers?: { index: number; conforms: boolean }[] | null
): string {
  if (!containers || containers.length === 0) {
    return `All ${containerCount} containers comply`;
  }

  const failedIndexes = containers
    .filter((c) => !c.conforms)
    .map((c) => c.index)
    .sort((a, b) => a - b);

  if (failedIndexes.length === 0) {
    return `All ${containerCount} containers comply`;
  }

  if (failedIndexes.length === 1) {
    return `Container ${failedIndexes[0]} of ${containerCount} does not comply`;
  }

  return `Containers ${failedIndexes.join(", ")} of ${containerCount} do not comply`;
}

/**
 * Validates the container count input.
 * Allowed range: 1 to 200 (integer).
 */
export function isValidContainerCount(count: number | null | undefined): boolean {
  return typeof count === "number" && Number.isInteger(count) && count >= 1 && count <= 200;
}

/**
 * Builds or adjusts the container rows (1..count), preserving any existing conforms
 * and observation values for previously existing container indexes.
 */
export function buildContainerRows<T extends { index: number; conforms: boolean; observation: string }>(
  existingContainers: T[],
  newCount: number
): { index: number; conforms: boolean; observation: string }[] {
  if (newCount < 1) return [];

  const existingMap = new Map<number, T>();
  for (const c of existingContainers) {
    existingMap.set(c.index, c);
  }

  const result: { index: number; conforms: boolean; observation: string }[] = [];
  for (let i = 1; i <= newCount; i++) {
    const existing = existingMap.get(i);
    if (existing) {
      result.push({
        index: i,
        conforms: existing.conforms,
        observation: existing.observation
      });
    } else {
      result.push({
        index: i,
        conforms: true,
        observation: ""
      });
    }
  }

  return result;
}

export interface BuildQualitativePayloadArgs {
  analysedAt: string;
  equipmentId?: number | "" | null;
  password: string;
  comment?: string | null;
  isPerContainer: boolean;
  containerCount?: number | null;
  parameters: {
    specificationId: number;
    conforms: boolean;
    observation?: string | null;
    containers?: { index: number; conforms: boolean; observation: string }[];
  }[];
}

export interface QualitativeStandardPayload {
  analysedAt: string;
  equipmentId: number | null;
  parameters: {
    specificationId: number;
    conforms: boolean;
    observation: string | null;
  }[];
  password: string;
  comment: string | null;
  containerCount?: undefined;
}

export interface QualitativePerContainerPayload {
  analysedAt: string;
  equipmentId: number | null;
  containerCount: number;
  parameters: {
    specificationId: number;
    conforms: boolean;
    observation: string | null;
    containers: {
      index: number;
      conforms: boolean;
      observation?: string;
    }[];
  }[];
  password: string;
  comment: string | null;
}

/**
 * Builds the qualitative result payload for the API:
 * - When isPerContainer is true:
 *   Includes containerCount at top level, and containers: [{ index, conforms, observation? }]
 *   for each parameter.
 * - When isPerContainer is false:
 *   Omits containerCount, and omits containers on parameters (exact legacy shape).
 */
export function buildQualitativePayload(args: BuildQualitativePayloadArgs & { isPerContainer: true }): QualitativePerContainerPayload;
export function buildQualitativePayload(args: BuildQualitativePayloadArgs & { isPerContainer: false }): QualitativeStandardPayload;
export function buildQualitativePayload(args: BuildQualitativePayloadArgs): QualitativePerContainerPayload | QualitativeStandardPayload;
export function buildQualitativePayload(args: BuildQualitativePayloadArgs): QualitativePerContainerPayload | QualitativeStandardPayload {
  const { analysedAt, equipmentId, password, comment, isPerContainer, containerCount, parameters } = args;

  const baseEquipmentId = equipmentId !== "" && equipmentId != null ? Number(equipmentId) : null;

  if (isPerContainer) {
    return {
      analysedAt: new Date(analysedAt).toISOString(),
      equipmentId: baseEquipmentId,
      containerCount: Number(containerCount),
      parameters: parameters.map((p) => {
        const containers = (p.containers || []).map((c) => ({
          index: c.index,
          conforms: c.conforms,
          ...(c.observation?.trim() ? { observation: c.observation.trim() } : {})
        }));
        const parameterConforms = containers.every((c) => c.conforms);

        return {
          specificationId: p.specificationId,
          conforms: parameterConforms,
          observation: p.observation?.trim() || null,
          containers
        };
      }),
      password,
      comment: comment?.trim() || null
    };
  }

  return {
    analysedAt: new Date(analysedAt).toISOString(),
    equipmentId: baseEquipmentId,
    parameters: parameters.map((p) => ({
      specificationId: p.specificationId,
      conforms: p.conforms,
      observation: p.observation?.trim() || null
    })),
    password,
    comment: comment?.trim() || null
  };
}
