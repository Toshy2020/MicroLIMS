import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { WaterLocationPanel } from "./WaterLocationPanel";
import { WaterConfigService } from "../services/WaterConfigService";
import type { SamplingPoint, SamplingConfig, WaterDept } from "../waterConfigTypes";
import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";

const mockOptions: TestDefinitionOption[] = [
  {
    id: 1,
    code: "TAMC-Water",
    displayName: "TAMC for Water",
    isActive: true,
    workflowType: "CountTest",
    section: { id: 1, name: "Microbiology", code: "MICRO" }
  },
  {
    id: 2,
    code: "PH",
    displayName: "pH",
    isActive: true,
    workflowType: "Measurement",
    section: { id: 2, name: "Physicochemical", code: "FP" }
  },
  {
    id: 3,
    code: "APPEARANCE",
    displayName: "Appearance",
    isActive: true,
    workflowType: "Qualitative",
    section: { id: 2, name: "Physicochemical", code: "FP" }
  }
];

vi.mock("../../../../hooks/useTestDefinitions", () => ({
  useTestDefinitions: () => ({
    options: mockOptions,
    activeOptions: mockOptions.filter((o) => o.isActive),
    addNew: vi.fn(),
    update: vi.fn(),
    setActive: vi.fn(),
    reload: vi.fn()
  })
}));

vi.mock("../services/WaterConfigService", () => ({
  WaterConfigService: {
    createSamplingPoint: vi.fn(),
    updateSamplingPoint: vi.fn(),
    createSamplingConfiguration: vi.fn(),
    updateSamplingConfiguration: vi.fn()
  }
}));

afterEach(cleanup);

describe("WaterLocationPanel", () => {
  const departments: WaterDept[] = [
    { id: 1, name: "Purified Water System", samplingPoints: [] }
  ];

  const point: SamplingPoint = {
    id: 10,
    code: "PW-01",
    location: "Loop A",
    waterDepartmentId: 1,
    testingFrequency: "Weekly",
    assignedTestCodes: ["TAMC-Water", "PH"],
    version: 1
  };

  const configs: SamplingConfig[] = [
    {
      id: 101,
      waterSamplingPointId: 10,
      testCode: "TAMC-Water",
      alertLimit: "10",
      actionLimit: "100",
      specLimit: "500",
      unit: "CFU/mL"
    },
    {
      id: 102,
      waterSamplingPointId: 10,
      testCode: "PH",
      alertLimit: "",
      actionLimit: "",
      specLimit: "6.5-8.5",
      unit: "pH",
      limitType: "Range",
      lowerLimit: 6.5,
      upperLimit: 8.5
    }
  ];

  it("renders count test editor for TAMC-Water and chemistry editor for PH", () => {
    render(
      <PinnedLightTheme>
        <WaterLocationPanel
          open={true}
          point={point}
          defaultDepartmentId={1}
          departments={departments}
          configs={configs}
          isCountTest={(code) => code === "TAMC-Water"}
          onClose={vi.fn()}
          onSaved={vi.fn()}
          editableCodes={new Set(["TAMC-Water", "PH"])}
        />
      </PinnedLightTheme>
    );

    // Count test editor has Alert, Action, Specification
    expect(screen.getByLabelText("Alert")).toBeDefined();
    expect(screen.getByLabelText("Action")).toBeDefined();
    expect(screen.getByLabelText("Specification")).toBeDefined();

    // Chemistry editor has Limit type, Lower, Upper
    expect(screen.getByLabelText("Limit type")).toBeDefined();
    expect(screen.getByLabelText("Lower")).toBeDefined();
    expect(screen.getByLabelText("Upper")).toBeDefined();
  });

  it("saves chemistry limits with create/update configuration", async () => {
    const onSaved = vi.fn();
    (WaterConfigService.updateSamplingPoint as any).mockResolvedValueOnce({ id: 10, version: 2 });
    (WaterConfigService.updateSamplingConfiguration as any).mockResolvedValue({ id: 102 });

    render(
      <PinnedLightTheme>
        <WaterLocationPanel
          open={true}
          point={point}
          defaultDepartmentId={1}
          departments={departments}
          configs={configs}
          isCountTest={(code) => code === "TAMC-Water"}
          onClose={vi.fn()}
          onSaved={onSaved}
          editableCodes={new Set(["TAMC-Water", "PH"])}
        />
      </PinnedLightTheme>
    );

    // Edit PH upper limit from 8.5 to 8.0
    const upperInput = screen.getByLabelText("Upper");
    fireEvent.change(upperInput, { target: { value: "8" } });

    const saveBtn = screen.getByRole("button", { name: "Save changes" });
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(WaterConfigService.updateSamplingConfiguration).toHaveBeenCalledWith(
        102,
        "PH",
        null,
        null,
        "6.5-8",
        "pH",
        undefined,
        "Range",
        6.5,
        8,
        true,
        true,
        ""
      );
      expect(onSaved).toHaveBeenCalled();
    });
  });

  it("renders read-only limit pill for other lab's test code", () => {
    render(
      <PinnedLightTheme>
        <WaterLocationPanel
          open={true}
          point={point}
          defaultDepartmentId={1}
          departments={departments}
          configs={configs}
          isCountTest={(code) => code === "TAMC-Water"}
          onClose={vi.fn()}
          onSaved={vi.fn()}
          editableCodes={new Set(["TAMC-Water"])} // PH is locked
        />
      </PinnedLightTheme>
    );

    // PH is locked -> shows Read-only and "6.5 – 8.5 pH"
    expect(screen.getByText("Physicochemical · Read-only")).toBeDefined();
    expect(screen.getByText("6.5 – 8.5 pH")).toBeDefined();
  });
});
