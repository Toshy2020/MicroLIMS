import { apiClient } from "../../../../services/apiClient";

export const EMPreparationService = {
  getNeedsPreparation: () => apiClient.get("/testorders").then((r) =>
    r.data.data.filter((s: any) => s.category === "EnvironmentalMonitoring" && s.preparationStatus === "NeedsPreparation")),
  getRoomsForDepartment: (departmentId: number) =>
    apiClient.get("/masterdata/rooms").then((r) => r.data.data.filter((room: any) => room.departmentId === departmentId)),
  getRoomTestConfigurations: (roomId: number) =>
    apiClient.get(`/masterdata/room-test-configurations?roomId=${roomId}`).then((r) => r.data.data),
  prepare: (sampleId: number, roomTestConfigurationIds: number[]) =>
    apiClient.post("/em/prepare", { sampleId, roomTestConfigurationIds }).then((r) => r.data.data)
};
