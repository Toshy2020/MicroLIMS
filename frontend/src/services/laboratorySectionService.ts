import { apiClient } from "./apiClient";

export interface LaboratorySection {
  sectionId: number;
  sectionCode: string;
  sectionName: string;
  departmentId: number;
  departmentCode: string;
  departmentName: string;
}

export async function getSections(): Promise<LaboratorySection[]> {
  return (await apiClient.get("/org/sections")).data.data;
}

export async function getMySections(): Promise<LaboratorySection[]> {
  return (await apiClient.get("/org/my-sections")).data.data;
}

export const laboratorySectionService = {
  getSections,
  getMySections
};
