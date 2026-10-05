// Stable route helpers for ICP Workspace.
// Defined as module-level constants to avoid re-creation on render.

export const ICP_ROUTES = {
  root: "/icp-workspace",
  instrument: (equipmentId: number | string) => `/icp-workspace/${equipmentId}`,
  run: (equipmentId: number | string, runId: number | string) => `/icp-workspace/${equipmentId}/run/${runId}`,
  calibration: (equipmentId: number | string, runId: number | string) => `/icp-workspace/${equipmentId}/run/${runId}/calibration`,
  ccv: (equipmentId: number | string, runId: number | string) => `/icp-workspace/${equipmentId}/run/${runId}/ccv`,
  samples: (equipmentId: number | string, runId: number | string) => `/icp-workspace/${equipmentId}/run/${runId}/samples`,
  evidence: (equipmentId: number | string, runId: number | string) => `/icp-workspace/${equipmentId}/run/${runId}/evidence`,
  history: (equipmentId: number | string) => `/icp-workspace/${equipmentId}/history`,
  sampleEntry: (equipmentId: number | string, runId: number | string, runSampleId: number | string) =>
    `/icp-workspace/${equipmentId}/run/${runId}/sample/${runSampleId}`
} as const;
