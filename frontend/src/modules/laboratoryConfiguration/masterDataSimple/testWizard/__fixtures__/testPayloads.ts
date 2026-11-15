import type {
  CreateTestDefinitionPayload,
  UpdateTestDefinitionPayload
} from "../../../../../services/masterDataOptions";
import type { IcpTestPayloadFields } from "../../testMaster/icpTestConfig";

export interface TestPayloadFixtures {
  countTest: CreateTestDefinitionPayload;
  observation: CreateTestDefinitionPayload;
  qualitative: CreateTestDefinitionPayload;
  measurement: CreateTestDefinitionPayload;
  gravimetricLoss: CreateTestDefinitionPayload;
  gravimetricResidue: CreateTestDefinitionPayload;
  dissolution: CreateTestDefinitionPayload;
  disintegration: CreateTestDefinitionPayload;
  weightVariation: CreateTestDefinitionPayload;
  titration: CreateTestDefinitionPayload;
  titrationKarlFischer: CreateTestDefinitionPayload;
  hplcAssay: CreateTestDefinitionPayload;
  icpAssay: CreateTestDefinitionPayload;
  icpImpurity: CreateTestDefinitionPayload;
  uvStandardComparison: CreateTestDefinitionPayload;
  uvSpecificAbsorbanceAssay: CreateTestDefinitionPayload;
  measurementEdit: UpdateTestDefinitionPayload;
  titrationEdit: UpdateTestDefinitionPayload;
}

const emptyCreateBase: Omit<
  CreateTestDefinitionPayload,
  "code" | "displayName" | "sectionId" | "workflowType" | "equationType"
> = {
  coaDisplayOrder: null,
  perContainer: false,
  moistureRole: "None",
  requiresSystemSuitability: false,
  methodAbbreviation: null,
  replicateCount: null,
  evaluationBasis: null,
  conditionFields: null,
  usesTare: null,
  dissolutionS1Offset: null,
  dissolutionS2MinOffset: null,
  dissolutionS3MinOffset: null,
  dissolutionS3MaxBelowS2Min: null,
  disintegrationStage1Units: null,
  disintegrationStage2Units: null,
  disintegrationMaxStage1Failures: null,
  disintegrationMinPassTotal: null,
  wvUnitCount: null,
  wvTabletBand1MaxMg: null,
  wvTabletBand1Percent: null,
  wvTabletBand2MaxMg: null,
  wvTabletBand2Percent: null,
  wvTabletBand3Percent: null,
  wvTabletMaxOutside: null,
  wvCapsuleInnerPercent: null,
  wvCapsuleOuterPercent: null,
  wvCapsuleS1MaxOutside: null,
  wvCapsuleS1MaxForRetest: null,
  wvCapsuleS2ExtraUnits: null,
  wvCapsuleS2MaxOutside: null,
  titrationType: null,
  titrationNonAqueous: null,
  titrationMode: null,
  titrationCalculation: null,
  titrantSolutionMasterId: null,
  titrationEquivalencyFactor: null,
  titrationBlankRequired: null,
  titrationExcessSolutionMasterId: null,
  titrationExcessVolumeMl: null,
  titrationMaxRsdPercent: null,
  titrationEndpoint: null,
  titrationIndicatorEntryId: null,
  titrationTempCorrection: null,
  titrationExpansionCoefficient: null,
  titrationStandardEntryId: null,
  physchemArea: "FinishedProduct",
  hplcMethodId: null,
  uvCalculationMode: null,
  uvSpecificAbsorbance: null,
  uvTotalVolumeMl: null,
  uvStandardVolumeMl: null,
  uvStandardEntryId: null
};

const icpAssayElements: IcpTestPayloadFields["icpElements"] = [
  { id: null, symbol: "Fe", targetPpm: 50, cleaningLimitPpm: 10 }
];

const icpImpurityElements: IcpTestPayloadFields["icpElements"] = [
  { id: null, symbol: "Pb", targetPpm: null, cleaningLimitPpm: null }
];

export const TEST_PAYLOAD_FIXTURES: TestPayloadFixtures = {
  countTest: {
    ...emptyCreateBase,
    code: "TAMC",
    displayName: "Total Aerobic Microbial Count",
    coaDisplayOrder: 1,
    sectionId: 2,
    workflowType: "CountTest",
    equationType: "None",
    physchemArea: null
  },
  observation: {
    ...emptyCreateBase,
    code: "ECOLI",
    displayName: "Escherichia coli",
    sectionId: 2,
    workflowType: "Observation",
    equationType: "None",
    physchemArea: null
  },
  qualitative: {
    ...emptyCreateBase,
    code: "APP",
    displayName: "Appearance",
    coaDisplayOrder: 2,
    sectionId: 1,
    workflowType: "Qualitative",
    equationType: "Qualitative",
    perContainer: true
  },
  measurement: {
    ...emptyCreateBase,
    code: "PH",
    displayName: "pH",
    coaDisplayOrder: 3,
    sectionId: 1,
    workflowType: "Measurement",
    equationType: "Measurement",
    replicateCount: 3,
    evaluationBasis: "Mean"
  },
  gravimetricLoss: {
    ...emptyCreateBase,
    code: "LOD",
    displayName: "Loss on Drying",
    sectionId: 1,
    workflowType: "Gravimetric",
    equationType: "GravimetricLoss",
    replicateCount: 2,
    usesTare: true,
    conditionFields: "Temperature (°C),Time (h)",
    moistureRole: "LossOnDrying"
  },
  gravimetricResidue: {
    ...emptyCreateBase,
    code: "ASH",
    displayName: "Sulfated Ash",
    sectionId: 1,
    workflowType: "Gravimetric",
    equationType: "GravimetricResidue",
    replicateCount: 2,
    usesTare: false,
    conditionFields: "Temperature (°C)"
  },
  dissolution: {
    ...emptyCreateBase,
    code: "DISS",
    displayName: "Dissolution",
    sectionId: 1,
    workflowType: "Dissolution",
    equationType: "Dissolution",
    requiresSystemSuitability: true,
    hplcMethodId: 10,
    methodAbbreviation: "DISS-UV",
    conditionFields: "Apparatus,RPM,Medium",
    dissolutionS1Offset: 5,
    dissolutionS2MinOffset: 15,
    dissolutionS3MinOffset: 25,
    dissolutionS3MaxBelowS2Min: 2
  },
  disintegration: {
    ...emptyCreateBase,
    code: "DISINT",
    displayName: "Disintegration",
    sectionId: 1,
    workflowType: "Disintegration",
    equationType: "Disintegration",
    conditionFields: "Medium,Temperature (°C)",
    disintegrationStage1Units: 6,
    disintegrationStage2Units: 12,
    disintegrationMaxStage1Failures: 2,
    disintegrationMinPassTotal: 16
  },
  weightVariation: {
    ...emptyCreateBase,
    code: "WV",
    displayName: "Weight Variation",
    sectionId: 1,
    workflowType: "WeightVariation",
    equationType: "WeightVariation",
    conditionFields: "Balance ID",
    wvUnitCount: 20,
    wvTabletBand1MaxMg: 130,
    wvTabletBand1Percent: 10,
    wvTabletBand2MaxMg: 324,
    wvTabletBand2Percent: 7.5,
    wvTabletBand3Percent: 5,
    wvTabletMaxOutside: 2,
    wvCapsuleInnerPercent: 10,
    wvCapsuleOuterPercent: 25,
    wvCapsuleS1MaxOutside: 2,
    wvCapsuleS1MaxForRetest: 6,
    wvCapsuleS2ExtraUnits: 40,
    wvCapsuleS2MaxOutside: 6
  },
  titration: {
    ...emptyCreateBase,
    code: "TITR-AB",
    displayName: "Ascorbic Acid Titration",
    sectionId: 1,
    workflowType: "Titration",
    equationType: "Titration",
    replicateCount: 3,
    titrationType: "AcidBase",
    titrationNonAqueous: false,
    titrationMode: "Direct",
    titrationCalculation: "UspFactor",
    titrantSolutionMasterId: 101,
    titrationEquivalencyFactor: 88.06,
    titrationBlankRequired: true,
    titrationExcessSolutionMasterId: null,
    titrationExcessVolumeMl: null,
    titrationMaxRsdPercent: 1.5,
    titrationEndpoint: "Visual",
    titrationIndicatorEntryId: 201,
    titrationTempCorrection: null,
    titrationExpansionCoefficient: null,
    titrationStandardEntryId: null
  },
  titrationKarlFischer: {
    ...emptyCreateBase,
    code: "KF-H2O",
    displayName: "Water Determination KF",
    sectionId: 1,
    workflowType: "Titration",
    equationType: "Titration",
    replicateCount: 3,
    moistureRole: "Water",
    titrationType: "KarlFischer",
    titrationNonAqueous: null,
    titrationMode: "Direct",
    titrationCalculation: "UspFactor",
    titrantSolutionMasterId: 102,
    titrationEquivalencyFactor: null,
    titrationBlankRequired: false,
    titrationExcessSolutionMasterId: null,
    titrationExcessVolumeMl: null,
    titrationMaxRsdPercent: null,
    titrationEndpoint: "Potentiometric",
    titrationIndicatorEntryId: null,
    titrationTempCorrection: null,
    titrationExpansionCoefficient: null,
    titrationStandardEntryId: null
  },
  hplcAssay: {
    ...emptyCreateBase,
    code: "HPLC-IBU",
    displayName: "Ibuprofen Assay",
    sectionId: 1,
    workflowType: "HplcMethodAssay",
    equationType: "HplcMethodAssay",
    requiresSystemSuitability: true,
    hplcMethodId: 10,
    methodAbbreviation: "IBU-A"
  },
  icpAssay: {
    ...emptyCreateBase,
    code: "ICP-FE",
    displayName: "Iron Assay by ICP",
    sectionId: 1,
    workflowType: "IcpMethodAssay",
    equationType: "IcpMethodAssay",
    methodAbbreviation: "",
    icpMode: "Assay",
    icpSampleForm: "Solid",
    icpTheoreticalAmount: 0.5,
    icpSpecificGravity: null,
    icpElements: icpAssayElements
  },
  icpImpurity: {
    ...emptyCreateBase,
    code: "ICP-PB",
    displayName: "Lead Impurity by ICP",
    sectionId: 1,
    workflowType: "IcpMethodAssay",
    equationType: "IcpMethodAssay",
    methodAbbreviation: "",
    icpMode: "Impurity",
    icpSampleForm: null,
    icpTheoreticalAmount: null,
    icpSpecificGravity: null,
    icpElements: icpImpurityElements
  },
  uvStandardComparison: {
    ...emptyCreateBase,
    code: "UV-STD",
    displayName: "UV Assay against Standard",
    sectionId: 1,
    workflowType: "UvSpectrophotometry",
    equationType: "UvSpectrophotometry",
    replicateCount: 3,
    uvCalculationMode: "StandardComparison",
    uvTotalVolumeMl: 100,
    uvStandardVolumeMl: 50,
    uvStandardEntryId: 301
  },
  uvSpecificAbsorbanceAssay: {
    ...emptyCreateBase,
    code: "UV-A1",
    displayName: "Riboflavin A1% Assay",
    sectionId: 1,
    workflowType: "UvSpectrophotometry",
    equationType: "UvSpectrophotometry",
    replicateCount: 2,
    uvCalculationMode: "SpecificAbsorbanceAssay",
    uvSpecificAbsorbance: 328,
    uvTotalVolumeMl: 5000
  },
  measurementEdit: {
    ...emptyCreateBase,
    coaDisplayOrder: 0,
    code: "PH",
    displayName: "pH (25 °C)",
    sectionId: 1,
    workflowType: "Measurement",
    equationType: "Measurement",
    replicateCount: 2,
    evaluationBasis: "Mean",
    changeReason: null
  },
  titrationEdit: {
    ...emptyCreateBase,
    coaDisplayOrder: 0,
    code: "TITR-AB",
    displayName: "Ascorbic Acid Titration",
    sectionId: 1,
    workflowType: "Titration",
    equationType: "Titration",
    replicateCount: 3,
    titrationType: "AcidBase",
    titrationNonAqueous: false,
    titrationMode: "Direct",
    titrationCalculation: "UspFactor",
    titrantSolutionMasterId: 101,
    titrationEquivalencyFactor: 88.1,
    titrationBlankRequired: true,
    titrationExcessSolutionMasterId: null,
    titrationExcessVolumeMl: null,
    titrationMaxRsdPercent: 1.5,
    titrationEndpoint: "Visual",
    titrationIndicatorEntryId: 201,
    titrationTempCorrection: null,
    titrationExpansionCoefficient: null,
    titrationStandardEntryId: null,
    changeReason: "SOP revision 4 updated equivalency factor."
  }
};
