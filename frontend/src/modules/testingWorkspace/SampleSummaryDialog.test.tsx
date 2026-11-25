import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { SampleSummaryDialog } from "./SampleSummaryDialog";
import type { SampleSummary } from "./types/sampleSummaryTypes";
import { PERMISSIONS } from "../../routes/routes";
import { labelService } from "../../services/labelService";
import { actionAvailabilityService } from "../../services/actionAvailabilityService";
import { oosInvestigationService } from "../../services/oosInvestigationService";

const mockGetSummary = vi.fn();
const mockGetSampleVersion = vi.fn();
const mockCompleteReview = vi.fn();
const mockDecideApproval = vi.fn();

// Network boundary: the server's answer about each gated action.
vi.mock("../../services/actionAvailabilityService", () => ({
  actionAvailabilityService: { get: vi.fn() }
}));

vi.mock("../../services/oosInvestigationService", () => ({
  oosInvestigationService: {
    getForSample: vi.fn(),
    openManually: vi.fn()
  }
}));

vi.mock("./services/SampleSummaryService", () => ({
  SampleSummaryService: {
    getSummary: (...args: unknown[]) => mockGetSummary(...args),
    getSampleVersion: (...args: unknown[]) => mockGetSampleVersion(...args),
    completeReview: (...args: unknown[]) => mockCompleteReview(...args),
    exportPdf: vi.fn(),
    returnTestToAnalyst: vi.fn(),
    decideApproval: (...args: unknown[]) => mockDecideApproval(...args)
  }
}));

vi.mock("../users/services/UserService", () => ({
  UserService: {
    getEligibleAnalysts: vi.fn().mockResolvedValue([])
  }
}));

vi.mock("../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

vi.mock("../../contexts/AuthContext", () => ({
  useOptionalAuth: () => null,
  useAuth: () => ({
    permissions: [
      PERMISSIONS.SAMPLES_REVIEW,
      PERMISSIONS.SAMPLES_APPROVE,
      PERMISSIONS.SAMPLES_RECEIVE_OWN_LAB,
      PERMISSIONS.LABELS_REPRINT,
      PERMISSIONS.OOS_MANAGE
    ],
    fullName: "QC Reviewer",
    username: "qcreviewer",
    role: "Reviewer"
  })
}));

// Mock SignatureDialog to immediately trigger onConfirm with password
vi.mock("../../components/SignatureDialog", () => ({
  SignatureDialog: ({
    open,
    meaningStatement,
    onConfirm
  }: {
    open: boolean;
    meaningStatement?: string;
    onConfirm: (p: string) => Promise<void>;
  }) =>
    open ? (
      <div data-testid="mock-signature-dialog">
        <span>{meaningStatement}</span>
        <button onClick={() => void onConfirm("secret123")}>confirm-signature</button>
      </div>
    ) : null
}));

afterEach(cleanup);

beforeEach(() => {
  mockGetSummary.mockReset();
  mockGetSampleVersion.mockReset();
  mockGetSampleVersion.mockResolvedValue(5);
  mockCompleteReview.mockReset();
  mockDecideApproval.mockReset();
  vi.mocked(actionAvailabilityService.get).mockReset();
  vi.mocked(actionAvailabilityService.get).mockResolvedValue([]);
  vi.mocked(oosInvestigationService.getForSample).mockReset();
  vi.mocked(oosInvestigationService.getForSample).mockResolvedValue([]);
  vi.mocked(oosInvestigationService.openManually).mockReset();
  vi.mocked(labelService.getStatus).mockReset();
  vi.mocked(labelService.getStatus).mockResolvedValue([]);
});

const createSample = (over: Partial<SampleSummary> = {}): SampleSummary => ({
  sampleId: 77,
  referenceNumber: "RM0926077",
  category: "RawMaterial",
  displayName: "Microcrystalline Cellulose",
  productionStage: null,
  causeOfTesting: "Routine",
  batchNumber: "LOT-MCC-01",
  grade: "NF",
  retentionQuantity: null,
  retentionQuantityUnit: null,
  controlNumber: "CTRL-01",
  status: "UnderReview",
  statusDisplay: "Under review",
  receivedByName: "Receiver",
  receivedAt: "2026-10-26T08:00:00Z",
  sampledBy: "QC",
  sampleQuantity: "500 g",
  mfgDate: "2026-01-01T00:00:00Z",
  expDate: "2028-01-01T00:00:00Z",
  waterSamplingPointCode: null,
  waterSamplingPointLocation: null,
  storageCondition: null,
  storageTimeHours: null,
  reviewedByName: null,
  reviewedAt: null,
  approvedByName: null,
  approvedAt: null,
  approvalDecision: null,
  certificateRemarks: null,
  preparation: null,
  signatures: [],
  testOrders: [],
  timeline: [],
  certificate: { sample: { matrix: null, simple: null } } as never,
  sections: [
    {
      sectionId: 10,
      sectionCode: "FP",
      sectionName: "Physicochemical",
      status: "UnderReview",
      canView: true,
      reviewedByName: null,
      reviewedAt: null,
      approvedByName: null,
      approvedAt: null,
      approvalDecision: null,
      certificateRemarks: null,
      coaAvailable: false,
      closedByName: null,
      closedAt: null,
      closeReason: null
    }
  ],
  overallStatus: "InProgress",
  combinedCoaAvailable: false,
  allSectionsVisible: true,
  ...over
});

describe("SampleSummaryDialog review flow", () => {
  it("sends retentionQuantity and retentionQuantityUnit when completing review for RawMaterial FP", async () => {
    const sample = createSample({ category: "RawMaterial" });
    mockGetSummary.mockResolvedValue(sample);
    mockCompleteReview.mockResolvedValue(undefined);

    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    expect(await screen.findByText("Complete overview of test execution, incubation stages, results, and approvals.")).toBeTruthy();
    expect(screen.getByLabelText(/Retention quantity/i)).toBeTruthy();

    // Fill retention quantity and select unit
    fireEvent.change(screen.getByLabelText(/Retention quantity/i), { target: { value: "50" } });
    await userEvent.click(screen.getByRole("combobox", { name: /Unit/i }));
    await userEvent.click(screen.getByRole("option", { name: "g" }));

    // Click Submit Review
    const submitReviewBtn = screen.getByRole("button", { name: /Submit Review/i });
    expect((submitReviewBtn as HTMLButtonElement).disabled).toBe(false);
    fireEvent.click(submitReviewBtn);

    // Signature dialog appears
    expect(await screen.findByTestId("mock-signature-dialog")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "confirm-signature", hidden: true }));

    await waitFor(() => expect(mockCompleteReview).toHaveBeenCalledTimes(1));
    expect(mockCompleteReview).toHaveBeenCalledWith(
      77,
      "secret123",
      undefined,
      10,
      50,
      "g"
    );
  }, 15000);

  it("does not send retention fields when completing review for FinishedProduct", async () => {
    const sample = createSample({
      category: "FinishedProduct",
      sections: [
        {
          sectionId: 10,
          sectionCode: "FP",
          sectionName: "Physicochemical",
          status: "UnderReview",
          canView: true,
          reviewedByName: null,
          reviewedAt: null,
          approvedByName: null,
          approvedAt: null,
          approvalDecision: null,
          certificateRemarks: null,
          coaAvailable: false,
          closedByName: null,
          closedAt: null,
          closeReason: null
        }
      ]
    });
    mockGetSummary.mockResolvedValue(sample);
    mockCompleteReview.mockResolvedValue(undefined);

    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    expect(await screen.findByText("Complete overview of test execution, incubation stages, results, and approvals.")).toBeTruthy();
    expect(screen.queryByLabelText(/Retention quantity/i)).toBeNull();

    const submitReviewBtn = screen.getByRole("button", { name: /Submit Review/i });
    expect((submitReviewBtn as HTMLButtonElement).disabled).toBe(false);
    fireEvent.click(submitReviewBtn);

    expect(await screen.findByTestId("mock-signature-dialog")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "confirm-signature", hidden: true }));

    await waitFor(() => expect(mockCompleteReview).toHaveBeenCalledTimes(1));
    expect(mockCompleteReview).toHaveBeenCalledWith(
      77,
      "secret123",
      undefined,
      10,
      undefined,
      undefined
    );
  }, 15000);

  it("disables review submission when retention quantity is 0", async () => {
    const sample = createSample({ category: "RawMaterial" });
    mockGetSummary.mockResolvedValue(sample);

    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    expect(await screen.findByLabelText(/Retention quantity/i)).toBeTruthy();

    fireEvent.change(screen.getByLabelText(/Retention quantity/i), { target: { value: "0" } });
    await userEvent.click(screen.getByRole("combobox", { name: /Unit/i }));
    await userEvent.click(screen.getByRole("option", { name: "g" }));

    const submitReviewBtn = screen.getByRole("button", { name: /Submit Review/i });
    expect((submitReviewBtn as HTMLButtonElement).disabled).toBe(true);
    expect(mockCompleteReview).not.toHaveBeenCalled();
  }, 15000);

  it("disables review submission when retention quantity is negative", async () => {
    const sample = createSample({ category: "RawMaterial" });
    mockGetSummary.mockResolvedValue(sample);

    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    expect(await screen.findByLabelText(/Retention quantity/i)).toBeTruthy();

    fireEvent.change(screen.getByLabelText(/Retention quantity/i), { target: { value: "-5" } });
    await userEvent.click(screen.getByRole("combobox", { name: /Unit/i }));
    await userEvent.click(screen.getByRole("option", { name: "g" }));

    const submitReviewBtn = screen.getByRole("button", { name: /Submit Review/i });
    expect((submitReviewBtn as HTMLButtonElement).disabled).toBe(true);
    expect(mockCompleteReview).not.toHaveBeenCalled();
  }, 15000);

  it("disables review submission when retention unit is empty", async () => {
    const sample = createSample({ category: "RawMaterial" });
    mockGetSummary.mockResolvedValue(sample);

    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    expect(await screen.findByLabelText(/Retention quantity/i)).toBeTruthy();

    fireEvent.change(screen.getByLabelText(/Retention quantity/i), { target: { value: "50" } });

    const submitReviewBtn = screen.getByRole("button", { name: /Submit Review/i });
    expect((submitReviewBtn as HTMLButtonElement).disabled).toBe(true);
    expect(mockCompleteReview).not.toHaveBeenCalled();
  }, 15000);

  it("shows the print label button with the record's code", async () => {
    const user = userEvent.setup();
    const sample = createSample();
    mockGetSummary.mockResolvedValue(sample);
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "Sample",
        entityId: 77,
        canPrint: true,
        blockedReason: null,
        printCount: 0,
        defaultCopies: 1
      }
    ]);

    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    const printBtn = await screen.findByRole("button", { name: "Print label" });
    await waitFor(() => {
      expect((printBtn as HTMLButtonElement).disabled).toBe(false);
    });

    await user.click(printBtn);

    const printDialog = await screen.findByRole("dialog", { name: "Print labels" });
    expect(await within(printDialog).findByText("RM0926077")).toBeTruthy();
  });
});

describe("SampleSummaryDialog gated actions", () => {
  function mount() {
    return render(
      <MemoryRouter>
        <PinnedLightTheme>
          <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );
  }

  it("a reviewer who performed a test sees Complete review disabled with the server's sentence", async () => {
    const sentence = "You performed a test on this sample, so you cannot review it.";
    mockGetSummary.mockResolvedValue(createSample({ category: "FinishedProduct" }));
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      { action: "review.complete", allowed: false, reason: sentence }
    ]);
    const user = userEvent.setup();
    const renderDialog = () =>
      render(
        <MemoryRouter>
          <PinnedLightTheme>
            <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
          </PinnedLightTheme>
        </MemoryRouter>
      );

    renderDialog();

    const reason = await screen.findByText(sentence);
    const button = screen.getByRole("button", { name: /Submit Review/i });
    expect(button.getAttribute("aria-disabled")).toBe("true");
    expect(document.getElementById(button.getAttribute("aria-describedby") ?? "")?.contains(reason)).toBe(true);
    expect(vi.mocked(actionAvailabilityService.get)).toHaveBeenCalledWith("sample", 77, 10, expect.any(AbortSignal));

    await user.click(button);
    expect(screen.queryByTestId("mock-signature-dialog")).toBeNull();
    expect(mockCompleteReview).not.toHaveBeenCalled();
  }, 15000);

  it("an analyst sees Approve disabled with the role sentence", async () => {
    const sentence = "Your role does not include this action.";
    mockGetSummary.mockResolvedValue(
      createSample({
        status: "UnderApproval",
        certificate: { sample: { matrix: null, simple: null }, sections: [] } as never,
        sections: (createSample().sections ?? []).map((section) => ({ ...section, status: "UnderApproval" }))
      })
    );
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      { action: "approval.approve", allowed: false, reason: sentence },
      { action: "approval.reject", allowed: true, reason: null }
    ]);
    const user = userEvent.setup();
    const renderDialog = () =>
      render(
        <MemoryRouter>
          <PinnedLightTheme>
            <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
          </PinnedLightTheme>
        </MemoryRouter>
      );

    renderDialog();

    expect(await screen.findByText(sentence)).toBeTruthy();
    const button = screen.getByRole("button", { name: /Submit Decision/i });
    expect(button.getAttribute("aria-disabled")).toBe("true");
    await user.click(button);
    expect(screen.queryByTestId("mock-signature-dialog")).toBeNull();
    expect(mockDecideApproval).not.toHaveBeenCalled();

    // The same screen gates Not Conform on its own action: allowed here, so the sentence goes away.
    await user.click(screen.getByRole("radio", { name: /Not Conform/i }));
    await waitFor(() => expect(screen.queryByText(sentence)).toBeNull());
    expect(screen.getByRole("button", { name: /Submit Decision/i }).getAttribute("aria-disabled")).toBeNull();
  }, 15000);

  it("when allowed, the release decision request payload is unchanged", async () => {
    mockGetSummary.mockResolvedValue(
      createSample({
        status: "UnderApproval",
        certificate: { sample: { matrix: null, simple: null }, sections: [] } as never,
        sections: (createSample().sections ?? []).map((section) => ({ ...section, status: "UnderApproval" }))
      })
    );
    mockDecideApproval.mockResolvedValue(undefined);
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      { action: "approval.approve", allowed: true, reason: null },
      { action: "approval.reject", allowed: true, reason: null }
    ]);
    const user = userEvent.setup();
    const renderDialog = () =>
      render(
        <MemoryRouter>
          <PinnedLightTheme>
            <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
          </PinnedLightTheme>
        </MemoryRouter>
      );

    renderDialog();

    await user.click(await screen.findByRole("button", { name: /Submit Decision/i }));
    await user.click(await screen.findByRole("button", { name: "confirm-signature", hidden: true }));
    await waitFor(() => expect(mockDecideApproval).toHaveBeenCalledTimes(1));
    expect(mockDecideApproval).toHaveBeenCalledWith(
      77, "secret123", "Approve", undefined, undefined, undefined, undefined, undefined, 10
    );
  }, 15000);
});

describe("SampleSummaryDialog in Arabic RTL", () => {
  afterEach(async () => {
    await resetLanguage();
  });

  it("renders RTL layout, Arabic heading, and sample identifier in LTR bdi", async () => {
    mockGetSummary.mockResolvedValue(createSample());
    await renderInArabic(
      <MemoryRouter>
        <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
      </MemoryRouter>,
      ["common", "testingWorkspace"]
    );

    expect(document.documentElement.dir).toBe("rtl");
    expect(await screen.findByText("ملخص العينة")).toBeTruthy();
    const refNodes = await screen.findAllByText("RM0926077");
    expect(refNodes.some((node) => node.closest("bdi")?.getAttribute("dir") === "ltr")).toBe(true);
  }, 15000);
});

describe("SampleSummaryDialog OOS banner and retest options", () => {
  it("shows banner link, disables approval with server reason, and hides retest/new-sample options when investigation exists", async () => {
    mockGetSummary.mockResolvedValue(
      createSample({
        status: "UnderApproval",
        certificate: { sample: { matrix: null, simple: null }, sections: [] } as never,
        sections: (createSample().sections ?? []).map((section) => ({ ...section, status: "UnderApproval" }))
      })
    );
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      { action: "approval.approve", allowed: false, reason: "OOS investigation OOS-2026-0042 is open." },
      { action: "approval.reject", allowed: false, reason: "OOS investigation OOS-2026-0042 is open." }
    ]);
    vi.mocked(oosInvestigationService.getForSample).mockResolvedValue([
      {
        id: 42,
        number: "OOS-2026-0042",
        kind: "Oos",
        status: "PhaseOne",
        sampleId: 77,
        sampleReferenceNumber: "RM0926077",
        sampleDisplayName: "Microcrystalline Cellulose",
        batchNumber: null,
        sectionId: 10,
        sectionName: "Microbiology",
        openedAtUtc: "2026-11-20T10:00:00Z",
        openedByName: "Reviewer",
        phaseOneDueDate: null,
        dueDate: null,
        isOverdue: false,
        conclusion: null,
        concludedAtUtc: null,
        rootCauseCategoryName: null,
        testCodes: ["TAMC"],
        version: 1
      }
    ]);

    const renderDialog = () =>
      render(
        <MemoryRouter>
          <PinnedLightTheme>
            <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
          </PinnedLightTheme>
        </MemoryRouter>
      );

    renderDialog();

    const link = await screen.findByRole("link", { name: /OOS-2026-0042/i });
    expect(link.getAttribute("href")).toBe("/oos-tracking/42");
    expect(screen.getAllByText("OOS investigation OOS-2026-0042 is open.").length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: /Submit Decision/i }).getAttribute("aria-disabled")).toBe("true");
    expect(screen.queryByRole("radio", { name: /Retest Retained Sample/i })).toBeNull();
    expect(screen.queryByRole("radio", { name: /New Sample Required/i })).toBeNull();
  }, 15000);

  it("keeps retest and new-sample options present when no investigation exists and allows manual open on blocked approval", async () => {
    const user = userEvent.setup();
    mockGetSummary.mockResolvedValue(
      createSample({
        status: "UnderApproval",
        certificate: { sample: { matrix: null, simple: null }, sections: [] } as never,
        sections: (createSample().sections ?? []).map((section) => ({ ...section, status: "UnderApproval" }))
      })
    );
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      {
        action: "approval.approve",
        allowed: false,
        reason: "This sample has an out-of-specification result without an investigation."
      }
    ]);
    vi.mocked(oosInvestigationService.getForSample).mockResolvedValue([]);
    vi.mocked(oosInvestigationService.openManually).mockResolvedValue({
      id: 55,
      number: "OOS-2026-0055",
      version: 1
    });

    const renderDialog = () =>
      render(
        <MemoryRouter>
          <PinnedLightTheme>
            <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
          </PinnedLightTheme>
        </MemoryRouter>
      );

    renderDialog();

    expect(await screen.findByRole("radio", { name: /Retest Retained Sample/i })).toBeTruthy();
    expect(screen.getByRole("radio", { name: /New Sample Required/i })).toBeTruthy();

    await user.click(screen.getByRole("button", { name: /Open investigation/i }));
    await waitFor(() =>
      expect(oosInvestigationService.openManually).toHaveBeenCalledWith(77, 10, 5)
    );
  }, 15000);
});
