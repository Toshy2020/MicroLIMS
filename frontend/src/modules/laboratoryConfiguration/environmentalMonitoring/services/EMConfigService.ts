import { apiClient } from "../../../../services/apiClient";

export const EMConfigService = {
  getDepartments: () => apiClient.get("/masterdata/departments").then((r) => r.data.data),
  createDepartment: (name: string, cls: string, frequency: string) =>
    apiClient.post("/masterdata/departments", { name, class: cls, testingFrequency: frequency }).then((r) => r.data.data),
  getRooms: () => apiClient.get("/masterdata/rooms").then((r) => r.data.data),
  createRoom: (name: string, departmentId: number, gradeClassification: string) =>
    apiClient.post("/masterdata/rooms", { name, departmentId, gradeClassification }).then((r) => r.data.data),
  createRoomTestConfiguration: (roomId: number, testType: string, testCode: string, alertLimit: string, actionLimit: string, specLimit: string) =>
    apiClient.post("/masterdata/room-test-configurations", { roomId, testType, testCode, alertLimit, actionLimit, specLimit }).then((r) => r.data.data)
};
