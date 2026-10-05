import { useLocation } from "react-router-dom";
import type { HplcTechnique } from "./types";

export interface TechniqueWorkspaceRoutes {
  root: string;
  instrument: (equipmentId: number | string) => string;
  newRun: (equipmentId: number | string) => string;
  run: (equipmentId: number | string, runId: number | string) => string;
  sst: (equipmentId: number | string, runId: number | string) => string;
  samples: (equipmentId: number | string, runId: number | string) => string;
  evidence: (equipmentId: number | string, runId: number | string) => string;
  history: (equipmentId: number | string) => string;
  sampleEntry: (equipmentId: number | string, runId: number | string, runSampleId: number | string) => string;
  qualificationEntry: (equipmentId: number | string, runId: number | string, runSampleId: number | string) => string;
}

export interface TechniqueContext {
  technique: HplcTechnique;
  label: "GC" | "HPLC";
  basePath: "/gc-workspace" | "/hplc-workspace";
  routes: TechniqueWorkspaceRoutes;
}

export function getTechniqueRoutes(basePath: "/gc-workspace" | "/hplc-workspace"): TechniqueWorkspaceRoutes {
  return {
    root: basePath,
    instrument: (equipmentId) => `${basePath}/${equipmentId}`,
    newRun: (equipmentId) => `${basePath}/${equipmentId}/new-run`,
    run: (equipmentId, runId) => `${basePath}/${equipmentId}/run/${runId}`,
    sst: (equipmentId, runId) => `${basePath}/${equipmentId}/run/${runId}/sst`,
    samples: (equipmentId, runId) => `${basePath}/${equipmentId}/run/${runId}/samples`,
    evidence: (equipmentId, runId) => `${basePath}/${equipmentId}/run/${runId}/evidence`,
    history: (equipmentId) => `${basePath}/${equipmentId}/history`,
    sampleEntry: (equipmentId, runId, runSampleId) => `${basePath}/${equipmentId}/run/${runId}/sample/${runSampleId}`,
    qualificationEntry: (equipmentId, runId, runSampleId) => `${basePath}/${equipmentId}/run/${runId}/qualification/${runSampleId}`
  };
}

// Built once so callers can list `routes` / the context in effect dependencies.
const HPLC_CONTEXT: TechniqueContext = { technique: "Hplc", label: "HPLC", basePath: "/hplc-workspace", routes: getTechniqueRoutes("/hplc-workspace") };
const GC_CONTEXT: TechniqueContext = { technique: "Gc", label: "GC", basePath: "/gc-workspace", routes: getTechniqueRoutes("/gc-workspace") };

export function useTechnique(): TechniqueContext {
  const location = useLocation();
  return location.pathname.startsWith("/gc-workspace") ? GC_CONTEXT : HPLC_CONTEXT;
}
