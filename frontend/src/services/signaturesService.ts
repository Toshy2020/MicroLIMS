import { apiClient } from "./apiClient";

export interface SignatureTrailEntry {
  printedName: string;
  role: string;
  meaning: string;
  signedAt: string;
  comment: string | null;
}

// SectionHead/SystemAdministrator only - matches the backend
// [Authorize(Roles = ...)] on SignaturesController.
export const SignaturesService = {
  async getTrail(entityType: string, entityId: number): Promise<SignatureTrailEntry[]> {
    return (await apiClient.get("/signatures", { params: { entityType, entityId } })).data.data;
  }
};
