import { apiClient } from "../../../../services/apiClient";

export const EMConfigService = {
  getDepartments: () => apiClient.get("/masterdata/departments").then((r) => r.data.data),
  createDepartment: (name: string, cls: string, frequency: string) =>
    apiClient.post("/masterdata/departments", { name, class: cls, testingFrequency: frequency }).then((r) => r.data.data),
  updateDepartment: (id: number, name: string, cls: string, frequency: string) =>
    apiClient.put(`/masterdata/departments/${id}`, { name, class: cls, testingFrequency: frequency }).then((r) => r.data.data),
  deleteDepartment: (id: number) => apiClient.delete(`/masterdata/departments/${id}`),

  getRooms: () => apiClient.get("/masterdata/rooms").then((r) => r.data.data),
  createRoom: (name: string, departmentId: number, gradeClassification: string) =>
    apiClient.post("/masterdata/rooms", { name, departmentId, gradeClassification }).then((r) => r.data.data),
  updateRoom: (id: number, name: string, departmentId: number, gradeClassification: string) =>
    apiClient.put(`/masterdata/rooms/${id}`, { name, departmentId, gradeClassification }).then((r) => r.data.data),
  deleteRoom: (id: number) => apiClient.delete(`/masterdata/rooms/${id}`),

  getRoomTestConfigurations: (roomId: number) =>
    apiClient.get("/masterdata/room-test-configurations", { params: { roomId } }).then((r) => r.data.data),
  createRoomTestConfiguration: (roomId: number, testType: string, testCode: string, alertLimit: string, actionLimit: string, specLimit: string, unit?: string) =>
    apiClient.post("/masterdata/room-test-configurations", { roomId, testType, testCode, alertLimit, actionLimit, specLimit, unit }).then((r) => r.data.data),
  updateRoomTestConfiguration: (id: number, testType: string, testCode: string, alertLimit: string, actionLimit: string, specLimit: string, unit?: string) =>
    apiClient.put(`/masterdata/room-test-configurations/${id}`, { testType, testCode, alertLimit, actionLimit, specLimit, unit }).then((r) => r.data.data),
  deleteRoomTestConfiguration: (id: number) => apiClient.delete(`/masterdata/room-test-configurations/${id}`)
};
