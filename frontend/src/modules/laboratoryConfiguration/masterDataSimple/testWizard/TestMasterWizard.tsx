import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import { ConfigWizard } from "../../../../components/config/ConfigWizard";
import type { WizardStep } from "../../../../components/config/useWizard";
import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";
import {
  getMySections,
  getSections,
  type LaboratorySection
} from "../../../../services/laboratorySectionService";
import {
  masterDataOptions,
  type CreateTestDefinitionPayload,
  type MediaIncubationConditionOption,
  type MediaProductOption,
  type SaveTestWithStepsPayload,
  type TestDefinitionStageReplicateDto,
  type UpdateTestDefinitionPayload
} from "../../../../services/masterDataOptions";
import { masterDataUsageService } from "../../../../services/masterDataUsageService";
import { getErrorMessage } from "../../../../utils/errorMessage";
import { HplcMethodService } from "../services/HplcMethodService";
import type { PageArea } from "../testMasterArea";
import { FP_SECTION_CODE, type TestMasterLab } from "../testMaster/testMasterConstants";
import type { WorkflowStepItem } from "../testMaster/WorkflowStepsSection";
import { AnalystPreview, previewModel } from "./AnalystPreview";
import { createBasicsStep } from "./BasicsStep";
import { createStagesStep } from "./StagesStep";
import { createStepsStep } from "./StepsStep";
import { createTypeSettingsStep } from "./TypeSettingsStep";
import {
  TEST_DRAFT_FIELDS,
  createEmptyTestFormModel,
  duplicateFromTestDefinition,
  fromTestDefinition,
  stagesFromDtos,
  stepsFromDtos,
  toStagePayloads,
  toStepPayloads,
  toTestPayload,
  type TestFormModel
} from "./testFormModel";
import {
  planStageOperations,
  planStepOperations,
  type HplcMethodChoice,
  type OrganismOption,
  type TestWizardLookupData
} from "./testHelpers";

export interface TestMasterWizardProps {
  open: boolean;
  lab: TestMasterLab;
  area?: PageArea;
  editingTest?: TestDefinitionOption | null;
  duplicateFrom?: TestDefinitionOption | null;
  defaultSectionId?: number | "";
  sections?: LaboratorySection[];
  mySections?: LaboratorySection[];
  hplcMethods?: HplcMethodChoice[];
  onCreateWithSteps: (payload: SaveTestWithStepsPayload) => Promise<TestDefinitionOption>;
  onUpdateTest: (
    id: number,
    payload: UpdateTestDefinitionPayload,
    reason?: string
  ) => Promise<TestDefinitionOption>;
  onReloadDefinitions?: () => Promise<void>;
  onClose: () => void;
  onSaved?: (saved: TestDefinitionOption, model: TestFormModel, mode: "create" | "edit") => void;
}

interface SourceChildrenState {
  ready: boolean;
  stages: TestDefinitionStageReplicateDto[];
  steps: WorkflowStepItem[];
}

function needsStageFetch(lab: TestMasterLab, workflowType?: string): boolean {
  return lab === "fp" && workflowType !== "Titration" && workflowType !== "UvSpectrophotometry";
}

function needsStepFetch(workflowType?: string): boolean {
  return workflowType === "CountTest" || workflowType === "Observation";
}

/**
 * Multi-step wizard for creating, duplicating ("New from existing"), and editing
 * tests on Test Master. Create uses the atomic with-steps endpoint; edit preserves
 * the existing separate test, stage-replicate, and workflow-step endpoints.
 */
export function TestMasterWizard(props: TestMasterWizardProps) {
  if (!props.open) return null;
  return <OpenTestMasterWizard {...props} />;
}

function OpenTestMasterWizard({
  open,
  lab,
  area,
  editingTest = null,
  duplicateFrom = null,
  defaultSectionId = "",
  sections: sectionsProp,
  mySections: mySectionsProp,
  hplcMethods: hplcMethodsProp,
  onCreateWithSteps,
  onUpdateTest,
  onReloadDefinitions,
  onClose,
  onSaved
}: TestMasterWizardProps) {
  const isEditing = Boolean(editingTest && editingTest.id != null);
  const sourceTest = editingTest ?? duplicateFrom ?? null;
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;
  const wizardMode: "create" | "edit" = isEditing ? "edit" : "create";

  const [fetchedSections, setFetchedSections] = useState<LaboratorySection[]>([]);
  const [fetchedMySections, setFetchedMySections] = useState<LaboratorySection[]>([]);
  const [fetchedHplcMethods, setFetchedHplcMethods] = useState<HplcMethodChoice[]>([]);
  const [mediaProducts, setMediaProducts] = useState<MediaProductOption[]>([]);
  const [conditions, setConditions] = useState<MediaIncubationConditionOption[]>([]);
  const [organisms, setOrganisms] = useState<OrganismOption[]>([]);
  const [sectionsLoaded, setSectionsLoaded] = useState(
    Boolean(sectionsProp && mySectionsProp)
  );

  const [sourceChildren, setSourceChildren] = useState<SourceChildrenState>(() => ({
    ready: !sourceTest || (!needsStageFetch(lab, sourceTest.workflowType) && !needsStepFetch(sourceTest.workflowType)),
    stages: [],
    steps: []
  }));

  useEffect(() => {
    let cancelled = false;
    if (!sectionsProp || !mySectionsProp) {
      Promise.all([
        sectionsProp ? Promise.resolve(sectionsProp) : getSections().catch(() => []),
        mySectionsProp ? Promise.resolve(mySectionsProp) : getMySections().catch(() => [])
      ]).then(([allSecs, mySecs]) => {
        if (cancelled) return;
        if (!sectionsProp) setFetchedSections(allSecs);
        if (!mySectionsProp) setFetchedMySections(mySecs);
        setSectionsLoaded(true);
      });
    } else {
      setSectionsLoaded(true);
    }
    if (!hplcMethodsProp) {
      HplcMethodService.getAll(false)
        .then((res) => {
          if (!cancelled) setFetchedHplcMethods(res);
        })
        .catch(() => {});
    }
    if (lab === "micro") {
      masterDataOptions
        .getMediaProducts()
        .then((res: MediaProductOption[]) => {
          if (!cancelled) setMediaProducts(res ?? []);
        })
        .catch(() => {});
      masterDataOptions
        .getMediaIncubationConditions()
        .then((res: MediaIncubationConditionOption[]) => {
          if (!cancelled) setConditions(res ?? []);
        })
        .catch(() => {});
      masterDataOptions
        .getOrganisms()
        .then((res: OrganismOption[]) => {
          if (!cancelled) setOrganisms(res ?? []);
        })
        .catch(() => {});
    }
    return () => {
      cancelled = true;
    };
  }, [lab, sectionsProp, mySectionsProp, hplcMethodsProp]);

  useEffect(() => {
    if (!sourceTest) {
      setSourceChildren({ ready: true, stages: [], steps: [] });
      return;
    }
    const loadStages = needsStageFetch(lab, sourceTest.workflowType);
    const loadSteps = needsStepFetch(sourceTest.workflowType);
    if (!loadStages && !loadSteps) {
      setSourceChildren({ ready: true, stages: [], steps: [] });
      return;
    }
    let cancelled = false;
    setSourceChildren({ ready: false, stages: [], steps: [] });
    Promise.all([
      loadStages
        ? masterDataOptions.getTestDefinitionStageReplicates(sourceTest.id)
        : Promise.resolve([]),
      loadSteps
        ? masterDataOptions.getTestWorkflowSteps(sourceTest.id)
        : Promise.resolve([])
    ])
      .then(([stages, steps]) => {
        if (cancelled) return;
        setSourceChildren({
          ready: true,
          stages: stages ?? [],
          steps: steps ?? []
        });
      })
      .catch((err: unknown) => {
        // Never carry on with empty stages or steps: a copy would silently lose them and an
        // edit would show a test without its workflow. Say so and close; the user can retry.
        if (cancelled) return;
        toast.error(getErrorMessage(err, "Could not load the test's stages and workflow steps. Try again."));
        onCloseRef.current();
      });
    return () => {
      cancelled = true;
    };
  }, [lab, sourceTest]);

  const fpSectionId = useMemo(
    () => fetchedSections.find((s) => s.sectionCode === FP_SECTION_CODE)?.sectionId ?? null,
    [fetchedSections]
  );
  const inLab = useCallback(
    (sid?: number | null) =>
      lab === "fp" ? fpSectionId !== null && sid === fpSectionId : sid !== fpSectionId,
    [lab, fpSectionId]
  );
  const sections = useMemo(
    () => sectionsProp ?? fetchedSections.filter((s) => inLab(s.sectionId)),
    [sectionsProp, fetchedSections, inLab]
  );
  const mySections = useMemo(
    () => mySectionsProp ?? fetchedMySections.filter((s) => inLab(s.sectionId)),
    [mySectionsProp, fetchedMySections, inLab]
  );
  const hplcMethods = hplcMethodsProp ?? fetchedHplcMethods;

  const lookupData = useMemo<TestWizardLookupData>(
    () => ({
      sections,
      mySections,
      hplcMethods,
      mediaProducts,
      conditions,
      organisms
    }),
    [sections, mySections, hplcMethods, mediaProducts, conditions, organisms]
  );

  const lookupRef = useRef(lookupData);
  lookupRef.current = lookupData;

  const steps = useMemo<WizardStep<TestFormModel>[]>(() => {
    const getLookup = () => lookupRef.current;
    return [
      createBasicsStep(getLookup),
      createTypeSettingsStep(getLookup),
      createStagesStep(),
      createStepsStep()
    ];
  }, []);

  const initialModel = useMemo<TestFormModel>(() => {
    if (editingTest) {
      const base = fromTestDefinition(editingTest, { lab, area, defaultSectionId });
      const initialStages = stagesFromDtos(sourceChildren.stages);
      const initialSteps = stepsFromDtos(sourceChildren.steps);
      return {
        ...base,
        stages: initialStages,
        steps: initialSteps,
        initialStages,
        initialSteps: sourceChildren.steps
      };
    }
    if (duplicateFrom) {
      const canUseSourceSection =
        duplicateFrom.sectionId != null &&
        mySections.some((s) => s.sectionId === duplicateFrom.sectionId);
      const mySingleSectionId = mySections.length === 1 ? mySections[0].sectionId : "";
      return duplicateFromTestDefinition(duplicateFrom, {
        context: { lab, area, defaultSectionId },
        canUseSourceSection,
        mySingleSectionId,
        stages: sourceChildren.stages,
        steps: sourceChildren.steps
      });
    }
    return createEmptyTestFormModel({
      lab,
      area,
      sectionId: defaultSectionId
    });
  }, [editingTest, duplicateFrom, lab, area, defaultSectionId, mySections, sourceChildren]);

  const validateOnServer = useCallback((model: TestFormModel) => {
    const payload = toTestPayload(model);
    return masterDataOptions.validateTestDefinition(payload, model.editingId ?? undefined);
  }, []);

  const usageFn = useMemo(
    () =>
      isEditing && editingTest?.id != null
        ? () => masterDataUsageService.getUsage("TestDefinition", editingTest.id)
        : undefined,
    [isEditing, editingTest?.id]
  );

  const reloadOriginal = useMemo(
    () =>
      isEditing && editingTest?.id != null
        ? async () => {
            const freshList: TestDefinitionOption[] = await masterDataOptions.getTestDefinitions();
            const fresh = freshList.find((t) => t.id === editingTest.id);
            if (!fresh) {
              throw new Error("Test definition no longer exists.");
            }
            const [freshStages, freshSteps] = await Promise.all([
              needsStageFetch(lab, fresh.workflowType)
                ? masterDataOptions.getTestDefinitionStageReplicates(fresh.id)
                : Promise.resolve([]),
              needsStepFetch(fresh.workflowType)
                ? masterDataOptions.getTestWorkflowSteps(fresh.id)
                : Promise.resolve([])
            ]);
            const base = fromTestDefinition(fresh, { lab, area, defaultSectionId });
            const initialStages = stagesFromDtos(freshStages ?? []);
            const initialSteps = stepsFromDtos(freshSteps ?? []);
            return {
              ...base,
              stages: initialStages,
              steps: initialSteps,
              initialStages,
              initialSteps: freshSteps ?? []
            };
          }
        : undefined,
    [isEditing, editingTest?.id, lab, area, defaultSectionId]
  );

  const handleSave = useCallback(
    async (model: TestFormModel, reason: string | null) => {
      if (model.editingId != null) {
        const editId = model.editingId;
        const updatePayload = toTestPayload(model) as UpdateTestDefinitionPayload;
        const updated = await onUpdateTest(editId, updatePayload, reason ?? undefined);

        const stagePlan = planStageOperations(model.initialStages, model.stages);
        for (const del of stagePlan.deletes) {
          await masterDataOptions.deleteTestDefinitionStageReplicate(editId, del.id);
        }
        for (const upd of stagePlan.updates) {
          await masterDataOptions.updateTestDefinitionStageReplicate(
            editId,
            upd.id,
            { sampleReplicates: upd.sampleReplicates },
            upd.version
          );
        }
        for (const cre of stagePlan.creates) {
          await masterDataOptions.createTestDefinitionStageReplicate(editId, cre);
        }

        const stepPlan = planStepOperations(
          model.initialSteps,
          model.steps,
          lookupRef.current.conditions
        );
        for (const del of stepPlan.deletes) {
          await masterDataOptions.deleteTestWorkflowStep(del.id, del.version);
        }
        for (const upd of stepPlan.updates) {
          await masterDataOptions.updateTestWorkflowStep(upd.id, upd.payload, upd.version);
        }
        for (const cre of stepPlan.creates) {
          await masterDataOptions.createTestWorkflowStep(editId, cre);
        }

        const hasChildChanges =
          stagePlan.deletes.length + stagePlan.updates.length + stagePlan.creates.length > 0 ||
          stepPlan.deletes.length + stepPlan.updates.length + stepPlan.creates.length > 0;
        if (hasChildChanges && onReloadDefinitions) {
          await onReloadDefinitions();
        }
        await Promise.resolve(onSaved?.(updated, model, "edit"));
        return;
      }

      const payload: SaveTestWithStepsPayload = {
        test: toTestPayload(model) as CreateTestDefinitionPayload,
        stages: toStagePayloads(model.stages),
        steps: toStepPayloads(model.steps, lookupRef.current.conditions)
      };
      const created = await onCreateWithSteps(payload);
      await Promise.resolve(onSaved?.(created, model, "create"));
    },
    [onCreateWithSteps, onUpdateTest, onReloadDefinitions, onSaved]
  );

  const title = editingTest
    ? `Edit Test "${editingTest.code}"`
    : duplicateFrom
      ? `New Test from "${duplicateFrom.code}"`
      : lab === "fp"
        ? "Add Physicochemical Test"
        : "Add Microbiology Test";

  const isReady = sourceChildren.ready && (!duplicateFrom || sectionsLoaded);
  const baseDraftKey = editingTest
    ? `test-edit-${editingTest.id}`
    : duplicateFrom
      ? `test-dup-${duplicateFrom.id}`
      : `test-create-${lab}-${area ?? "default"}`;
  const draftKey = isReady ? baseDraftKey : `${baseDraftKey}:loading`;

  return (
    <ConfigWizard
      open={open}
      title={title}
      mode={wizardMode}
      steps={steps}
      initial={initialModel}
      original={isEditing ? initialModel : undefined}
      draftKey={draftKey}
      draftFields={[...TEST_DRAFT_FIELDS]}
      validateOnServer={validateOnServer}
      usage={usageFn}
      reasonRequired={(u) => (u?.openWorkCount ?? 0) > 0}
      reloadOriginal={reloadOriginal}
      onSave={handleSave}
      onClose={onClose}
      extraReview={(model) => (
        <AnalystPreview
          steps={previewModel(model, {
            mediaProducts: lookupRef.current.mediaProducts,
            conditions: lookupRef.current.conditions
          })}
        />
      )}
    />
  );
}
