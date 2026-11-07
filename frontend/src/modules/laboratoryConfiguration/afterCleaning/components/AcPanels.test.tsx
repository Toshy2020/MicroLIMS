import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AcCountTestPanel } from "./AcPanels";
import { AfterCleaningConfigService } from "../services/AfterCleaningConfigService";
import type { MachinePart, PartConfig } from "../acConfigTypes";
import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";

const mockTestDefs: TestDefinitionOption[] = [
  {
    id: 1,
    code: "TAMC-Swab",
    displayName: "TAMC Swab",
    isActive: true,
    workflowType: "CountTest",
    sectionId: 1,
    section: { id: 1, name: "Microbiology", code: "MICRO" }
  },
  {
    id: 2,
    code: "UV-Clean",
    displayName: "UV Clean Scan",
    isActive: true,
    workflowType: "Measurement",
    sectionId: 2,
    section: { id: 2, name: "Physicochemical", code: "FP" }
  },
  {
    id: 3,
    code: "HPLC-Clean",
    displayName: "HPLC Clean Assay",
    isActive: true,
    workflowType: "HplcMethodAssay",
    sectionId: 2,
    section: { id: 2, name: "Physicochemical", code: "FP" }
  },
  {
    id: 4,
    code: "ICP-Clean",
    displayName: "ICP Clean Impurity",
    isActive: true,
    workflowType: "IcpMethodAssay",
    sectionId: 2,
    section: { id: 2, name: "Physicochemical", code: "FP" }
  }
];

vi.mock("../../../../hooks/useTestDefinitions", () => ({
  useTestDefinitions: () => ({
    options: mockTestDefs,
    activeOptions: mockTestDefs.filter((o) => o.isActive),
    addNew: vi.fn(),
    update: vi.fn(),
    setActive: vi.fn(),
    reload: vi.fn()
  })
}));

vi.mock("../../../../hooks/useEditableTestCodes", () => ({
  useEditableTestCodes: () => ({
    codes: ["TAMC-Swab", "UV-Clean", "HPLC-Clean", "ICP-Clean"],
    editableCodes: new Set(["TAMC-Swab", "UV-Clean", "HPLC-Clean", "ICP-Clean"]),
    loading: false,
    reload: vi.fn()
  })
}));

vi.mock("../services/AfterCleaningConfigService", () => ({
  AfterCleaningConfigService: {
    createPartConfiguration: vi.fn().mockResolvedValue({ id: 99 }),
    updatePartConfiguration: vi.fn().mockResolvedValue({ id: 99 })
  }
}));

afterEach(cleanup);

describe("AcCountTestPanel chemistry limits", () => {
  const part: MachinePart = {
    id: 10,
    name: "Hopper",
    machineId: 1
  };

  it("renders ChemistryLimitFields with default Qualitative 'No peaks' for UV test and saves limits", async () => {
    const onSaved = vi.fn();

    render(
      <PinnedLightTheme>
        <AcCountTestPanel
          open={true}
          part={part}
          config={null}
          onClose={vi.fn()}
          onSaved={onSaved}
        />
      </PinnedLightTheme>
    );

    // Pick UV test
    const testPickerInput = screen.getByLabelText("Test");
    fireEvent.change(testPickerInput, { target: { value: "UV-Clean" } });
    fireEvent.click(await screen.findByText(/UV Clean Scan/i));

    // Expected result text should default to "No peaks"
    const expectedInput = await screen.findByLabelText("Expected result text") as HTMLInputElement;
    expect(expectedInput.value).toBe("No peaks");

    // Save
    const saveBtn = screen.getByRole("button", { name: "Add test" });
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(AfterCleaningConfigService.createPartConfiguration).toHaveBeenCalledWith(
        10,
        "Swab",
        "UV-Clean",
        "",
        "",
        "No peaks",
        false,
        "",
        expect.objectContaining({
          limitType: "Qualitative",
          expectedResultText: "No peaks"
        })
      );
      expect(onSaved).toHaveBeenCalled();
    });
  });

  it("renders standard count test limits for micro test", async () => {
    render(
      <PinnedLightTheme>
        <AcCountTestPanel
          open={true}
          part={part}
          config={null}
          onClose={vi.fn()}
          onSaved={vi.fn()}
        />
      </PinnedLightTheme>
    );

    // Pick TAMC Swab
    const testPickerInput = screen.getByLabelText("Test");
    fireEvent.change(testPickerInput, { target: { value: "TAMC-Swab" } });
    fireEvent.click(await screen.findByText(/TAMC Swab/i));

    // Standard Alert / Action / Specification inputs are rendered
    expect(await screen.findByLabelText("Alert")).toBeDefined();
    expect(screen.getByLabelText("Action")).toBeDefined();
    expect(screen.getByLabelText("Specification")).toBeDefined();
  });

  it("shows 'Limit from the method' and sends no limit fields for HPLC method assay", async () => {
    const onSaved = vi.fn();

    render(
      <PinnedLightTheme>
        <AcCountTestPanel
          open={true}
          part={part}
          config={null}
          onClose={vi.fn()}
          onSaved={onSaved}
        />
      </PinnedLightTheme>
    );

    // Pick HPLC test
    const testPickerInput = screen.getByLabelText("Test");
    fireEvent.change(testPickerInput, { target: { value: "HPLC-Clean" } });
    fireEvent.click(await screen.findByText(/HPLC Clean Assay/i));

    // Shows caption
    expect(await screen.findByText("Limit from the method")).toBeTruthy();

    // ChemistryLimitFields is hidden
    expect(screen.queryByLabelText("Limit type")).toBeNull();
    expect(screen.queryByLabelText("Expected result text")).toBeNull();

    // Save
    const saveBtn = screen.getByRole("button", { name: "Add test" });
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(AfterCleaningConfigService.createPartConfiguration).toHaveBeenCalledWith(
        10,
        "Swab",
        "HPLC-Clean",
        "",
        "",
        "",
        false,
        "",
        undefined
      );
      expect(onSaved).toHaveBeenCalled();
    });
  });

  it("shows 'Limit from the ICP test' and sends no limit fields for ICP method assay", async () => {
    const onSaved = vi.fn();

    render(
      <PinnedLightTheme>
        <AcCountTestPanel
          open={true}
          part={part}
          config={null}
          onClose={vi.fn()}
          onSaved={onSaved}
        />
      </PinnedLightTheme>
    );

    // Pick ICP test
    const testPickerInput = screen.getByLabelText("Test");
    fireEvent.change(testPickerInput, { target: { value: "ICP-Clean" } });
    fireEvent.click(await screen.findByText(/ICP Clean Impurity/i));

    // Shows caption
    expect(await screen.findByText("Limit from the ICP test")).toBeTruthy();

    // ChemistryLimitFields is hidden
    expect(screen.queryByLabelText("Limit type")).toBeNull();
    expect(screen.queryByLabelText("Expected result text")).toBeNull();

    // Save
    const saveBtn = screen.getByRole("button", { name: "Add test" });
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(AfterCleaningConfigService.createPartConfiguration).toHaveBeenCalledWith(
        10,
        "Swab",
        "ICP-Clean",
        "",
        "",
        "",
        false,
        "",
        undefined
      );
      expect(onSaved).toHaveBeenCalled();
    });
  });
});
