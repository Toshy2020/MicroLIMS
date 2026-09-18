import { apiClient } from "./apiClient";

export interface LaboratorySection {
  sectionId: number;
  sectionCode: string;
  sectionName: string;
  departmentId: number;
  departmentCode: string;
  departmentName: string;
}

export interface UserMembership {
  departmentId: number;
  sectionId: number | null;
}

export async function getSections(): Promise<LaboratorySection[]> {
  return (await apiClient.get("/org/sections")).data.data;
}

export async function getMySections(): Promise<LaboratorySection[]> {
  return (await apiClient.get("/org/my-sections")).data.data;
}

export async function getUserMemberships(userId: number): Promise<UserMembership[]> {
  return (await apiClient.get(`/org/users/${userId}/memberships`)).data.data;
}

export async function replaceUserMemberships(
  userId: number,
  memberships: UserMembership[]
): Promise<UserMembership[]> {
  return (await apiClient.put(`/org/users/${userId}/memberships`, { memberships })).data.data;
}

export const laboratorySectionService = {
  getSections,
  getMySections,
  getUserMemberships,
  replaceUserMemberships
};

