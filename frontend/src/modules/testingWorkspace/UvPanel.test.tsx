import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { UvPanel } from "./UvPanel";
import type { TestDefinitionOption } from "../../hooks/useTestDefinitions";
import type { SpecificationDto } from "../laboratoryConfiguration/specifications/services/SpecificationService";

const {
  mockRecordUvResult,
  mockGetMoisture,
  mockGetSections,
  mockGetConfiguredSummary,
  mockGetUsableReferenceStandards,
  mockGetTestDefinitions,
  mockGetForTestOrder,
  mockStableEquipment,
  mockStableSections,
  mockStableStandards,
  mockRiboflavinDef,
  mockAbsLimitDef,
  mockStdCompDef,
  mockRiboflavinSpec,
  mockAbsLimitSpec,
  mockStdCompSpec,
} = vi.hoisted(() => {
  const stableEquipment = [
    {
      id: 1,
      name: "UV-Vis Lambda 365",
      code: "UV-01",
      type: "UvVis",
      sectionId: 10,
      section: { code: "FP", sectionCode: "FP" }
    }
  ];
  const stableSections = [
    { id: 10, sectionId: 10, sectionCode: "FP", name: "Physicochemical" }
  ];
  const stableStandards = [
    {
      id: 101,
      materialName: "Riboflavin Reference Standard",
      batchNumber: "STD-RIB-01",
      code: "WS-RIB",
      lotLabel: "STD-RIB-01",
      purity: 99.8,
      moisturePercent: 0.1,
      materialType: "ReferenceStandard"
    }
  ];

  const riboflavinDef: TestDefinitionOption = {
    id: 1,
    code: "UV-RIB",
    displayName: "Riboflavin assay",
    isActive: true,
    workflowType: "UvSpectrophotometry",
    equationType: "UvSpectrophotometry",
    replicateCount: 1,
    uvCalculationMode: "SpecificAbsorbanceAssay",
    uvSpecificAbsorbance: 328,
    uvTotalVolumeMl: 5000,
    uvStandardVolumeMl: null
  };

  const absLimitDef: TestDefinitionOption = {
    id: 2,
    code: "UV-INORG-P",
    displayName: "Inorganic phosphate limit",
    isActive: true,
    workflowType: "UvSpectrophotometry",
    equationType: "UvSpectrophotometry",
    replicateCount: 1,
    uvCalculationMode: "AbsorbanceLimit",
    uvSpecificAbsorbance: null,
    uvTotalVolumeMl: null,
    uvStandardVolumeMl: null
  };

  const stdCompDef: TestDefinitionOption = {
    id: 3,
    code: "UV-STD-COMP",
    displayName: "Assay against standard",
    isActive: true,
    workflowType: "UvSpectrophotometry",
    equationType: "UvSpectrophotometry",
    replicateCount: 1,
    uvCalculationMode: "StandardComparison",
    uvSpecificAbsorbance: null,
    uvTotalVolumeMl: 100,
    uvStandardVolumeMl: 100
  };

  const riboflavinSpec: SpecificationDto = {
    id: 10,
    testCode: "UV-RIB",
    parameterName: "Riboflavin 5-phosphate (444 nm)",
    specLimit: "73.0 - 79.0 %",
    unit: "%",
    resultBasis: "PercentDriedBasis",
    limitType: "Range",
    displayOrder: 1
  };

  const absLimitSpec: SpecificationDto = {
    id: 20,
    testCode: "UV-INORG-P",
    parameterName: "Inorganic phosphate (700 nm)",
    specLimit: "Conforms",
    unit: "AU",
    resultBasis: null,
    limitType: "Qualitative",
    displayOrder: 1
  };

  const stdCompSpec: SpecificationDto = {
    id: 30,
    testCode: "UV-STD-COMP",
    parameterName: "Active substance assay (275 nm)",
    specLimit: "98.0 - 102.0 %",
    unit: "%",
    resultBasis: "PercentAsIs",
    limitType: "Range",
    displayOrder: 1
  };

  return {
    mockRecordUvResult: vi.fn(),
    mockGetMoisture: vi.fn(),
    mockGetSections: vi.fn(),
    mockGetConfiguredSummary: vi.fn(),
    mockGetUsableReferenceStandards: vi.fn(),
    mockGetTestDefinitions: vi.fn(),
    mockGetForTestOrder: vi.fn(),
    mockStableEquipment: stableEquipment,
    mockStableSections: stableSections,
    mockStableStandards: stableStandards,
    mockRiboflavinDef: riboflavinDef,
    mockAbsLimitDef: absLimitDef,
    mockStdCompDef: stdCompDef,
    mockRiboflavinSpec: riboflavinSpec,
    mockAbsLimitSpec: absLimitSpec,
    mockStdCompSpec: stdCompSpec,
  };
});

vi.mock("./services/TestWorkflowService", () => ({
  TestWorkflowService: {
    recordUvResult: (...a: unknown[]) => mockRecordUvResult(...a),
    getMoisture: (...a: unknown[]) => mockGetMoisture(...a),
  }
}));

vi.mock("./services/SampleSummaryService", () => ({
  SampleSummaryService: {
    getSummary: vi.fn().mockResolvedValue({ testOrders: [] })
  }
}));

vi.mock("../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getConfiguredSummary: (...a: unknown[]) => mockGetConfiguredSummary(...a)
  }
}));

vi.mock("../../services/laboratorySectionService", () => ({
  getSections: (...a: unknown[]) => mockGetSections(...a)
}));

vi.mock("../inventory/materials/services/MaterialService", () => ({
  MaterialService: {
    getUsableReferenceStandards: (...a: unknown[]) => mockGetUsableReferenceStandards(...a)
  }
}));

vi.mock("../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getTestDefinitions: (...a: unknown[]) => mockGetTestDefinitions(...a)
  }
}));

vi.mock("../laboratoryConfiguration/specifications/services/SpecificationService", () => ({
  SpecificationService: {
    getForTestOrder: (...a: unknown[]) => mockGetForTestOrder(...a)
  }
}));

vi.mock("../../components/SignatureDialog", () => ({
  SignatureDialog: ({
    open,
    meaningStatement,
    onConfirm
  }: {
    open: boolean;
    meaningStatement?: string;
    onConfirm: (pw: string) => Promise<void>;
  }) =>
    open ? (
      <div data-testid="signature-dialog">
        <span>{meaningStatement}</span>
        <button onClick={() => void onConfirm("password123")}>confirm-signature</button>
      </div>
    ) : null
}));

const renderUvPanel = (props: {
  testOrderId?: number;
  testCode?: string;
  displayName?: string;
  onRecorded?: () => Promise<void> | void;
}) => {
  const onRecorded = props.onRecorded ?? vi.fn();
  render(
    <PinnedLightTheme>
      <UvPanel
        testOrderId={props.testOrderId ?? 100}
        displayName={props.displayName ?? "UV Test"}
        testCode={props.testCode ?? "UV-RIB"}
        current={{ workflowType: "UvSpectrophotometry", testName: props.testCode ?? "UV-RIB" }}
        onRecorded={onRecorded}
      />
    </PinnedLightTheme>
  );
  return { onRecorded };
};

describe("UvPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetSections.mockResolvedValue(mockStableSections);
    mockGetConfiguredSummary.mockResolvedValue(mockStableEquipment);
    mockGetUsableReferenceStandards.mockResolvedValue(mockStableStandards);
    mockGetTestDefinitions.mockResolvedValue([
      mockRiboflavinDef,
      mockAbsLimitDef,
      mockStdCompDef
    ]);
  });

  afterEach(() => {
    cleanup();
  });

  it("SpecificAbsorbanceAssay shows weight, absorbance and wavelength and no standard fields", async () => {
    mockGetForTestOrder.mockResolvedValue([mockRiboflavinSpec]);
    mockGetMoisture.mockResolvedValue({
      percent: 5,
      role: "LossOnDrying",
      testCode: "LOD-01"
    });

    renderUvPanel({ testCode: "UV-RIB" });

    await waitFor(() => {
      expect(screen.getByLabelText(/Sample weight/i)).toBeTruthy();
      expect(screen.getByLabelText(/Absorbance 1/i)).toBeTruthy();
      expect(screen.getByLabelText(/Wavelength/i)).toBeTruthy();
    });

    // Should NOT show standard fields
    expect(screen.queryByLabelText(/Standard lot/i)).toBeNull();
    expect(screen.queryByLabelText(/Standard weight/i)).toBeNull();
    expect(screen.queryByLabelText(/Standard absorbance/i)).toBeNull();
    expect(screen.queryByLabelText(/Reference absorbance/i)).toBeNull();
  });

  it("AbsorbanceLimit shows reference and no weight", async () => {
    mockGetForTestOrder.mockResolvedValue([mockAbsLimitSpec]);

    renderUvPanel({ testCode: "UV-INORG-P" });

    await waitFor(() => {
      expect(screen.getByLabelText(/Reference absorbance/i)).toBeTruthy();
      expect(screen.getByLabelText(/Absorbance 1/i)).toBeTruthy();
      expect(screen.getByLabelText(/Wavelength/i)).toBeTruthy();
    });

    // Should NOT show weight fields or standard fields
    expect(screen.queryByLabelText(/Sample weight/i)).toBeNull();
    expect(screen.queryByLabelText(/Standard lot/i)).toBeNull();
  });

  it("StandardComparison shows the lot, standard weight and standard absorbance", async () => {
    mockGetForTestOrder.mockResolvedValue([mockStdCompSpec]);

    renderUvPanel({ testCode: "UV-STD-COMP" });

    await waitFor(() => {
      expect(screen.getByLabelText(/Standard lot/i)).toBeTruthy();
      expect(screen.getByLabelText(/Standard weight/i)).toBeTruthy();
      expect(screen.getByLabelText(/Standard absorbance/i)).toBeTruthy();
      expect(screen.getByLabelText(/Sample weight/i)).toBeTruthy();
      expect(screen.getByLabelText(/Absorbance 1/i)).toBeTruthy();
    });
  });

  it("dried basis with no LOD shows the missing-moisture message and disables signing", async () => {
    mockGetForTestOrder.mockResolvedValue([mockRiboflavinSpec]);
    mockGetMoisture.mockResolvedValue(null);

    renderUvPanel({ testCode: "UV-RIB" });

    await waitFor(() => {
      expect(
        screen.getByText(/Record the sample's loss on drying result first/i)
      ).toBeTruthy();
    });

    const signButton = screen.getByRole("button", { name: /Sign and record result/i });
    expect(signButton).toHaveProperty("disabled", true);
  });

  it("with LOD shows 'Loss on drying 0.5 % (from LOD-UI)'", async () => {
    mockGetForTestOrder.mockResolvedValue([mockRiboflavinSpec]);
    mockGetMoisture.mockResolvedValue({
      percent: 0.5,
      role: "LossOnDrying",
      testCode: "LOD-UI"
    });

    renderUvPanel({ testCode: "UV-RIB" });

    await waitFor(() => {
      expect(screen.getByText("Loss on drying 0.5 % (from LOD-UI)")).toBeTruthy();
    });
  });

  it("the payload sent matches the request shape and the preview for the Riboflavin numbers reads 78.63", async () => {
    const user = userEvent.setup();
    mockGetForTestOrder.mockResolvedValue([mockRiboflavinSpec]);
    mockGetMoisture.mockResolvedValue({
      percent: 5,
      role: "LossOnDrying",
      testCode: "LOD-UI"
    });
    mockRecordUvResult.mockResolvedValue({
      outcomeSummary: "Riboflavin assay: 78.63 %",
      status: "WithinLimits"
    });

    renderUvPanel({ testCode: "UV-RIB", testOrderId: 123 });

    await waitFor(() => {
      expect(screen.getByLabelText(/Sample weight/i)).toBeTruthy();
      expect(screen.getByRole("combobox", { name: /Equipment/i })).toBeTruthy();
    });

    // Enter wavelength 444
    const wavelengthInput = screen.getByLabelText(/Wavelength/i);
    await user.clear(wavelengthInput);
    await user.type(wavelengthInput, "444");

    // Enter weight 0.1 g
    const weightInput = screen.getByLabelText(/Sample weight/i);
    await user.clear(weightInput);
    await user.type(weightInput, "0.1");

    // Enter absorbance 0.49
    const absInput = screen.getByLabelText(/Absorbance 1/i);
    await user.clear(absInput);
    await user.type(absInput, "0.49");

    // Verify preview reads 78.63
    await waitFor(() => {
      expect(screen.getByText(/78\.63/)).toBeTruthy();
    });

    // Click sign button
    const signBtn = screen.getByRole("button", { name: /Sign and record result/i });
    expect(signBtn).toHaveProperty("disabled", false);
    await user.click(signBtn);

    // Confirm signature
    const confirmBtn = await screen.findByText("confirm-signature");
    await user.click(confirmBtn);

    // Verify payload sent
    await waitFor(() => {
      expect(mockRecordUvResult).toHaveBeenCalledTimes(1);
    });

    const [testOrderId, payload] = mockRecordUvResult.mock.calls[0];
    expect(testOrderId).toBe(123);
    expect(payload).toMatchObject({
      equipmentId: 1,
      standardMaterialId: null,
      standardWeightG: null,
      standardAbsorbance: null,
      password: "password123",
      parameters: [
        {
          specificationId: 10,
          wavelengthNm: 444,
          referenceAbsorbance: null,
          replicates: [
            {
              weightG: 0.1,
              absorbance: 0.49
            }
          ]
        }
      ]
    });
  });
});
