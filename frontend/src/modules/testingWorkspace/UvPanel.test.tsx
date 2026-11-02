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

    expect(screen.getByText("Specific Absorbance Assay")).toBeTruthy();
    expect(screen.getByText(/^A1%: /)).toBeTruthy();

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

  it("a parameter named 'Vitamin D3 400 IU' leaves the wavelength empty", async () => {
    const specWith400IU: SpecificationDto = {
      id: 20,
      testCode: "UV-VITD",
      parameterName: "Vitamin D3 400 IU",
      limitType: "Range",
      lowerLimit: 90,
      upperLimit: 110,
      unit: "%",
      resultBasis: "PercentAsIs",
      displayOrder: 1
    };
    mockGetForTestOrder.mockResolvedValue([specWith400IU]);
    mockGetTestDefinitions.mockResolvedValue([
      {
        id: 4,
        code: "UV-VITD",
        displayName: "Vitamin D3",
        workflowType: "UvSpectrophotometry",
        uvCalculationMode: "SpecificAbsorbanceAssay",
        replicateCount: 1,
        uvSpecificAbsorbance: 400,
        uvTotalVolumeMl: 100
      }
    ]);

    renderUvPanel({ testCode: "UV-VITD" });

    // Loading has finished once the form is on screen; only then can the wavelength be judged.
    await screen.findByLabelText(/Sample weight/i);
    expect((screen.getByLabelText(/Wavelength/i) as HTMLInputElement).value).toBe("");
  });

  it("value mode applies the dried-basis correction to the preview, with no percent sign", async () => {
    const user = userEvent.setup();
    mockGetForTestOrder.mockResolvedValue([
      { id: 40, testCode: "UV-VAL", parameterName: "E1% 1cm", limitType: "Range", lowerLimit: 4, upperLimit: 6, unit: "", resultBasis: "PercentDriedBasis", displayOrder: 1 }
    ]);
    mockGetTestDefinitions.mockResolvedValue([
      { id: 6, code: "UV-VAL", displayName: "Specific absorbance", workflowType: "UvSpectrophotometry", uvCalculationMode: "SpecificAbsorbanceValue", replicateCount: 1, uvTotalVolumeMl: 1000 }
    ]);
    mockGetMoisture.mockResolvedValue({ percent: 5, role: "LossOnDrying", testCode: "LOD-UI" });

    renderUvPanel({ testCode: "UV-VAL" });

    await user.type(await screen.findByLabelText(/Sample weight/i), "1");
    await user.type(screen.getByLabelText(/Absorbance 1/i), "0.5");

    // 0.5 x 1000 / (1 x 100) = 5.00 as is; 5.00 / 0.95 = 5.26 dried
    expect(await screen.findByText("5.26")).toBeTruthy();
  });

  it("a rejected equipment load shows the error message", async () => {
    mockGetForTestOrder.mockResolvedValue([mockRiboflavinSpec]);
    mockGetConfiguredSummary.mockRejectedValue(new Error("Network failed loading equipment"));

    renderUvPanel({ testCode: "UV-RIB" });

    await waitFor(() => {
      expect(screen.getByText(/Could not load laboratory equipment/i)).toBeTruthy();
    });
  });

  it("an HPLC instrument in the load is not offered", async () => {
    mockGetForTestOrder.mockResolvedValue([mockRiboflavinSpec]);
    mockGetConfiguredSummary.mockResolvedValue([
      { id: 1, name: "UV Spectrophotometer 01", type: "UvVis", section: { code: "FP" } },
      { id: 2, name: "HPLC Agilent 1260", type: "Hplc", section: { code: "FP" } }
    ]);

    renderUvPanel({ testCode: "UV-RIB" });

    await waitFor(() => {
      expect(screen.getByRole("combobox", { name: /Equipment/i })).toBeTruthy();
    });

    const equipSelect = screen.getByRole("combobox", { name: /Equipment/i });
    fireEvent.mouseDown(equipSelect);

    expect(await screen.findByRole("option", { name: /UV Spectrophotometer 01/i })).toBeTruthy();
    expect(screen.queryByRole("option", { name: /HPLC Agilent 1260/i })).toBeNull();
  });

  it("weight prefill with two specs: mirrors param 1 to param 2 until param 2 is edited", async () => {
    const user = userEvent.setup();
    const spec1: SpecificationDto = {
      id: 101,
      testCode: "UV-DUAL",
      parameterName: "Parameter A",
      limitType: "Range",
      displayOrder: 1
    };
    const spec2: SpecificationDto = {
      id: 102,
      testCode: "UV-DUAL",
      parameterName: "Parameter B",
      limitType: "Range",
      displayOrder: 2
    };
    mockGetForTestOrder.mockResolvedValue([spec1, spec2]);
    mockGetTestDefinitions.mockResolvedValue([
      {
        id: 5,
        code: "UV-DUAL",
        displayName: "UV Dual Spec Test",
        workflowType: "UvSpectrophotometry",
        uvCalculationMode: "SpecificAbsorbanceAssay",
        replicateCount: 1,
        uvSpecificAbsorbance: 100,
        uvTotalVolumeMl: 100
      }
    ]);

    renderUvPanel({ testCode: "UV-DUAL" });

    await waitFor(() => {
      expect(screen.getAllByLabelText(/Sample weight/i).length).toBe(2);
    });

    const [weightInput1, weightInput2] = screen.getAllByLabelText(/Sample weight/i) as HTMLInputElement[];

    // Type in parameter 1 replicate 1
    await user.type(weightInput1, "0.123");
    // Parameter 2 replicate 1 should mirror it
    expect(weightInput2.value).toBe("0.123");

    // Edit parameter 2 replicate 1
    await user.clear(weightInput2);
    await user.type(weightInput2, "0.456");
    expect(weightInput2.value).toBe("0.456");

    // Change parameter 1 replicate 1
    await user.clear(weightInput1);
    await user.type(weightInput1, "0.789");

    // Parameter 2 keeps its own value
    expect(weightInput2.value).toBe("0.456");
  });

  it("submits payload for AbsorbanceLimit with weightG null and referenceAbsorbance set", async () => {
    const user = userEvent.setup();
    mockGetForTestOrder.mockResolvedValue([mockAbsLimitSpec]);
    mockRecordUvResult.mockResolvedValue({
      outcomeSummary: "Inorganic phosphate: Conforms",
      status: "WithinLimits"
    });

    renderUvPanel({ testCode: "UV-INORG-P", testOrderId: 777 });

    await waitFor(() => {
      expect(screen.getByLabelText(/Reference absorbance/i)).toBeTruthy();
    });

    const refInput = screen.getByLabelText(/Reference absorbance/i);
    await user.type(refInput, "0.15");

    const wavelengthInput = screen.getByLabelText(/Wavelength/i);
    await user.clear(wavelengthInput);
    await user.type(wavelengthInput, "700");

    const absInput = screen.getByLabelText(/Absorbance 1/i);
    await user.type(absInput, "0.12");

    const signBtn = screen.getByRole("button", { name: /Sign and record result/i });
    expect(signBtn).toHaveProperty("disabled", false);
    await user.click(signBtn);

    const confirmBtn = await screen.findByText("confirm-signature");
    await user.click(confirmBtn);

    await waitFor(() => {
      expect(mockRecordUvResult).toHaveBeenCalledTimes(1);
    });

    const [orderId, payload] = mockRecordUvResult.mock.calls[0];
    expect(orderId).toBe(777);
    expect(payload.parameters[0].referenceAbsorbance).toBe(0.15);
    expect(payload.parameters[0].replicates[0].weightG).toBeNull();
    expect(payload.parameters[0].replicates[0].absorbance).toBe(0.12);
  });

  it("a blank limit absorbance leaves signing disabled, and 0 enables it", async () => {
    const user = userEvent.setup();
    mockGetForTestOrder.mockResolvedValue([mockAbsLimitSpec]);
    renderUvPanel({ testCode: "UV-INORG-P", testOrderId: 778 });
    await waitFor(() => {
      expect(screen.getByLabelText(/Reference absorbance/i)).toBeTruthy();
    });
    await user.type(screen.getByLabelText(/Reference absorbance/i), "0.15");
    const wavelengthInput = screen.getByLabelText(/Wavelength/i);
    await user.clear(wavelengthInput);
    await user.type(wavelengthInput, "700");

    const signBtn = screen.getByRole("button", { name: /Sign and record result/i });
    expect(signBtn).toHaveProperty("disabled", true);
    await user.type(screen.getByLabelText(/Absorbance 1/i), "0");
    expect(signBtn).toHaveProperty("disabled", false);
  });

  it("submits payload for StandardComparison with standard fields set", async () => {
    const user = userEvent.setup();
    mockGetForTestOrder.mockResolvedValue([mockStdCompSpec]);
    mockRecordUvResult.mockResolvedValue({
      outcomeSummary: "Standard comparison: 99.8 %",
      status: "WithinLimits"
    });

    renderUvPanel({ testCode: "UV-STD-COMP", testOrderId: 888 });

    await waitFor(() => {
      expect(screen.getByLabelText(/Standard weight/i)).toBeTruthy();
    });

    const stdWeightInput = screen.getByLabelText(/Standard weight/i);
    await user.type(stdWeightInput, "0.05");

    const stdAbsInput = screen.getByLabelText(/Standard absorbance/i);
    await user.type(stdAbsInput, "0.45");

    const wavelengthInput = screen.getByLabelText(/Wavelength/i);
    await user.clear(wavelengthInput);
    await user.type(wavelengthInput, "275");

    const sampleWeightInput = screen.getByLabelText(/Sample weight/i);
    await user.type(sampleWeightInput, "0.05");

    const absInput = screen.getByLabelText(/Absorbance 1/i);
    await user.type(absInput, "0.45");

    const signBtn = screen.getByRole("button", { name: /Sign and record result/i });
    await user.click(signBtn);

    const confirmBtn = await screen.findByText("confirm-signature");
    await user.click(confirmBtn);

    await waitFor(() => {
      expect(mockRecordUvResult).toHaveBeenCalledTimes(1);
    });

    const [orderId, payload] = mockRecordUvResult.mock.calls[0];
    expect(orderId).toBe(888);
    expect(payload.standardMaterialId).toBe(101);
    expect(payload.standardWeightG).toBe(0.05);
    expect(payload.standardAbsorbance).toBe(0.45);
    expect(payload.parameters[0].replicates[0].weightG).toBe(0.05);
    expect(payload.parameters[0].replicates[0].absorbance).toBe(0.45);
  });
});
