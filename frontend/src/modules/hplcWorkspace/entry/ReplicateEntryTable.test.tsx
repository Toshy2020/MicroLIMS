import { useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ReplicateEntryTable, replicatesComplete } from "./ReplicateEntryTable";
import { buildReplicatePayload } from "./replicatePayload";
import type { HplcMethodWeightDto, HplcReplicateDto } from "../types";

afterEach(cleanup);

const mockWeights: HplcMethodWeightDto[] = [
  {
    hplcMethodAnalyteId: 10,
    analyteName: "Methanol",
    theoreticalWeightStdMg: 0,
    theoreticalWeightTestMg: 0
  }
];

const mockReplicate: HplcReplicateDto = {
  replicateNo: 1,
  actualWeightMg: 100.5,
  responses: [
    {
      hplcMethodAnalyteId: 10,
      response: 0
    }
  ]
};

describe("ReplicateEntryTable", () => {
  it("shows ResidualSolvents column headers: 'Sample weight (mg) *' and '{analyte} Area *'", () => {
    render(
      <PinnedLightTheme>
        <ReplicateEntryTable
          methodWeights={mockWeights}
          replicates={[mockReplicate]}
          onChange={vi.fn()}
          isResidualSolvents={true}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Sample weight (mg) *")).toBeTruthy();
    expect(screen.getByText("Methanol Area *")).toBeTruthy();
    expect(screen.queryByText("Actual Weight *")).toBeNull();
  });

  it("shows Assay mode headers when isResidualSolvents is false", () => {
    render(
      <PinnedLightTheme>
        <ReplicateEntryTable
          methodWeights={mockWeights}
          replicates={[mockReplicate]}
          onChange={vi.fn()}
          isResidualSolvents={false}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Actual Weight *")).toBeTruthy();
    expect(screen.getByText("Methanol Response *")).toBeTruthy();
    expect(screen.queryByText("Sample weight (mg) *")).toBeNull();
  });

  it("displays '0 = not detected' helper when response is 0 in residual solvents mode", () => {
    render(
      <PinnedLightTheme>
        <ReplicateEntryTable
          methodWeights={mockWeights}
          replicates={[mockReplicate]}
          onChange={vi.fn()}
          isResidualSolvents={true}
        />
      </PinnedLightTheme>
    );

    const matches = screen.getAllByText(/0 = not detected/);
    expect(matches.length).toBeGreaterThan(0);
  });

  it("shows IS column and labels 'Analyte area *' and 'IS area *' when hasInternalStandard is true", () => {
    render(
      <PinnedLightTheme>
        <ReplicateEntryTable
          methodWeights={mockWeights}
          replicates={[
            {
              replicateNo: 1,
              actualWeightMg: 100.5,
              responses: [{ hplcMethodAnalyteId: 10, response: 2000 }],
              internalStandardResponse: 0
            }
          ]}
          onChange={vi.fn()}
          hasInternalStandard={true}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Analyte area *")).toBeTruthy();
    expect(screen.getByText("IS area *")).toBeTruthy();
    expect(screen.getByLabelText(/replicate 1 is area/i)).toBeTruthy();
  });

  it("the IS column is hidden without internalStandardName", () => {
    render(
      <PinnedLightTheme>
        <ReplicateEntryTable
          methodWeights={mockWeights}
          replicates={[mockReplicate]}
          onChange={vi.fn()}
          hasInternalStandard={false}
          internalStandardName={null}
        />
      </PinnedLightTheme>
    );

    expect(screen.queryByText("IS area *")).toBeNull();
    expect(screen.queryByLabelText(/is area/i)).toBeNull();
  });

  it("edits the IS area when hasInternalStandard is true", async () => {
    const user = userEvent.setup();
    const handleChange = vi.fn();
    render(
      <PinnedLightTheme>
        <ReplicateEntryTable
          methodWeights={mockWeights}
          replicates={[
            {
              replicateNo: 1,
              actualWeightMg: 100.5,
              responses: [{ hplcMethodAnalyteId: 10, response: 2000 }],
              internalStandardResponse: 0
            }
          ]}
          onChange={handleChange}
          hasInternalStandard={true}
          internalStandardName="n-Hexane (IS)"
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText("n-Hexane (IS) Area *")).toBeTruthy();
    const isInput = screen.getByLabelText(/replicate 1 is area/i);
    await user.click(isInput);
    await user.paste("3500");

    expect(handleChange).toHaveBeenCalled();
    const callArg = handleChange.mock.calls[0][0];
    expect(callArg[0].internalStandardResponse).toBe(3500);
  });

  it("characterises existing Enter key behaviour: moves down to same column, selects target text, stays on last row, and does not submit form", async () => {
    const user = userEvent.setup();
    const onFormSubmit = vi.fn();
    const initialReplicates: HplcReplicateDto[] = [
      {
        replicateNo: 1,
        actualWeightMg: 100.5,
        responses: [{ hplcMethodAnalyteId: 10, response: 2000 }],
        internalStandardResponse: 1500
      },
      {
        replicateNo: 2,
        actualWeightMg: 101.2,
        responses: [{ hplcMethodAnalyteId: 10, response: 2100 }],
        internalStandardResponse: 1550
      }
    ];

    function Harness() {
      const [reps, setReps] = useState(initialReplicates);
      return (
        <PinnedLightTheme>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              onFormSubmit();
            }}
          >
            <ReplicateEntryTable
              methodWeights={mockWeights}
              replicates={reps}
              onChange={setReps}
              hasInternalStandard={true}
            />
          </form>
        </PinnedLightTheme>
      );
    }

    render(<Harness />);

    const w1 = screen.getByLabelText("Replicate 1 actual weight (mg)") as HTMLInputElement;
    const w2 = screen.getByLabelText("Replicate 2 actual weight (mg)") as HTMLInputElement;
    const a1 = screen.getByLabelText("Replicate 1 Analyte area") as HTMLInputElement;
    const a2 = screen.getByLabelText("Replicate 2 Analyte area") as HTMLInputElement;
    const is1 = screen.getByLabelText("Replicate 1 IS area") as HTMLInputElement;
    const is2 = screen.getByLabelText("Replicate 2 IS area") as HTMLInputElement;

    const w2SelectSpy = vi.spyOn(w2, "select");
    const a2SelectSpy = vi.spyOn(a2, "select");
    const is2SelectSpy = vi.spyOn(is2, "select");

    await user.click(w1);
    await user.keyboard("{Enter}");
    expect(document.activeElement).toBe(w2);
    expect(w2SelectSpy).toHaveBeenCalledTimes(1);
    expect(onFormSubmit).not.toHaveBeenCalled();

    // Pressing Enter on the last row stays on the last row and does not submit
    await user.keyboard("{Enter}");
    expect(document.activeElement).toBe(w2);
    expect(onFormSubmit).not.toHaveBeenCalled();

    await user.click(a1);
    await user.keyboard("{Enter}");
    expect(document.activeElement).toBe(a2);
    expect(a2SelectSpy).toHaveBeenCalledTimes(1);

    await user.click(is1);
    await user.keyboard("{Enter}");
    expect(document.activeElement).toBe(is2);
    expect(is2SelectSpy).toHaveBeenCalledTimes(1);
    expect(onFormSubmit).not.toHaveBeenCalled();
  });

  it("Enter moves focus down to the same column in the next row, arrows move across grid, Ctrl+D changes nothing, and Ctrl+Enter saves", async () => {
    const user = userEvent.setup();
    const onSave = vi.fn();
    const initialReplicates: HplcReplicateDto[] = [
      {
        replicateNo: 1,
        actualWeightMg: 100.5,
        responses: [{ hplcMethodAnalyteId: 10, response: 2000 }],
        internalStandardResponse: 1500
      },
      {
        replicateNo: 2,
        actualWeightMg: 0,
        responses: [{ hplcMethodAnalyteId: 10, response: 0 }],
        internalStandardResponse: 0
      }
    ];

    function Harness() {
      const [reps, setReps] = useState(initialReplicates);
      return (
        <PinnedLightTheme>
          <ReplicateEntryTable
            methodWeights={mockWeights}
            replicates={reps}
            onChange={setReps}
            hasInternalStandard={true}
            onSave={() => onSave(reps)}
          />
        </PinnedLightTheme>
      );
    }

    render(<Harness />);

    const w1 = screen.getByLabelText("Replicate 1 actual weight (mg)") as HTMLInputElement;
    const w2 = screen.getByLabelText("Replicate 2 actual weight (mg)") as HTMLInputElement;
    const a1 = screen.getByLabelText("Replicate 1 Analyte area") as HTMLInputElement;
    const a2 = screen.getByLabelText("Replicate 2 Analyte area") as HTMLInputElement;
    const is2 = screen.getByLabelText("Replicate 2 IS area") as HTMLInputElement;

    await user.click(w1);
    await user.keyboard("{Enter}");
    expect(document.activeElement).toBe(w2);

    await user.keyboard("{ArrowUp}");
    expect(document.activeElement).toBe(w1);

    await user.keyboard("{ArrowDown}");
    expect(document.activeElement).toBe(w2);

    await user.keyboard("{ArrowRight}");
    expect(document.activeElement).toBe(a2);

    await user.keyboard("{ArrowRight}");
    expect(document.activeElement).toBe(is2);

    await user.keyboard("{ArrowLeft}");
    expect(document.activeElement).toBe(a2);

    // Ctrl+D on row 0 and row > 0 changes nothing (regulated readings must be entered deliberately)
    await user.click(a1);
    await user.keyboard("{Control>}d{/Control}");
    expect(a1.value).toBe("2000");

    await user.click(a2);
    await user.keyboard("{Control>}d{/Control}");
    expect(a2.value).toBe("");

    await user.keyboard("{Control>}{Enter}{/Control}");
    expect(onSave).toHaveBeenCalledTimes(1);
  });

  it("the save payload is unchanged when entering values via keyboard navigation", async () => {
    const user = userEvent.setup();
    const onSavePayload = vi.fn();
    const initialReplicates: HplcReplicateDto[] = [
      {
        replicateNo: 1,
        actualWeightMg: 0,
        responses: [{ hplcMethodAnalyteId: 10, response: 0 }],
        internalStandardResponse: 0
      },
      {
        replicateNo: 2,
        actualWeightMg: 0,
        responses: [{ hplcMethodAnalyteId: 10, response: 0 }],
        internalStandardResponse: 0
      }
    ];

    function Harness() {
      const [reps, setReps] = useState(initialReplicates);
      const save = () => onSavePayload(buildReplicatePayload(reps, false, true));
      return (
        <PinnedLightTheme>
          <ReplicateEntryTable
            methodWeights={mockWeights}
            replicates={reps}
            onChange={setReps}
            hasInternalStandard={true}
            onSave={save}
            footer={<button type="button" onClick={save}>Save Replicates</button>}
          />
        </PinnedLightTheme>
      );
    }

    render(<Harness />);

    const w1 = screen.getByLabelText("Replicate 1 actual weight (mg)");
    await user.click(w1);
    await user.type(w1, "100.5");
    await user.keyboard("{ArrowRight}");
    await user.type(document.activeElement as HTMLElement, "2000");
    await user.keyboard("{ArrowRight}");
    await user.type(document.activeElement as HTMLElement, "1500");

    // Move down to row 2 IS area, type value, then move left and enter remaining values
    await user.keyboard("{Enter}");
    await user.type(document.activeElement as HTMLElement, "1500");
    await user.keyboard("{ArrowLeft}");
    await user.type(document.activeElement as HTMLElement, "2100");
    await user.keyboard("{ArrowLeft}");
    await user.type(document.activeElement as HTMLElement, "101.2");

    await user.click(screen.getByRole("button", { name: "Save Replicates" }));

    expect(onSavePayload).toHaveBeenCalledTimes(1);
    expect(onSavePayload).toHaveBeenCalledWith([
      {
        actualWeightMg: 100.5,
        responses: [{ hplcMethodAnalyteId: 10, response: 2000 }],
        internalStandardResponse: 1500
      },
      {
        actualWeightMg: 101.2,
        responses: [{ hplcMethodAnalyteId: 10, response: 2100 }],
        internalStandardResponse: 1500
      }
    ]);
  });

  describe("replicatesComplete", () => {
    it("permits response 0 in ResidualSolvents mode", () => {
      const repZero: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 50.0,
        responses: [{ hplcMethodAnalyteId: 10, response: 0 }]
      };
      expect(replicatesComplete(mockWeights, [repZero], true)).toBe(true);
    });

    it("requires response > 0 in Assay mode", () => {
      const repZero: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 50.0,
        responses: [{ hplcMethodAnalyteId: 10, response: 0 }]
      };
      expect(replicatesComplete(mockWeights, [repZero], false)).toBe(false);

      const repPositive: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 50.0,
        responses: [{ hplcMethodAnalyteId: 10, response: 1500 }]
      };
      expect(replicatesComplete(mockWeights, [repPositive], false)).toBe(true);
    });

    it("requires actualWeightMg > 0 in both modes", () => {
      const repNoWeight: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 0,
        responses: [{ hplcMethodAnalyteId: 10, response: 1500 }]
      };
      expect(replicatesComplete(mockWeights, [repNoWeight], true)).toBe(false);
      expect(replicatesComplete(mockWeights, [repNoWeight], false)).toBe(false);
    });

    it("in IS mode, requires internalStandardResponse > 0", () => {
      const repZeroIS: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 50.0,
        responses: [{ hplcMethodAnalyteId: 10, response: 1500 }],
        internalStandardResponse: 0
      };
      expect(replicatesComplete(mockWeights, [repZeroIS], false, true)).toBe(false);

      const repValidIS: HplcReplicateDto = {
        replicateNo: 1,
        actualWeightMg: 50.0,
        responses: [{ hplcMethodAnalyteId: 10, response: 1500 }],
        internalStandardResponse: 3000
      };
      expect(replicatesComplete(mockWeights, [repValidIS], false, true)).toBe(true);
    });
  });
});
