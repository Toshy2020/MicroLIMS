import { useEffect, useState } from "react";
import {
  masterDataOptions,
  CreateTestDefinitionPayload,
  UpdateTestDefinitionPayload
} from "../services/masterDataOptions";

export interface TestDefinitionOption {
  id: number;
  code: string;
  displayName: string;
  isActive: boolean;
  workflowType: string;
  equationType?: string;
  requiresSystemSuitability?: boolean;
  methodAbbreviation?: string | null;
  sstMaxRsdPercent?: number | null;
  sstMinResolution?: number | null;
  sstMaxTailingFactor?: number | null;
  sstMinTheoreticalPlates?: number | null;
  calibrationEntryMode?: string | null;
  calMinCorrelation?: number | null;
  calCorrelationType?: string | null;
  calMinStandards?: number | null;
  calCheckRecoveryLowPercent?: number | null;
  calCheckRecoveryHighPercent?: number | null;
  calBlankMax?: number | null;
  calIsRecoveryLowPercent?: number | null;
  calIsRecoveryHighPercent?: number | null;
  calRequireBlank?: boolean | null;
  calRequireIcv?: boolean | null;
  calRequireCcv?: boolean | null;
  calRequireInternalStandard?: boolean | null;
  reportedConcentrationBasis?: string | null;
  calMaxRunAgeHours?: number | null;
  // CalibrationCurve only: instrument family (null = ICP-OES, existing rows) and
  // optional fixed standard levels (e.g. "1, 5"; null = no fixed levels configured).
  calInstrumentType?: "IcpOes" | "Aas" | null;
  calStandardLevelsMgPerL?: string | null;
  replicateCount?: number | null;
  evaluationBasis?: "Mean" | "EachValue" | "Min" | "Max" | null;
  conditionFields?: string | null;
  usesTare?: boolean | null;
  dissolutionS1Offset?: number | null;
  dissolutionS2MinOffset?: number | null;
  dissolutionS3MinOffset?: number | null;
  dissolutionS3MaxBelowS2Min?: number | null;
  disintegrationStage1Units?: number | null;
  disintegrationStage2Units?: number | null;
  disintegrationMaxStage1Failures?: number | null;
  disintegrationMinPassTotal?: number | null;
  wvUnitCount?: number | null;
  wvTabletBand1MaxMg?: number | null;
  wvTabletBand1Percent?: number | null;
  wvTabletBand2MaxMg?: number | null;
  wvTabletBand2Percent?: number | null;
  wvTabletBand3Percent?: number | null;
  wvTabletMaxOutside?: number | null;
  wvCapsuleInnerPercent?: number | null;
  wvCapsuleOuterPercent?: number | null;
  wvCapsuleS1MaxOutside?: number | null;
  wvCapsuleS1MaxForRetest?: number | null;
  wvCapsuleS2ExtraUnits?: number | null;
  wvCapsuleS2MaxOutside?: number | null;
  hplcMaxPreparationRsdPercent?: number | null;
  // StandardComparison only - how the assay measures its response (SC-5a).
  // Absent/undefined on non-StandardComparison tests.
  responseMode?: "PeakArea" | "TitrationVolume" | null;
  sectionId?: number;
  section?: {
    id: number;
    name: string;
    code: string;
  };
}

// Backs every TestCode picker in the app (Items, Water Sampling Points,
// Room Test Configurations, Machine Part Configurations) with the one
// canonical Test Master list, instead of each screen free-typing codes
// by hand. See backend TestDefinition.cs for why this exists.
//
// `options` is the full list (active + frozen) - TestMasterPage needs
// to see everything. `activeOptions` is what the pickers should offer
// for a *new* selection; frozen tests are deliberately excluded there
// but a picker's already-selected value is looked up against the full
// `options` list so an existing assignment to a since-frozen test still
// renders correctly.
export function useTestDefinitions() {
  const [options, setOptions] = useState<TestDefinitionOption[]>([]);
  const [loading, setLoading] = useState(true);

  const reload = () => masterDataOptions.getTestDefinitions().then((data: TestDefinitionOption[]) => {
    setOptions(data);
    setLoading(false);
  });

  useEffect(() => { reload(); }, []);

  // Adds a brand-new test to the Test Master (used when the analyst
  // types a code that doesn't exist yet) and returns it so the caller
  // can select it immediately.
  const addNew = async (
    codeOrPayload: string | CreateTestDefinitionPayload,
    displayName?: string,
    sectionId?: number | null
  ) => {
    const created = await masterDataOptions.createTestDefinition(codeOrPayload, displayName, sectionId);
    await reload();
    return created as TestDefinitionOption;
  };

  const update = async (
    id: number,
    codeOrPayload: string | UpdateTestDefinitionPayload,
    displayName?: string,
    sectionId?: number | null
  ) => {
    const updated = await masterDataOptions.updateTestDefinition(id, codeOrPayload, displayName, sectionId);
    await reload();
    return updated as TestDefinitionOption;
  };

  const setActive = async (id: number, isActive: boolean) => {
    if (isActive) await masterDataOptions.unfreezeTestDefinition(id);
    else await masterDataOptions.freezeTestDefinition(id);
    await reload();
  };

  return { options, activeOptions: options.filter((o) => o.isActive), loading, addNew, update, setActive, reload };
}
