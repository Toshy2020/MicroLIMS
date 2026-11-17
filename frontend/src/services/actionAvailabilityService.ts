import { apiClient } from "./apiClient";

// Whether the signed-in user could perform each gated action on one record now. The server answers with its own
// refusal sentence when not; the actions enforce the same checks themselves, so this is a display aid only.
export type AvailabilityKind = "sample" | "test-order" | "media" | "document-revision";

export interface ActionAvailabilityDto {
  action: string;
  allowed: boolean;
  reason?: string | null;
}

export const actionAvailabilityService = {
  async get(
    kind: AvailabilityKind,
    id: number,
    sectionId?: number | null,
    signal?: AbortSignal
  ): Promise<ActionAvailabilityDto[]> {
    const res = await apiClient.get<{ data: ActionAvailabilityDto[] | null }>(`/actions/${kind}/${id}`, {
      params: kind === "sample" && sectionId != null ? { sectionId } : undefined,
      signal
    });
    const list = res.data?.data;
    // An answer that is not a list is a failed load: the buttons stay usable and the server decides on click.
    if (!Array.isArray(list)) throw new Error("Unexpected action availability response.");
    return list;
  }
};
