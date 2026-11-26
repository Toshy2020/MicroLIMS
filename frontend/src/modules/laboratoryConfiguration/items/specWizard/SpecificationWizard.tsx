import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "../../../../i18n";
import { ConfigWizard } from "../../../../components/config/ConfigWizard";
import type { WizardSaveContext, WizardStep } from "../../../../components/config/useWizard";
import { useLaboratorySections } from "../../../../hooks/useLaboratorySections";
import { useMyLabs } from "../../../../hooks/useMyLabs";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { masterDataUsageService } from "../../../../services/masterDataUsageService";
import {
  HplcMethodService,
  type HplcMethodResponse
} from "../../masterDataSimple/services/HplcMethodService";
import { areaIncludes, areaOfItemCategory } from "../../masterDataSimple/testMasterArea";
import { collapseGrades } from "../../specifications/services/gradeGroups";
import {
  SpecificationService,
  type CreateSpecificationPayload,
  type SpecificationDto,
  type UpdateSpecificationPayload
} from "../../specifications/services/SpecificationService";
import type { Item } from "../services/ItemService";
import { createLimitStep } from "./LimitStep";
import { useChangeControlGate } from "../../../../hooks/useChangeControlGate";
import { createMethodStep } from "./MethodStep";
import { createTestParameterStep } from "./TestParameterStep";
import { createUnitDilutionStep } from "./UnitDilutionStep";
import {
  fromSpecification,
  SPEC_WIZARD_STEP_ORDER,
  toBasePayload,
  toPayload,
  type SpecFormModel,
  type SpecWizardStep
} from "./specFormModel";
import {
  buildInitialSpecModel,
  findNextTestWithoutSpec,
  planSpecGradeOperations,
  SPEC_DRAFT_FIELDS,
  type SpecWizardLookupData,
  type TestDefinitionSummary
} from "./specHelpers";

export interface SpecificationWizardProps {
  open: boolean;
  item: Item;
  editingSpec?: SpecificationDto | null;
  preselectedTestCode?: string | null;
  workflowTypeByCode: Record<string, string>;
  testDefinitionByCode?: Record<string, TestDefinitionSummary>;
  existingSpecs: SpecificationDto[];
  onClose: () => void;
  onSuccess?: () => void | Promise<void>;
  onSaved?: () => void | Promise<void>;
}

export type { TestDefinitionSummary };

/**
 * Step-by-step specification parameter wizard built on ConfigWizard.
 * Preserves the exact validation rules, payload shape, grade expansion,
 * and X-Change-Reason handling of SpecificationParameterDialog.
 */
export function SpecificationWizard(props: SpecificationWizardProps) {
  if (!props.open) return null;
  return <OpenSpecificationWizard {...props} />;
}

function OpenSpecificationWizard({
  open,
  item,
  editingSpec,
  preselectedTestCode,
  workflowTypeByCode,
  testDefinitionByCode,
  existingSpecs,
  onClose,
  onSuccess,
  onSaved
}: SpecificationWizardProps) {
  const { t } = useTranslation("labConfig");
  const isEditing = Boolean(editingSpec && editingSpec.id != null);
  const assignedTests = useMemo(() => item.assignedTests ?? [], [item.assignedTests]);

  const { codes: myLabCodes } = useMyLabs();
  const { sections } = useLaboratorySections();
  const myLabSectionIds = useMemo(
    () => new Set(sections.filter((s) => myLabCodes.includes(s.sectionCode)).map((s) => s.sectionId)),
    [sections, myLabCodes]
  );

  const [testDefs, setTestDefs] = useState<Record<string, TestDefinitionSummary>>(
    testDefinitionByCode || {}
  );
  const [testDefsFailed, setTestDefsFailed] = useState(false);
  const [hplcMethodById, setHplcMethodById] = useState<Record<number, HplcMethodResponse>>({});
  const hplcMethodByIdRef = useRef(hplcMethodById);
  hplcMethodByIdRef.current = hplcMethodById;
  const [loadingHplcMethodId, setLoadingHplcMethodId] = useState<number | null>(null);
  const fetchingMethodIdsRef = useRef<Set<number>>(new Set());

  useEffect(() => {
    if (testDefinitionByCode && Object.keys(testDefinitionByCode).length > 0) {
      setTestDefs(testDefinitionByCode);
      setTestDefsFailed(false);
    } else {
      masterDataOptions
        .getTestDefinitions()
        .then((defs: TestDefinitionSummary[]) => {
          setTestDefs(Object.fromEntries(defs.map((d) => [d.code, d])));
          setTestDefsFailed(false);
        })
        .catch(() => setTestDefsFailed(true));
    }
  }, [testDefinitionByCode]);

  const [continuedState, setContinuedState] = useState<{ testCode: string | null; seq: number }>({
    testCode: null,
    seq: 0
  });

  const activePreselectedTestCode = continuedState.testCode ?? preselectedTestCode;
  const isTestDisabled = isEditing || Boolean(activePreselectedTestCode);

  const ownLabAssignedTests = useMemo(
    () =>
      assignedTests.filter((t) => {
        const def = testDefs[t.testCode];
        const sectionId = def?.sectionId;
        return (
          areaIncludes(def?.physchemArea, areaOfItemCategory(item.category)) &&
          (sectionId == null || myLabSectionIds.size === 0 || myLabSectionIds.has(sectionId))
        );
      }),
    [assignedTests, testDefs, myLabSectionIds, item.category]
  );
  const selectableAssignedTests = isTestDisabled ? assignedTests : ownLabAssignedTests;

  const ensureHplcMethod = useCallback((methodId: number) => {
    if (hplcMethodByIdRef.current[methodId] || fetchingMethodIdsRef.current.has(methodId)) return;
    fetchingMethodIdsRef.current.add(methodId);
    setLoadingHplcMethodId(methodId);
    HplcMethodService.getById(methodId)
      .then((res) => {
        setHplcMethodById((prev) => ({ ...prev, [methodId]: res }));
      })
      .catch(() => {
        // Non-fatal: MethodStep displays empty analytes/impurities message.
      })
      .finally(() => {
        fetchingMethodIdsRef.current.delete(methodId);
        setLoadingHplcMethodId((cur) => (cur === methodId ? null : cur));
      });
  }, []);

  const lookupData = useMemo<SpecWizardLookupData>(
    () => ({
      assignedTests,
      selectableAssignedTests,
      isTestDisabled,
      testDefs,
      testDefsFailed,
      workflowTypeByCode,
      hplcMethodById,
      loadingHplcMethodId,
      ensureHplcMethod
    }),
    [
      assignedTests,
      selectableAssignedTests,
      isTestDisabled,
      testDefs,
      testDefsFailed,
      workflowTypeByCode,
      hplcMethodById,
      loadingHplcMethodId,
      ensureHplcMethod
    ]
  );

  const lookupRef = useRef(lookupData);
  lookupRef.current = lookupData;

  const steps = useMemo<WizardStep<SpecFormModel>[]>(() => {
    const getLookup = () => lookupRef.current;
    const stepMap: Record<SpecWizardStep, WizardStep<SpecFormModel>> = {
      test: createTestParameterStep(getLookup, t),
      method: createMethodStep(getLookup, t),
      limit: createLimitStep(getLookup, t),
      unit: createUnitDilutionStep(t)
    };
    return SPEC_WIZARD_STEP_ORDER.map((key) => stepMap[key]);
  }, [t]);

  const initialModel = useMemo(
    () =>
      buildInitialSpecModel({
        itemId: item.id,
        itemCategory: item.category,
        editingSpec,
        preselectedTestCode: activePreselectedTestCode,
        assignedTests,
        selectableAssignedTests,
        workflowTypeByCode,
        testDefs,
        existingSpecs
      }),
    [
      item.id,
      item.category,
      editingSpec,
      activePreselectedTestCode,
      assignedTests,
      selectableAssignedTests,
      workflowTypeByCode,
      testDefs,
      existingSpecs
    ]
  );

  const validateOnServer = useCallback(
    (model: SpecFormModel) => {
      const payload = toPayload(model);
      const editId = model.editingId ?? undefined;
      return SpecificationService.validate(
        { ...payload, itemId: item.id, ...(editId != null ? { id: editId } : {}) },
        editId
      );
    },
    [item.id]
  );

  const usageFn = useMemo(
    () =>
      isEditing && editingSpec?.id != null
        ? () => masterDataUsageService.getUsage("Specification", editingSpec.id!)
        : undefined,
    [isEditing, editingSpec?.id]
  );

  const reloadedSpecRef = useRef<SpecificationDto | null>(null);
  useEffect(() => {
    reloadedSpecRef.current = null;
  }, [editingSpec?.id]);

  const reloadOriginal = useMemo(
    () =>
      isEditing && editingSpec?.id != null
        ? async () => {
            const all = await SpecificationService.getForItem(item.id);
            const collapsed = collapseGrades(all);
            const fresh =
              collapsed.find((s) => s.id === editingSpec.id) ??
              all.find((s) => s.id === editingSpec.id);
            if (!fresh) {
              throw new Error(t("specWizard.specMissingError"));
            }
            reloadedSpecRef.current = fresh;
            return fromSpecification(fresh, {
              itemId: item.id,
              itemCategory: item.category,
              workflowType: workflowTypeByCode[fresh.testCode],
              testDef: lookupRef.current.testDefs[fresh.testCode],
              existingSpecs: all
            });
          }
        : undefined,
    [isEditing, editingSpec?.id, item.id, item.category, workflowTypeByCode, t]
  );

  // Every specification write is gated (CONF-227): a refusal opens the change-control prompt for that
  // write, and the number the server accepted is reused for the rest of this editing session.
  const gate = useChangeControlGate({
    sectionId: null,
    recordKey: isEditing && editingSpec?.id != null ? `spec-${editingSpec.id}` : `spec-new-${item.id}`
  });
  const gateRun = gate.run;

  const handleSave = useCallback(
    async (model: SpecFormModel, reason: string | null, saveCtx: WizardSaveContext<SpecFormModel>) => {
      const scope = { sectionId: lookupRef.current.testDefs[model.testCode]?.sectionId ?? null };
      const basePayload: UpdateSpecificationPayload = {
        ...toBasePayload(model),
        grade: null
      };
      const activeSpec = reloadedSpecRef.current ?? editingSpec;
      const { writes, removes } = planSpecGradeOperations(model, isEditing, activeSpec);

      let wrote = false;
      try {
        for (const w of writes) {
          if (w.existing?.id != null) {
            const updatePayload: UpdateSpecificationPayload = { ...basePayload, grade: w.grade };
            const version = reloadedSpecRef.current
              ? w.existing.version
              : w.existing.id === editingSpec?.id
                ? (saveCtx.recordVersion ?? w.existing.version)
                : w.existing.version;
            const specId = w.existing.id;
            await gateRun(
              (cc) =>
                cc
                  ? SpecificationService.update(specId, updatePayload, version, reason ?? undefined, cc)
                  : reason
                    ? SpecificationService.update(specId, updatePayload, version, reason)
                    : SpecificationService.update(specId, updatePayload, version),
              scope
            );
          } else {
            const createPayload: CreateSpecificationPayload = {
              itemId: item.id,
              ...basePayload,
              grade: w.grade
            };
            await gateRun((cc) => (cc ? SpecificationService.create(createPayload, cc) : SpecificationService.create(createPayload)), scope);
          }
          wrote = true;
        }

        for (const r of removes) {
          if (r.id != null) {
            const removeId = r.id;
            await gateRun(
              (cc) =>
                cc
                  ? SpecificationService.remove(removeId, reason ?? undefined, cc)
                  : reason
                    ? SpecificationService.remove(removeId, reason)
                    : SpecificationService.remove(removeId),
              scope
            );
            wrote = true;
          }
        }

        await Promise.resolve(onSuccess?.());
        await Promise.resolve(onSaved?.());
      } catch (err: unknown) {
        if (wrote) {
          await Promise.resolve(onSuccess?.());
        }
        throw err;
      }
    },
    [isEditing, editingSpec, item.id, gateRun, onSuccess, onSaved]
  );

  const saveAndNext = useCallback(
    (model: SpecFormModel) => {
      const next = findNextTestWithoutSpec({
        isEditing,
        currentTestCode: model.testCode,
        ownLabAssignedTests,
        existingSpecs
      });
      if (!next) return null;
      return {
        label: t("specWizard.saveAndNext", { name: next.displayName || next.testCode }),
        onSaved: () => {
          setContinuedState((prev) => ({ testCode: next.testCode, seq: prev.seq + 1 }));
        }
      };
    },
    [isEditing, ownLabAssignedTests, existingSpecs, t]
  );

  const draftKey =
    isEditing && editingSpec?.id != null
      ? `spec-edit-${editingSpec.id}`
      : `spec-create-${item.id}-${activePreselectedTestCode ?? "any"}-${continuedState.seq}`;

  return (
    <>
    <ConfigWizard
      open={open}
      title={isEditing ? t("specWizard.titleEdit") : t("specWizard.titleAdd")}
      mode={isEditing ? "edit" : "create"}
      steps={steps}
      initial={initialModel}
      original={isEditing ? initialModel : undefined}
      recordVersion={isEditing ? (editingSpec?.version ?? null) : null}
      getRecordVersion={() => reloadedSpecRef.current?.version ?? null}
      draftKey={draftKey}
      draftFields={SPEC_DRAFT_FIELDS}
      validateOnServer={validateOnServer}
      usage={usageFn}
      reasonRequired={(u, m) =>
        (u?.openWorkCount ?? 0) > 0 ||
        planSpecGradeOperations(m, isEditing, editingSpec).removes.length > 0
      }
      reloadOriginal={reloadOriginal}
      liveRecord={isEditing ? { type: "specification", id: editingSpec?.id ?? null } : undefined}
      onSave={handleSave}
      saveAndNext={saveAndNext}
      onClose={onClose}
    />
    {gate.prompt}
    </>
  );
}
