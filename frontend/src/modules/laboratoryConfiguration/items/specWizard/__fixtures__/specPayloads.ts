import type {
  CreateSpecificationPayload,
  UpdateSpecificationPayload
} from "../../../specifications/services/SpecificationService";

export interface SpecPayloadFixtures {
  range: CreateSpecificationPayload;
  notMoreThan: CreateSpecificationPayload;
  notLessThan: CreateSpecificationPayload;
  targetWithTolerance: CreateSpecificationPayload;
  countTiered: CreateSpecificationPayload;
  qualitative: CreateSpecificationPayload;
  presenceAbsence: CreateSpecificationPayload;
  multiStage: CreateSpecificationPayload;
  dissolutionQ: CreateSpecificationPayload;
  disintegrationTime: CreateSpecificationPayload;
  weightVariation: CreateSpecificationPayload;
  hplcAssay: CreateSpecificationPayload;
  hplcRelatedSubstances: CreateSpecificationPayload;
  hplcResidualSolvents: CreateSpecificationPayload;
  icpAssay: CreateSpecificationPayload;
  icpImpurity: CreateSpecificationPayload;
  titration: CreateSpecificationPayload;
  rangeEdit: UpdateSpecificationPayload;
}

const emptyBase: Omit<CreateSpecificationPayload, "itemId" | "testCode"> = {
  parameterName: undefined,
  displayOrder: 0,
  limitType: "Range",
  referenceStandard: null,
  unit: null,
  dosageForm: null,
  dilutionFactor: null,
  lowerLimit: null,
  upperLimit: null,
  lowerInclusive: undefined,
  upperInclusive: undefined,
  target: null,
  tolerance: null,
  toleranceMode: null,
  expectedResultText: null,
  expectedState: null,
  sampleQuantity: null,
  sampleQuantityUnit: null,
  alertLimit: null,
  actionLimit: null,
  specLimit: null,
  stages: undefined,
  hplcMethodAnalyteId: null,
  relatedSubstanceRow: null,
  hplcMethodImpurityId: null,
  icpTestElementId: null,
  resultBasis: null,
  labelClaim: null,
  labelClaimUnit: null,
  productionStageRole: null,
  grade: null
};

export const SPEC_PAYLOAD_FIXTURES: SpecPayloadFixtures = {
  range: {
    itemId: 7,
    ...emptyBase,
    testCode: "PH",
    parameterName: "pH",
    limitType: "Range",
    referenceStandard: "USP <791>",
    unit: "pH units",
    lowerLimit: 5,
    upperLimit: 7,
    lowerInclusive: true,
    upperInclusive: false,
    productionStageRole: "Finished"
  },
  notMoreThan: {
    itemId: 7,
    ...emptyBase,
    testCode: "LOD",
    parameterName: "Loss on Drying",
    limitType: "NotMoreThan",
    referenceStandard: "USP <731>",
    unit: "%",
    upperLimit: 0.5
  },
  notLessThan: {
    itemId: 7,
    ...emptyBase,
    testCode: "POT",
    parameterName: "Potency",
    limitType: "NotLessThan",
    unit: "%",
    lowerLimit: 95
  },
  targetWithTolerance: {
    itemId: 7,
    ...emptyBase,
    testCode: "DENS",
    parameterName: "Density",
    limitType: "TargetWithTolerance",
    unit: "g/mL",
    target: 1.02,
    tolerance: 5,
    toleranceMode: "Percent"
  },
  countTiered: {
    itemId: 7,
    ...emptyBase,
    testCode: "TAMC",
    parameterName: "Total Aerobic Microbial Count",
    limitType: "CountTiered",
    referenceStandard: "USP <61>",
    unit: "CFU/g",
    dilutionFactor: 10,
    alertLimit: "100",
    actionLimit: "500",
    specLimit: "1000"
  },
  qualitative: {
    itemId: 7,
    ...emptyBase,
    testCode: "APP",
    parameterName: "Appearance",
    limitType: "Qualitative",
    expectedResultText: "White round tablets"
  },
  presenceAbsence: {
    itemId: 7,
    ...emptyBase,
    testCode: "ECOLI",
    parameterName: "E. coli",
    limitType: "PresenceAbsence",
    referenceStandard: "USP <62>",
    expectedState: "Absence",
    sampleQuantity: 10,
    sampleQuantityUnit: "g"
  },
  multiStage: {
    itemId: 7,
    ...emptyBase,
    testCode: "UNIF",
    parameterName: "Content Uniformity",
    limitType: "MultiStage",
    unit: "%",
    stages: [
      {
        stageNumber: 1,
        stageLabel: "Stage 1 (S1, n=6)",
        acceptanceCriteriaText: "Each unit >= 85%"
      },
      {
        stageNumber: 2,
        stageLabel: "Stage 2 (S1+S2, n=12)",
        acceptanceCriteriaText: "Average >= 75%"
      }
    ]
  },
  dissolutionQ: {
    itemId: 7,
    ...emptyBase,
    testCode: "DISS",
    parameterName: "Dissolution",
    limitType: "DissolutionQ",
    referenceStandard: "USP <711>",
    unit: "%",
    lowerLimit: 75,
    specLimit: "Q = 75 %",
    labelClaim: 500,
    labelClaimUnit: "mg"
  },
  disintegrationTime: {
    itemId: 7,
    ...emptyBase,
    testCode: "DISINT",
    parameterName: "Disintegration",
    limitType: "DisintegrationTime",
    referenceStandard: "USP <701>",
    unit: "min",
    upperLimit: 15,
    specLimit: "NMT 15 min"
  },
  weightVariation: {
    itemId: 7,
    ...emptyBase,
    testCode: "WV",
    parameterName: "Weight Variation",
    limitType: "WeightVariation",
    referenceStandard: "USP <2091>",
    unit: "mg",
    dosageForm: "Tablet",
    specLimit: "USP <2091>: tablets, limit by average weight"
  },
  hplcAssay: {
    itemId: 7,
    ...emptyBase,
    testCode: "HPLC_A",
    parameterName: "Ibuprofen (amount per unit)",
    limitType: "Range",
    unit: "mg",
    lowerLimit: 380,
    upperLimit: 420,
    lowerInclusive: true,
    upperInclusive: true,
    hplcMethodAnalyteId: 601,
    resultBasis: "MgPerUnit",
    labelClaim: 400,
    labelClaimUnit: "mg"
  },
  hplcRelatedSubstances: {
    itemId: 7,
    ...emptyBase,
    testCode: "HPLC_RS",
    parameterName: "Impurity A",
    limitType: "NotMoreThan",
    unit: "%",
    upperLimit: 0.15,
    hplcMethodAnalyteId: 501,
    relatedSubstanceRow: "Named",
    hplcMethodImpurityId: 101,
    resultBasis: "PercentAsIs"
  },
  hplcResidualSolvents: {
    itemId: 7,
    ...emptyBase,
    testCode: "GC_RS",
    parameterName: "Methanol",
    limitType: "NotMoreThan",
    unit: "ppm",
    upperLimit: 3000,
    hplcMethodAnalyteId: 701,
    resultBasis: "Ppm"
  },
  icpAssay: {
    itemId: 7,
    ...emptyBase,
    testCode: "ICP_A",
    parameterName: "Fe",
    limitType: "Range",
    unit: "%",
    lowerLimit: 90,
    upperLimit: 110,
    lowerInclusive: true,
    upperInclusive: true,
    icpTestElementId: 11
  },
  icpImpurity: {
    itemId: 7,
    ...emptyBase,
    testCode: "ICP_IMP",
    parameterName: "Pb",
    limitType: "NotMoreThan",
    unit: "ppm",
    upperLimit: 0.5,
    icpTestElementId: 21
  },
  titration: {
    itemId: 7,
    ...emptyBase,
    testCode: "TITR",
    parameterName: "Titration Assay",
    limitType: "Range",
    unit: "%",
    lowerLimit: 98,
    upperLimit: 102,
    lowerInclusive: true,
    upperInclusive: true,
    resultBasis: "PercentLabelClaim",
    labelClaim: 500,
    labelClaimUnit: "mg"
  },
  rangeEdit: {
    ...emptyBase,
    testCode: "PH",
    parameterName: "pH",
    displayOrder: 2,
    limitType: "Range",
    referenceStandard: "USP <791>",
    unit: "pH units",
    lowerLimit: 5,
    upperLimit: 7.5,
    lowerInclusive: true,
    upperInclusive: true,
    productionStageRole: "Bulk"
  }
};
