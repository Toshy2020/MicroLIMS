import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { TitrationPanel } from "./TitrationPanel";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import type { TitrationContext } from "./types/testWorkflowTypes";

const getTitrationContext = vi.fn();
const recordTitrationResult = vi.fn();

vi.mock("./services/TestWorkflowService", () => ({
  TestWorkflowService: {
    getTitrationContext: (...a: unknown[]) => getTitrationContext(...a),
    recordTitrationResult: (...a: unknown[]) => recordTitrationResult(...a)
  }
}));
vi.mock("./services/SampleSummaryService", () => ({ SampleSummaryService: { getSummary: vi.fn().mockResolvedValue({ testOrders: [] }) } }));
vi.mock("../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: { getConfiguredSummary: vi.fn().mockResolvedValue([]) }
}));
vi.mock("../../services/laboratorySectionService", () => ({ getSections: vi.fn().mockResolvedValue([]) }));
// The real dialog needs auth context; this stand-in signs with a fixed password and exposes meaningStatement.
vi.mock("../../components/SignatureDialog", () => ({
  SignatureDialog: ({ open, meaningStatement, onConfirm }: { open: boolean; meaningStatement?: string; onConfirm: (p: string) => Promise<void> }) =>
    open ? (
      <div>
        <span>{meaningStatement}</span>
        <button onClick={() => void onConfirm("pw")}>confirm-signature</button>
      </div>
    ) : null
}));

const prep = (over: Record<string, unknown>) => ({
  preparationId: 1,
  code: "TP-1",
  expiresAt: null,
  factor: 1.002,
  factorState: "Valid",
  standardizedAt: null,
  validUntil: null,
  standardizationId: 5,
  standardizationTemperatureC: null,
  usable: true,
  warning: null,
  blockReason: null,
  ...over
});

const baseCtx = (over: Partial<TitrationContext> = {}): TitrationContext => ({
  testOrderId: 7,
  testCode: "ASC",
  displayName: "Ascorbic acid assay",
  titrationType: "Redox",
  nonAqueous: false,
  mode: "Direct",
  calculation: "UspFactor",
  endpoint: "Visual",
  indicator: "starch",
  equivalencyFactor: 88.06,
  blankRequired: false,
  replicateCount: 2,
  maxRsdPercent: 2,
  tempCorrection: false,
  expansionCoefficient: null,
  excessVolumeMl: null,
  titrant: { solutionMasterId: 1, name: "Iodine VS", nominalStrength: 0.1, strengthUnit: "Normal" },
  excessTitrant: null,
  titrantPreparations: [
    prep({}),
    prep({ preparationId: 2, code: "TP-2", factor: null, factorState: "NotStandardized", usable: false, blockReason: "Titrant is not standardized" })
  ] as never,
  excessPreparations: [],
  standardLots: [],
  specifications: [{ specificationId: 11, parameterName: "Assay", resultBasis: "PercentAsIs", unit: "%", specLimit: "99-101", labelClaim: null, labelClaimUnit: null }],
  ...over
});

const renderPanel = async (ctx: TitrationContext) => {
  getTitrationContext.mockResolvedValue(ctx);
  const onRecorded = vi.fn();
  render(
    <PinnedLightTheme>
      <TitrationPanel testOrderId={7} displayName="Ascorbic acid assay" current={{ workflowType: "Titration" }} onRecorded={onRecorded} />
    </PinnedLightTheme>
  );
  await screen.findByText("Method");
  return onRecorded;
};

beforeEach(() => {
  getTitrationContext.mockReset();
  recordTitrationResult.mockReset();
});
afterEach(cleanup);

describe("TitrationPanel", () => {
  it("renders the method card from the context", async () => {
    await renderPanel(baseCtx());
    expect(screen.getByText("Redox")).toBeTruthy();
    expect(screen.getByText("Direct")).toBeTruthy();
    expect(screen.getByText("USP factor")).toBeTruthy();
    expect(screen.getByText("Visual: starch")).toBeTruthy();
    expect(screen.getByText(/Iodine VS/)).toBeTruthy();
    expect(screen.getByText(/88.06 mg\/mEq/)).toBeTruthy();
  });

  it("disables blocked titrant options and shows the reason", async () => {
    await renderPanel(baseCtx());
    await userEvent.click(screen.getByRole("combobox", { name: /Titrant preparation/ }));
    const blocked = screen.getByRole("option", { name: /TP-2/ });
    expect(blocked.getAttribute("aria-disabled")).toBe("true");
    expect(screen.getByText("Titrant is not standardized")).toBeTruthy();
    expect(screen.getByRole("option", { name: /TP-1/ }).getAttribute("aria-disabled")).not.toBe("true");
  });

  it("shows the Due warning for the selected preparation", async () => {
    await renderPanel(
      baseCtx({ titrantPreparations: [prep({ factorState: "Due", warning: "Titrant standardization is due" })] as never })
    );
    await userEvent.click(screen.getByRole("combobox", { name: /Titrant preparation/ }));
    await userEvent.click(screen.getByRole("option", { name: /TP-1/ }));
    expect(screen.getByText("Titrant standardization is due")).toBeTruthy();
  });

  it("shows the blank row only when a blank is required", async () => {
    await renderPanel(baseCtx());
    expect(screen.queryByLabelText(/Blank volume/)).toBeNull();
    cleanup();
    await renderPanel(baseCtx({ blankRequired: true }));
    expect(screen.getByLabelText(/Blank volume/)).toBeTruthy();
  });

  it("shows the standard section only for the relative method, with add/remove rows", async () => {
    await renderPanel(baseCtx());
    expect(screen.queryByText(/Reference standard titrations/)).toBeNull();
    cleanup();
    await renderPanel(
      baseCtx({
        calculation: "Relative",
        equivalencyFactor: null,
        standardLots: [{ materialId: 40, lotLabel: "LOT-9", kind: "WorkingStandard", purityPercent: 99.5, moisturePercent: null, expiryDate: null, quantityRemaining: 500, unit: "mg" }]
      })
    );
    expect(screen.getByText(/Reference standard titrations/)).toBeTruthy();
    await userEvent.click(screen.getByRole("button", { name: /Add standard titration/ }));
    expect(screen.getByText("Std 2")).toBeTruthy();
    await userEvent.click(screen.getByRole("button", { name: "Remove standard 2" }));
    expect(screen.queryByText("Std 2")).toBeNull();
  });

  it("asks for the excess preparation in residual mode", async () => {
    await renderPanel(
      baseCtx({
        mode: "Residual",
        excessVolumeMl: 25,
        excessTitrant: { solutionMasterId: 3, name: "Iodine VS excess", nominalStrength: 0.1, strengthUnit: "Normal" },
        excessPreparations: [prep({ preparationId: 9, code: "EX-1" })] as never
      })
    );
    expect(screen.getByRole("combobox", { name: /Excess titrant preparation/ })).toBeTruthy();
  });

  it("posts the exact payload (relative, blank, temperature-free, avg unit weight)", async () => {
    recordTitrationResult.mockResolvedValue({ outcomeSummary: "ok", status: "WithinLimits" });
    const ctx = baseCtx({
      calculation: "Relative",
      equivalencyFactor: null,
      blankRequired: true,
      standardLots: [{ materialId: 40, lotLabel: "LOT-9", kind: "ReferenceStandard", purityPercent: 99.5, moisturePercent: null, expiryDate: null, quantityRemaining: 500, unit: "mg" }],
      specifications: [{ specificationId: 11, parameterName: "Assay", resultBasis: "MgPerUnit", unit: "mg", specLimit: "450-550", labelClaim: 500, labelClaimUnit: "mg" }]
    });
    const onRecorded = await renderPanel(ctx);

    await userEvent.click(screen.getByRole("combobox", { name: /Titrant preparation/ }));
    await userEvent.click(screen.getByRole("option", { name: /TP-1/ }));
    fireEvent.change(screen.getByLabelText(/Blank volume/), { target: { value: "0.1" } });
    fireEvent.change(screen.getByLabelText(/Average unit weight/), { target: { value: "600" } });
    fireEvent.change(screen.getByLabelText(/Standard weight \(mg\)/), { target: { value: "100" } });
    fireEvent.change(screen.getByLabelText("Titre (mL)"), { target: { value: "10.5" } });
    fireEvent.change(screen.getByLabelText("Rep 1 Sample weight (mg)"), { target: { value: "120" } });
    fireEvent.change(screen.getByLabelText("Rep 1 Titrant volume (mL)"), { target: { value: "10" } });
    fireEvent.change(screen.getByLabelText("Rep 2 Sample weight (mg)"), { target: { value: "121" } });
    fireEvent.change(screen.getByLabelText("Rep 2 Titrant volume (mL)"), { target: { value: "10.1" } });

    await userEvent.click(screen.getByRole("button", { name: /Sign and record result/ }));
    await userEvent.click(screen.getByRole("button", { name: "confirm-signature" }));

    await waitFor(() => expect(recordTitrationResult).toHaveBeenCalledTimes(1));
    const [orderId, payload] = recordTitrationResult.mock.calls[0];
    expect(orderId).toBe(7);
    expect(payload).toMatchObject({
      equipmentId: null,
      titrantPreparationId: 1,
      excessPreparationId: null,
      blankVolumeMl: 0.1,
      titrationTemperatureC: null,
      lossOnDryingPercent: null,
      averageUnitWeightMg: 600,
      standards: [{ materialId: 40, weightMg: 100, titreMl: 10.5 }],
      specificationIds: [11],
      replicates: [
        { sampleWeightMg: 120, titrantVolumeMl: 10 },
        { sampleWeightMg: 121, titrantVolumeMl: 10.1 }
      ],
      password: "pw",
      comment: null,
      dueTitrantAcknowledged: null,
      dueTitrantJustification: null
    });
    expect(payload).not.toHaveProperty("standardizationTemperatureC");
    expect(payload).not.toHaveProperty("standard");
    await waitFor(() => expect(onRecorded).toHaveBeenCalled());
  }, 15000);

  it("shows the server error message verbatim", async () => {
    recordTitrationResult.mockRejectedValue({ response: { data: { message: "Titrant factor is Due." } } });
    await renderPanel(baseCtx());
    await userEvent.click(screen.getByRole("combobox", { name: /Titrant preparation/ }));
    await userEvent.click(screen.getByRole("option", { name: /TP-1/ }));
    for (const r of [1, 2]) {
      fireEvent.change(screen.getByLabelText(`Rep ${r} Sample weight (mg)`), { target: { value: "120" } });
      fireEvent.change(screen.getByLabelText(`Rep ${r} Titrant volume (mL)`), { target: { value: "10" } });
    }
    await userEvent.click(screen.getByRole("button", { name: /Sign and record result/ }));
    await userEvent.click(screen.getByRole("button", { name: "confirm-signature" }));
    expect(await screen.findByText("Titrant factor is Due.")).toBeTruthy();
  });

  it("disables submit until checkbox and 10-char justification are filled when titrant has a warning", async () => {
    recordTitrationResult.mockResolvedValue({ outcomeSummary: "ok", status: "WithinLimits" });
    const ctx = baseCtx({
      titrantPreparations: [prep({ factorState: "Due", warning: "Titrant standardization is due" })] as never
    });
    await renderPanel(ctx);

    await userEvent.click(screen.getByRole("combobox", { name: /Titrant preparation/ }));
    await userEvent.click(screen.getByRole("option", { name: /TP-1/ }));

    // Warning alert is visible
    expect(screen.getByText("Titrant standardization is due")).toBeTruthy();

    // Checkbox and justification field are rendered
    const ackCheckbox = screen.getByRole("checkbox", { name: /I acknowledge this titrant is due for standardization/i });
    const justificationInput = screen.getByLabelText(/Justification for using due titrant/i);
    expect(ackCheckbox).toBeTruthy();
    expect(justificationInput).toBeTruthy();

    // Fill the required replicate measurements
    for (const r of [1, 2]) {
      fireEvent.change(screen.getByLabelText(`Rep ${r} Sample weight (mg)`), { target: { value: "120" } });
      fireEvent.change(screen.getByLabelText(`Rep ${r} Titrant volume (mL)`), { target: { value: "10" } });
    }

    const submitBtn = screen.getByRole("button", { name: /Sign and record result/i });

    // Submit disabled when neither checkbox nor justification is provided
    expect((submitBtn as HTMLButtonElement).disabled).toBe(true);

    // Check the box - still disabled because justification is missing
    await userEvent.click(ackCheckbox);
    expect((submitBtn as HTMLButtonElement).disabled).toBe(true);

    // Type 9 characters - still disabled (minimum 10 chars required)
    fireEvent.change(justificationInput, { target: { value: "123456789" } });
    expect((submitBtn as HTMLButtonElement).disabled).toBe(true);

    // Type 10+ characters - now enabled
    const validJustification = "Approved by QC Manager for emergency analysis";
    fireEvent.change(justificationInput, { target: { value: validJustification } });
    expect((submitBtn as HTMLButtonElement).disabled).toBe(false);

    // Click submit and verify signature statement
    await userEvent.click(submitBtn);
    expect(
      screen.getByText("Your signature records the result and your acknowledgement of the due titrant.")
    ).toBeTruthy();

    // Confirm signature
    await userEvent.click(screen.getByRole("button", { name: "confirm-signature" }));

    await waitFor(() => expect(recordTitrationResult).toHaveBeenCalledTimes(1));
    const [orderId, payload] = recordTitrationResult.mock.calls[0];
    expect(orderId).toBe(7);
    expect(payload.dueTitrantAcknowledged).toBe(true);
    expect(payload.dueTitrantJustification).toBe(validJustification);
  }, 15000);

  it("requires acknowledgement when excess titrant has a warning in residual mode", async () => {
    recordTitrationResult.mockResolvedValue({ outcomeSummary: "ok", status: "WithinLimits" });
    const ctx = baseCtx({
      mode: "Residual",
      excessVolumeMl: 25,
      excessTitrant: { solutionMasterId: 3, name: "Iodine VS excess", nominalStrength: 0.1, strengthUnit: "Normal" },
      titrantPreparations: [prep({ preparationId: 1, code: "TP-1", warning: null })] as never,
      excessPreparations: [prep({ preparationId: 9, code: "EX-1", factorState: "Due", warning: "Excess titrant standardization is due" })] as never
    });
    await renderPanel(ctx);

    await userEvent.click(screen.getByRole("combobox", { name: /Titrant preparation/ }));
    await userEvent.click(screen.getByRole("option", { name: /TP-1/ }));

    await userEvent.click(screen.getByRole("combobox", { name: /Excess titrant preparation/ }));
    await userEvent.click(screen.getByRole("option", { name: /EX-1/ }));

    // Warning from excess titrant is shown
    expect(screen.getByText("Excess titrant standardization is due")).toBeTruthy();

    for (const r of [1, 2]) {
      fireEvent.change(screen.getByLabelText(`Rep ${r} Sample weight (mg)`), { target: { value: "120" } });
      fireEvent.change(screen.getByLabelText(`Rep ${r} Titrant volume (mL)`), { target: { value: "10" } });
    }

    const submitBtn = screen.getByRole("button", { name: /Sign and record result/i });
    expect((submitBtn as HTMLButtonElement).disabled).toBe(true);

    const ackCheckbox = screen.getByRole("checkbox", { name: /I acknowledge this titrant is due for standardization/i });
    const justificationInput = screen.getByLabelText(/Justification for using due titrant/i);

    await userEvent.click(ackCheckbox);
    expect((submitBtn as HTMLButtonElement).disabled).toBe(true);

    fireEvent.change(justificationInput, { target: { value: "Excess standardized earlier today" } });
    expect((submitBtn as HTMLButtonElement).disabled).toBe(false);

    await userEvent.click(submitBtn);
    expect(
      screen.getByText("Your signature records the result and your acknowledgement of the due titrant.")
    ).toBeTruthy();

    await userEvent.click(screen.getByRole("button", { name: "confirm-signature" }));

    await waitFor(() => expect(recordTitrationResult).toHaveBeenCalledTimes(1));
    const [, payload] = recordTitrationResult.mock.calls[0];
    expect(payload.dueTitrantAcknowledged).toBe(true);
    expect(payload.dueTitrantJustification).toBe("Excess standardized earlier today");
  }, 15000);
});
