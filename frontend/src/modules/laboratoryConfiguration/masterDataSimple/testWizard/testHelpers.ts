import type { TFunction } from "i18next";
import { i18n } from "../../../../i18n";
import type { LaboratorySection } from "../../../../services/laboratorySectionService";
import type {
  CreateTestDefinitionStageReplicateRequest,
  MediaIncubationConditionOption,
  MediaProductOption,
  TestDefinitionStageReplicateDto,
  TestWorkflowStepPayload
} from "../../../../services/masterDataOptions";
import type { HplcMethodListItem } from "../services/HplcMethodService";
import type { WorkflowStepItem } from "../testMaster/WorkflowStepsSection";
import {
  DraftStageReplicate,
  DraftWorkflowStep,
  TestFormModel,
  toStepPayload
} from "./testFormModel";

export interface OrganismOption {
  id: number;
  scientificName: string;
  commonName?: string;
}

export type HplcMethodChoice = Pick<HplcMethodListItem, "id" | "name" | "abbreviation" | "isActive">;

export interface TestWizardLookupData {
  sections: LaboratorySection[];
  mySections: LaboratorySection[];
  hplcMethods: HplcMethodChoice[];
  mediaProducts: MediaProductOption[];
  conditions: MediaIncubationConditionOption[];
  organisms: OrganismOption[];
}

export function sectionLabel(
  sections: LaboratorySection[],
  sectionId: number | "",
  t: TFunction = i18n.getFixedT("en", "masterData")
): string {
  if (sectionId === "") return "—";
  const match = sections.find((s) => s.sectionId === sectionId);
  return match ? `${match.sectionName} (${match.sectionCode})` : t("testWizard.helpers.sectionFallback", { id: sectionId });
}

export function hplcMethodLabel(
  methods: HplcMethodChoice[],
  id: number | "",
  t: TFunction = i18n.getFixedT("en", "masterData")
): string {
  if (id === "") return "—";
  const m = methods.find((item) => item.id === id);
  return m ? `${m.name} (${m.abbreviation})` : t("testWizard.helpers.methodFallback", { id });
}

export interface StageOperationPlan {
  deletes: { id: number }[];
  updates: { id: number; sampleReplicates: number; version?: number }[];
  creates: CreateTestDefinitionStageReplicateRequest[];
}

export function planStageOperations(
  initial: DraftStageReplicate[],
  current: DraftStageReplicate[]
): StageOperationPlan {
  const currentById = new Map<number, DraftStageReplicate>();
  const creates: CreateTestDefinitionStageReplicateRequest[] = [];

  for (const row of current) {
    if (row.id != null) {
      currentById.set(row.id, row);
    } else {
      creates.push({
        role: row.role,
        sampleReplicates: Number(row.sampleReplicates.trim())
      });
    }
  }

  const deletes: { id: number }[] = [];
  const updates: { id: number; sampleReplicates: number; version?: number }[] = [];

  for (const orig of initial) {
    if (orig.id == null) continue;
    const match = currentById.get(orig.id);
    if (!match) {
      deletes.push({ id: orig.id });
    } else if (match.sampleReplicates.trim() !== orig.sampleReplicates.trim()) {
      updates.push({
        id: orig.id,
        sampleReplicates: Number(match.sampleReplicates.trim()),
        version: orig.version
      });
    }
  }

  return { deletes, updates, creates };
}

function stepSignature(payload: TestWorkflowStepPayload): string {
  return JSON.stringify({
    ...payload,
    stepMedia: payload.stepMedia.map((m) => ({
      mediaProductId: m.mediaProductId,
      mediaIncubationConditionId: m.mediaIncubationConditionId,
      isRequired: m.isRequired,
      displayOrder: m.displayOrder
    }))
  });
}

export interface StepOperationPlan {
  deletes: { id: number; version?: number }[];
  updates: { id: number; version?: number; payload: TestWorkflowStepPayload }[];
  creates: TestWorkflowStepPayload[];
}

export function planStepOperations(
  initial: WorkflowStepItem[],
  current: DraftWorkflowStep[],
  conditions: MediaIncubationConditionOption[]
): StepOperationPlan {
  const currentById = new Map<number, DraftWorkflowStep>();
  const creates: TestWorkflowStepPayload[] = [];

  for (const step of current) {
    if (step.id != null) {
      currentById.set(step.id, step);
    } else {
      creates.push(toStepPayload(step, conditions));
    }
  }

  const deletes: { id: number; version?: number }[] = [];
  const updates: { id: number; version?: number; payload: TestWorkflowStepPayload }[] = [];

  for (const orig of initial) {
    const match = currentById.get(orig.id);
    if (!match) {
      deletes.push({ id: orig.id, version: orig.version });
      continue;
    }
    const origPayload = toStepPayload(
      {
        stepName: orig.stepName,
        isFinalStep: orig.isFinalStep,
        stepType: orig.stepType,
        targetOrganismId: orig.targetOrganismId ?? null,
        stepMedia: (orig.stepMedia ?? []).map((m, idx) => ({
          mediaProductId: m.mediaProductId,
          mediaIncubationConditionId:
            typeof m.mediaIncubationConditionId === "number" ? m.mediaIncubationConditionId : "",
          isRequired: !!m.isRequired,
          displayOrder: typeof m.displayOrder === "number" ? m.displayOrder : idx
        })),
        requiresIncubationTransfer: !!orig.requiresIncubationTransfer,
        stage2TempMin: (orig.incubationStages ?? []).find((x) => x.stageNumber === 2)?.tempMin,
        stage2TempMax: (orig.incubationStages ?? []).find((x) => x.stageNumber === 2)?.tempMax,
        stage2IncubationMinHours: (orig.incubationStages ?? []).find((x) => x.stageNumber === 2)?.incubationMinHours,
        stage2IncubationMaxHours: (orig.incubationStages ?? []).find((x) => x.stageNumber === 2)?.incubationMaxHours,
        phenotypicTestType: orig.phenotypicTestType ?? null,
        phenotypicTestTypes: orig.phenotypicTestTypes ?? []
      },
      conditions
    );
    const nextPayload = toStepPayload(match, conditions);
    if (stepSignature(origPayload) !== stepSignature(nextPayload)) {
      updates.push({ id: orig.id, version: orig.version, payload: nextPayload });
    }
  }

  return { deletes, updates, creates };
}

export function stagesToDtos(dtos: TestDefinitionStageReplicateDto[]): DraftStageReplicate[] {
  return dtos.map((r) => ({
    id: r.id,
    version: r.version,
    role: r.role,
    sampleReplicates: String(r.sampleReplicates)
  }));
}

export function isMicroWorkflow(model: TestFormModel): boolean {
  return model.workflowType === "CountTest" || model.workflowType === "Observation";
}
