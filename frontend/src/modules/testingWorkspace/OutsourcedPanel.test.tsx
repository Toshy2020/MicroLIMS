import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { OutsourcedPanel, OutsourceTestAction } from "./OutsourcedPanel";
import { PERMISSIONS } from "../../routes/routes";
import { AnalysisCard } from "./TestResultCards";

const { mockOutsource, mockUpload, mockRecord, mockGetSpecs, authState } = vi.hoisted(() => ({
  mockOutsource: vi.fn(),
  mockUpload: vi.fn(),
  mockRecord: vi.fn(),
  mockGetSpecs: vi.fn(),
  authState: { permissions: [] as string[] }
}));

vi.mock("./services/TestWorkflowService", () => ({
  TestWorkflowService: {
    outsourceTest: (...a: unknown[]) => mockOutsource(...a),
    uploadExternalReport: (...a: unknown[]) => mockUpload(...a),
    recordOutsourcedResult: (...a: unknown[]) => mockRecord(...a)
  }
}));

vi.mock("../laboratoryConfiguration/specifications/services/SpecificationService", () => ({
  SpecificationService: { getForTestOrder: (...a: unknown[]) => mockGetSpecs(...a) }
}));

vi.mock("../../contexts/AuthContext", () => ({
  useAuth: () => ({ permissions: authState.permissions })
}));

// Mirrors the real SignatureDialog contract: a rejected onConfirm leaves it open with the server's message.
vi.mock("../../components/SignatureDialog", async () => {
  const { useState } = await import("react");
  return {
    SignatureDialog: ({ open, onConfirm }: { open: boolean; onConfirm: (pw: string) => Promise<void> }) => {
      const [error, setError] = useState<string | null>(null);
      if (!open) return null;
      return (
        <div data-testid="signature-dialog">
          {error && <div role="alert">{error}</div>}
          <button
            onClick={() =>
              void onConfirm("pw-123").catch((e: { response?: { data?: { message?: string } } }) => setError(e?.response?.data?.message ?? "Signature failed."))
            }
          >
            confirm-signature
          </button>
        </div>
      );
    }
  };
});

const specs = [
  { id: 11, testCode: "ASSAY", parameterName: "Assay", limitType: "Range", lowerLimit: 98, upperLimit: 102, displayOrder: 1, unit: "%" },
  { id: 12, testCode: "ASSAY", parameterName: "Appearance", limitType: "Qualitative", displayOrder: 2 },
  { id: 13, testCode: "OTHER", parameterName: "Not this test", limitType: "Range", displayOrder: 1 }
];

const renderPanel = (onRecorded = vi.fn(), status?: string) => {
  render(
    <PinnedLightTheme>
      <OutsourcedPanel testOrderId={55} testCode="ASSAY" displayName="Assay" outsourcedToLab="Acme Labs" status={status} onRecorded={onRecorded} />
    </PinnedLightTheme>
  );
  return { onRecorded };
};

const uploadReport = async () => {
  const file = new File(["%PDF-1.4"], "acme-report.pdf", { type: "application/pdf" });
  await userEvent.upload(await screen.findByLabelText("External lab report file"), file);
  await screen.findByText("acme-report.pdf");
};

const fillRows = async () => {
  fireEvent.change(await screen.findByLabelText("External report number"), { target: { value: "AC-2026-17" } });
  fireEvent.change(screen.getByLabelText("Report date"), { target: { value: "2026-11-02" } });
  fireEvent.change(screen.getByLabelText("Value for Assay"), { target: { value: "99.5" } });
  const row = screen.getByText("Appearance").closest("tr") as HTMLElement;
  fireEvent.click(within(row).getByText("Conforms"));
};

describe("OutsourcedPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetSpecs.mockResolvedValue(specs);
    mockUpload.mockResolvedValue({ id: 900, originalFileName: "acme-report.pdf" });
  });
  afterEach(cleanup);

  it("keeps signing disabled until the report is uploaded and every row is filled", async () => {
    renderPanel();
    const sign = (await screen.findByText("Sign and record result")).closest("button") as HTMLButtonElement;
    expect(screen.queryByText("Not this test")).toBeNull();
    await fillRows();
    expect(sign.disabled).toBe(true); // rows filled, no report yet
    await uploadReport();
    await waitFor(() => expect(sign.disabled).toBe(false));
    expect(mockUpload).toHaveBeenCalledWith(55, expect.any(File));
  });

  it("requires an observation when a qualitative row does not conform", async () => {
    renderPanel();
    await uploadReport();
    await fillRows();
    const sign = screen.getByText("Sign and record result").closest("button") as HTMLButtonElement;
    const row = screen.getByText("Appearance").closest("tr") as HTMLElement;
    fireEvent.click(within(row).getByText("Does not conform"));
    expect(sign.disabled).toBe(true);
    fireEvent.change(within(row).getByLabelText("Observation"), { target: { value: "Yellow" } });
    expect(sign.disabled).toBe(false);
  });

  it("sends the report id and one parameter per spec, then shows the server's verdict", async () => {
    mockRecord.mockResolvedValue({ outcomeSummary: "Result recorded", status: "WithinLimits" });
    const { onRecorded } = renderPanel();
    await uploadReport();
    await fillRows();
    fireEvent.click(screen.getByText("Sign and record result"));
    fireEvent.click(await screen.findByText("confirm-signature"));

    await waitFor(() => expect(mockRecord).toHaveBeenCalled());
    const [orderId, payload] = mockRecord.mock.calls[0];
    expect(orderId).toBe(55);
    expect(payload.reportDocumentId).toBe(900);
    expect(payload.externalReportNumber).toBe("AC-2026-17");
    expect(payload.password).toBe("pw-123");
    expect(payload.parameters).toEqual([
      { specificationId: 11, value: 99.5 },
      { specificationId: 12, conforms: true, observation: null }
    ]);
    expect(await screen.findByText(/WithinLimits/)).toBeTruthy();
    expect(onRecorded).toHaveBeenCalled();
  });

  it("sends conforms false with the observation", async () => {
    mockRecord.mockResolvedValue({ outcomeSummary: "Out of spec", status: "OutOfSpecification" });
    renderPanel();
    await uploadReport();
    await fillRows();
    const row = screen.getByText("Appearance").closest("tr") as HTMLElement;
    fireEvent.click(within(row).getByText("Does not conform"));
    fireEvent.change(within(row).getByLabelText("Observation"), { target: { value: "Yellow" } });
    fireEvent.click(screen.getByText("Sign and record result"));
    fireEvent.click(await screen.findByText("confirm-signature"));
    await waitFor(() => expect(mockRecord).toHaveBeenCalled());
    expect(mockRecord.mock.calls[0][1].parameters[1]).toEqual({ specificationId: 12, conforms: false, observation: "Yellow" });
  });
});

describe("OutsourcedPanel refusals and reopening", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetSpecs.mockResolvedValue(specs);
    mockUpload.mockResolvedValue({ id: 900, originalFileName: "acme-report.pdf" });
  });
  afterEach(cleanup);

  it("shows the server's message in the open signature dialog when the record is refused", async () => {
    mockRecord.mockRejectedValue({ response: { data: { message: "Every specification for this test must be supplied exactly once." } } });
    renderPanel();
    await uploadReport();
    await fillRows();
    fireEvent.click(screen.getByText("Sign and record result"));
    fireEvent.click(await screen.findByText("confirm-signature"));
    expect(await screen.findByText("Every specification for this test must be supplied exactly once.")).toBeTruthy();
    expect(screen.getByTestId("signature-dialog")).toBeTruthy(); // still open: retry without re-entering the form
    expect((screen.getByLabelText("External report number") as HTMLInputElement).value).toBe("AC-2026-17");
  });

  it("opens read-only, with the lab and no sign button, once a result is recorded", async () => {
    renderPanel(vi.fn(), "ResultRecorded");
    expect(await screen.findByText(/Acme Labs/)).toBeTruthy();
    expect(screen.queryByText("Sign and record result")).toBeNull();
    expect(screen.queryByLabelText("External report number")).toBeNull();
  });

  it("stays editable while the test is in progress", async () => {
    renderPanel(vi.fn(), "InProgress");
    expect(await screen.findByText("Sign and record result")).toBeTruthy();
  });
});

describe("OutsourceTestAction", () => {
  beforeEach(() => vi.clearAllMocks());
  afterEach(cleanup);

  const renderAction = (status = "Pending", onOutsourced = vi.fn()) => {
    render(
      <PinnedLightTheme>
        <OutsourceTestAction testOrderId={55} status={status} onOutsourced={onOutsourced} />
      </PinnedLightTheme>
    );
    return { onOutsourced };
  };

  it("is hidden from a user without the permission", () => {
    authState.permissions = [];
    renderAction();
    expect(screen.queryByText("Outsource test")).toBeNull();
  });

  it("is hidden once the test has a result", () => {
    authState.permissions = [PERMISSIONS.TESTS_OUTSOURCE];
    renderAction("ResultRecorded");
    expect(screen.queryByText("Outsource test")).toBeNull();
  });

  it("shows the server's message in the signature dialog when outsourcing is refused", async () => {
    authState.permissions = [PERMISSIONS.TESTS_OUTSOURCE];
    mockOutsource.mockRejectedValue({ response: { data: { message: "Only physicochemical tests can be outsourced." } } });
    const { onOutsourced } = renderAction();
    fireEvent.click(screen.getByText("Outsource test"));
    fireEvent.change(await screen.findByLabelText("External laboratory"), { target: { value: "Acme Labs" } });
    fireEvent.change(screen.getByLabelText("Reason for outsourcing"), { target: { value: "No ICP-MS in house" } });
    fireEvent.click(screen.getByText("Continue to signature"));
    fireEvent.click(await screen.findByText("confirm-signature"));
    expect(await screen.findByText("Only physicochemical tests can be outsourced.")).toBeTruthy();
    expect(onOutsourced).not.toHaveBeenCalled();
  });

  it("collects lab and reason, signs, and reports the lab", async () => {
    authState.permissions = [PERMISSIONS.TESTS_OUTSOURCE];
    mockOutsource.mockResolvedValue(true);
    const { onOutsourced } = renderAction("InProgress");
    fireEvent.click(screen.getByText("Outsource test"));
    fireEvent.change(await screen.findByLabelText("External laboratory"), { target: { value: "Acme Labs" } });
    fireEvent.change(screen.getByLabelText("Reason for outsourcing"), { target: { value: "No ICP-MS in house" } });
    fireEvent.click(screen.getByText("Continue to signature"));
    fireEvent.click(await screen.findByText("confirm-signature"));
    await waitFor(() => expect(onOutsourced).toHaveBeenCalledWith("Acme Labs"));
    expect(mockOutsource).toHaveBeenCalledWith(55, { externalLab: "Acme Labs", reason: "No ICP-MS in house", password: "pw-123" });
  });
});

describe("AnalysisCard for an outsourced test", () => {
  afterEach(cleanup);

  const outsourcedTest = (calc: string | null) =>
    ({
      testOrderId: 55,
      testCode: "ASSAY",
      testDisplayName: "Assay",
      isSuperseded: false,
      outsourcedToLab: "Acme Labs",
      analysis: {
        testOrderId: 55,
        analysisType: "Measurement",
        analysedAt: "2026-11-02T00:00:00Z",
        parameterResults: [
          { id: 1, parameterName: "Assay", reportedDisplay: "99.5 %", unit: "%", specLimit: "98-102", comparisonStatus: "WithinLimits", calculationJson: calc, readings: [] }
        ]
      }
    }) as unknown as Parameters<typeof AnalysisCard>[0]["test"];

  it("shows the external lab and a link to the report", () => {
    render(<AnalysisCard test={outsourcedTest(JSON.stringify({ quantity: "Outsourced", reportDocumentId: 900 }))} />);
    expect(screen.getAllByText("External lab: Acme Labs").length).toBeGreaterThan(0);
    expect(screen.getAllByText("External report").length).toBeGreaterThan(0);
  });

  it("shows the lab without a link when the report id is not recorded", () => {
    render(<AnalysisCard test={outsourcedTest(null)} />);
    expect(screen.getAllByText("External lab: Acme Labs").length).toBeGreaterThan(0);
    expect(screen.queryByText("External report")).toBeNull();
  });
});
