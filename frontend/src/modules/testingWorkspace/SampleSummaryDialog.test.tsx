import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { render, screen, cleanup, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { SampleSummaryDialog } from "./SampleSummaryDialog";
import type { SampleSummary } from "./types/sampleSummaryTypes";
import { AuthProvider } from "../../contexts/AuthContext";
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

afterEach(() => {
  cleanup();
  localStorage.clear();
});

const dialog = (
  <MemoryRouter>
    <AuthProvider>
      <SampleSummaryDialog open sampleId={77} onClose={vi.fn()} />
    </AuthProvider>
  </MemoryRouter>
);

function mount() {
  return render(<PinnedLightTheme>{dialog}</PinnedLightTheme>);
}

async function sign(user: ReturnType<typeof userEvent.setup>) {
  const password = await screen.findByLabelText(/^Password/i);
  const signButton = screen.getByRole("button", { name: "Sign" });
  expect(signButton).toHaveProperty("disabled", true);
  await user.type(password, "secret123");
  await user.click(signButton);
}

beforeEach(() => {
  localStorage.setItem("microlims_role", "Reviewer");
  localStorage.setItem("microlims_full_name", "QC Reviewer");
  localStorage.setItem("microlims_permissions", JSON.stringify([PERMISSIONS.SAMPLES_RECEIVE_OWN_LAB]));

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
  certificate: {
    sample: {
      matrix: null,
      simple: null,
      supportingRows: [],
      complies: false,
      conclusionText: "",
      resultDate: null
    },
    sections: []
  },
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
    const user = userEvent.setup();
    const sample = createSample({ category: "RawMaterial" });
    mockGetSummary.mockResolvedValue(sample);
    mockCompleteReview.mockResolvedValue(undefined);

    mount();

    // Fill retention quantity and select unit
    await user.type(await screen.findByLabelText(/Retention quantity/i), "50");
    await user.click(screen.getByRole("combobox", { name: /Unit/i }));
    await user.click(screen.getByRole("option", { name: "g" }));

    // Click Submit Review
    const submitReviewBtn = await screen.findByRole("button", { name: /Submit Review/i });
    await user.click(submitReviewBtn);

    await sign(user);

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
    const user = userEvent.setup();
    const sample = createSample({ category: "FinishedProduct" });
    mockGetSummary.mockResolvedValue(sample);
    mockCompleteReview.mockResolvedValue(undefined);

    mount();

    const submitReviewBtn = await screen.findByRole("button", { name: /Submit Review/i });
    expect(screen.queryByLabelText(/Retention quantity/i)).toBeNull();
    await user.click(submitReviewBtn);

    await sign(user);

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
    const user = userEvent.setup();
    const sample = createSample({ category: "RawMaterial" });
    mockGetSummary.mockResolvedValue(sample);

    mount();

    const quantity = await screen.findByLabelText(/Retention quantity/i);
    await user.type(quantity, "0");
    expect(quantity).toHaveProperty("value", "0");
    await user.click(screen.getByRole("combobox", { name: /Unit/i }));
    await user.click(screen.getByRole("option", { name: "g" }));

    const submitReviewBtn = screen.getByRole("button", { name: /Submit Review/i });
    expect((submitReviewBtn as HTMLButtonElement).disabled).toBe(true);
    expect(mockCompleteReview).not.toHaveBeenCalled();
  }, 15000);

  it("disables review submission when retention quantity is negative", async () => {
    const user = userEvent.setup();
    const sample = createSample({ category: "RawMaterial" });
    mockGetSummary.mockResolvedValue(sample);

    mount();

    const quantity = await screen.findByLabelText(/Retention quantity/i);
    await user.type(quantity, "-5");
    expect(quantity).toHaveProperty("value", "-5");
    await user.click(screen.getByRole("combobox", { name: /Unit/i }));
    await user.click(screen.getByRole("option", { name: "g" }));

    const submitReviewBtn = screen.getByRole("button", { name: /Submit Review/i });
    expect((submitReviewBtn as HTMLButtonElement).disabled).toBe(true);
    expect(mockCompleteReview).not.toHaveBeenCalled();
  }, 15000);

  it("disables review submission when retention unit is empty", async () => {
    const user = userEvent.setup();
    const sample = createSample({ category: "RawMaterial" });
    mockGetSummary.mockResolvedValue(sample);

    mount();

    const quantity = await screen.findByLabelText(/Retention quantity/i);
    await user.type(quantity, "50");
    expect(quantity).toHaveProperty("value", "50");

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

    mount();

    const printBtn = await screen.findByRole("button", { name: "Print label" });
    await waitFor(() => {
      expect((printBtn as HTMLButtonElement).disabled).toBe(false);
    });

    await user.click(printBtn);

    const printDialog = await screen.findByRole("dialog", { name: "Print labels" });
    expect(await within(printDialog).findByText("RM0926077")).toBeTruthy();
  });
});

// R3 tests (OOS banner, retest availability, gated actions) render with the real AuthProvider
// and the permissions a reviewer/approver holds, read from localStorage like the rest of this file.
function mountAt(sampleId = 77) {
  localStorage.setItem("microlims_permissions", JSON.stringify([
    PERMISSIONS.SAMPLES_REVIEW,
    PERMISSIONS.SAMPLES_APPROVE,
    PERMISSIONS.SAMPLES_RECEIVE_OWN_LAB,
    PERMISSIONS.LABELS_REPRINT,
    PERMISSIONS.OOS_MANAGE
  ]));
  return render(dialogAt(sampleId));
}

function dialogAt(sampleId: number) {
  return (
    <MemoryRouter>
      <AuthProvider>
        <PinnedLightTheme>
          <SampleSummaryDialog open={true} sampleId={sampleId} onClose={vi.fn()} />
        </PinnedLightTheme>
      </AuthProvider>
    </MemoryRouter>
  );
}

describe("SampleSummaryDialog gated actions", () => {
  it("a reviewer who performed a test sees Complete review disabled with the server's sentence", async () => {
    const sentence = "You performed a test on this sample, so you cannot review it.";
    mockGetSummary.mockResolvedValue(createSample({ category: "FinishedProduct" }));
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      { action: "review.complete", allowed: false, reason: sentence }
    ]);
    const user = userEvent.setup();

    mountAt();

    const reason = await screen.findByText(sentence);
    const button = screen.getByRole("button", { name: /Submit Review/i });
    expect(button.getAttribute("aria-disabled")).toBe("true");
    expect(document.getElementById(button.getAttribute("aria-describedby") ?? "")?.contains(reason)).toBe(true);
    expect(vi.mocked(actionAvailabilityService.get)).toHaveBeenCalledWith("sample", 77, 10, expect.any(AbortSignal));

    await user.click(button);
    expect(screen.queryByLabelText(/^Password/i)).toBeNull();
    expect(mockCompleteReview).not.toHaveBeenCalled();
  }, 15000);

  it("an analyst sees Approve disabled with the role sentence", async () => {
    const sentence = "Your role does not include this action.";
    mockGetSummary.mockResolvedValue(
      createSample({
        status: "UnderApproval",
        sections: (createSample().sections ?? []).map((section) => ({ ...section, status: "UnderApproval" }))
      })
    );
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      { action: "approval.approve", allowed: false, reason: sentence },
      { action: "approval.reject", allowed: true, reason: null }
    ]);
    const user = userEvent.setup();

    mountAt();

    expect((await screen.findAllByText(sentence)).length).toBeGreaterThan(0);
    const button = screen.getByRole("button", { name: /Submit Decision/i });
    expect(button.getAttribute("aria-disabled")).toBe("true");
    await user.click(button);
    expect(screen.queryByLabelText(/^Password/i)).toBeNull();
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
        sections: (createSample().sections ?? []).map((section) => ({ ...section, status: "UnderApproval" }))
      })
    );
    mockDecideApproval.mockResolvedValue(undefined);
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      { action: "approval.approve", allowed: true, reason: null },
      { action: "approval.reject", allowed: true, reason: null }
    ]);
    const user = userEvent.setup();

    mountAt();

    await user.click(await screen.findByRole("button", { name: /Submit Decision/i }));
    await sign(user);
    await waitFor(() => expect(mockDecideApproval).toHaveBeenCalledTimes(1));
    expect(mockDecideApproval).toHaveBeenCalledWith(
      77, "secret123", "Approve", undefined, undefined, undefined, undefined, undefined, 10
    );
  }, 15000);

  it("blocks review and approval via action-availability for Sample.CannotReviewOverrode / Sample.CannotApproveOverrode and shows override time with timezone and ended text", async () => {
    const reviewRefusal = "You overrode competency for a test on this sample, so you cannot review it.";
    const approveRefusal = "You overrode competency for a test on this sample, so you cannot approve it.";
    const base = createSample({ category: "FinishedProduct" });
    const overriddenOrder = {
      testOrderId: 101,
      testCode: "TAMC",
      testDisplayName: "Total Aerobic Microbial Count",
      specificationText: "< 100 CFU/g",
      sectionName: "Physicochemical",
      status: "ResultEntered",
      currentStep: "ResultEntered",
      isSuperseded: false,
      outsourcedToLab: null,
      cancelledAtStep: null,
      cancelledAtStage: null,
      locations: [],
      incubations: [],
      results: [],
      countTestReadings: [],
      pathogenObservations: [],
      biochemicalResults: [],
      workflowHistory: [],
      analysis: null,
      competencyOverrides: [
        {
          analystName: "Amina Analyst",
          grantedByFullName: "Hana Head",
          grantedAtUtc: "2026-11-24T09:00:00Z",
          meaning: "Competency override",
          reason: "Supervised batch release",
          endedAtUtc: "2026-11-24T11:00:00Z"
        }
      ]
    };
    mockGetSummary.mockResolvedValue({
      ...base,
      testOrders: [overriddenOrder]
    });
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      {
        action: "review.complete",
        allowed: false,
        reasonCode: "Sample.CannotReviewOverrode",
        reason: reviewRefusal
      }
    ]);
    const user = userEvent.setup();

    mountAt();

    const reviewReason = await screen.findByText(reviewRefusal);
    const reviewButton = screen.getByRole("button", { name: /Submit Review/i });
    expect(reviewButton.getAttribute("aria-disabled")).toBe("true");
    expect(document.getElementById(reviewButton.getAttribute("aria-describedby") ?? "")?.contains(reviewReason)).toBe(true);
    expect(vi.mocked(actionAvailabilityService.get)).toHaveBeenCalledWith("sample", 77, 10, expect.any(AbortSignal));
    await user.click(reviewButton);
    expect(mockCompleteReview).not.toHaveBeenCalled();

    // TestResultsSection renders override time with timezone and ended text
    const { formatDateTimeWithTimeZone } = await import("../../utils/formatDate");
    const expectedGranted = formatDateTimeWithTimeZone("2026-11-24T09:00:00Z", { seconds: true });
    const expectedEnded = formatDateTimeWithTimeZone("2026-11-24T11:00:00Z", { seconds: true });
    expect(screen.getByText(expectedGranted)).toBeTruthy();
    expect(screen.getByText(/Ended/)).toBeTruthy();
    expect(screen.getByText(expectedEnded)).toBeTruthy();

    cleanup();
    mockGetSummary.mockResolvedValue({
      ...base,
      status: "UnderApproval",
      certificate: { sample: { matrix: null, simple: null }, sections: [] } as never,
      sections: (base.sections ?? []).map((section) => ({ ...section, status: "UnderApproval" })),
      testOrders: [
        {
          ...overriddenOrder,
          status: "Reviewed"
        }
      ]
    });
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      {
        action: "approval.approve",
        allowed: false,
        reasonCode: "Sample.CannotApproveOverrode",
        reason: approveRefusal
      },
      {
        action: "approval.reject",
        allowed: false,
        reasonCode: "Sample.CannotApproveOverrode",
        reason: approveRefusal
      }
    ]);

    mountAt();

    expect((await screen.findAllByText(approveRefusal)).length).toBeGreaterThan(0);
    const approveButton = screen.getByRole("button", { name: /Submit Decision/i });
    expect(approveButton.getAttribute("aria-disabled")).toBe("true");
    await user.click(approveButton);
    expect(mockDecideApproval).not.toHaveBeenCalled();
  }, 15000);
});

describe("SampleSummaryDialog in Arabic RTL", () => {
  afterEach(async () => {
    await resetLanguage();
  });

  it("renders RTL layout, Arabic heading, and sample identifier in LTR bdi", async () => {
    mockGetSummary.mockResolvedValue(createSample());
    await renderInArabic(dialog, ["common", "testingWorkspace"]);

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
      { action: "approval.reject", allowed: false, reason: "OOS investigation OOS-2026-0042 is open." },
      {
        action: "approval.retestRetainedSample",
        allowed: false,
        reasonCode: "Oos.RetestNeedsApprovedPlan",
        reason: "A retest needs an approved retest plan."
      },
      {
        action: "approval.newSampleRequest",
        allowed: false,
        reasonCode: "Oos.RetestNeedsApprovedPlan",
        reason: "A retest needs an approved retest plan."
      }
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

    mountAt();

    const link = await screen.findByRole("link", { name: /OOS-2026-0042/i });
    expect(link.getAttribute("href")).toBe("/oos-tracking/42");
    expect((await screen.findAllByText("OOS investigation OOS-2026-0042 is open.")).length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: /Submit Decision/i }).getAttribute("aria-disabled")).toBe("true");
    expect(screen.queryByRole("radio", { name: /Retest Retained Sample/i })).toBeNull();
    expect(screen.queryByRole("radio", { name: /New Sample Required/i })).toBeNull();
  }, 15000);

  it("never offers Investigation or OOS Investigation as approval decision options", async () => {
    const base = createSample();
    mockGetSummary.mockResolvedValue(
      createSample({
        status: "UnderApproval",
        certificate: { sample: { matrix: null, simple: null }, sections: [] } as never,
        sections: [{ ...base.sections![0], status: "UnderApproval" }]
      })
    );

    mountAt(77);

    expect(await screen.findByRole("radio", { name: /Approve/i })).toBeTruthy();
    expect(screen.getByRole("radio", { name: /Not Conform/i })).toBeTruthy();
    expect(screen.getByRole("radio", { name: /Retest Retained Sample/i })).toBeTruthy();
    expect(screen.getByRole("radio", { name: /New Sample Required/i })).toBeTruthy();
    expect(screen.queryByRole("radio", { name: /^Investigation$/i })).toBeNull();
    expect(screen.queryByRole("radio", { name: /OOS Investigation/i })).toBeNull();
  }, 15000);

  it("follows server availability for retest and new-sample options regardless of client investigation list", async () => {
    mockGetSummary.mockResolvedValue(
      createSample({
        status: "UnderApproval",
        certificate: { sample: { matrix: null, simple: null }, sections: [] } as never,
        sections: (createSample().sections ?? []).map((section) => ({ ...section, status: "UnderApproval" }))
      })
    );
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
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      { action: "approval.approve", allowed: true, reason: null },
      { action: "approval.reject", allowed: true, reason: null },
      { action: "approval.retestRetainedSample", allowed: true, reason: null },
      { action: "approval.newSampleRequest", allowed: true, reason: null }
    ]);

    const { rerender } = mountAt();

    expect(await screen.findByRole("radio", { name: /Retest Retained Sample/i })).toBeTruthy();
    expect(screen.getByRole("radio", { name: /New Sample Required/i })).toBeTruthy();

    vi.mocked(oosInvestigationService.getForSample).mockResolvedValue([]);
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      {
        action: "approval.approve",
        allowed: false,
        reasonCode: "Oos.InvestigationOpen",
        reasonParams: { number: "OOS-2026-0099" },
        reason: "OOS investigation OOS-2026-0099 is open."
      },
      {
        action: "approval.reject",
        allowed: false,
        reasonCode: "Oos.InvestigationOpen",
        reasonParams: { number: "OOS-2026-0099" },
        reason: "OOS investigation OOS-2026-0099 is open."
      },
      {
        action: "approval.retestRetainedSample",
        allowed: false,
        reasonCode: "Oos.RetestNeedsApprovedPlan",
        reason: "A retest needs an approved retest plan."
      },
      {
        action: "approval.newSampleRequest",
        allowed: false,
        reasonCode: "Oos.RetestNeedsApprovedPlan",
        reason: "A retest needs an approved retest plan."
      }
    ]);

    rerender(dialogAt(78));

    expect((await screen.findAllByText("OOS investigation OOS-2026-0099 is open.")).length).toBeGreaterThan(0);
    expect(screen.queryByRole("radio", { name: /Retest Retained Sample/i })).toBeNull();
    expect(screen.queryByRole("radio", { name: /New Sample Required/i })).toBeNull();
  }, 15000);

  it("resets an unavailable selected retest decision back to Approve when server availability changes", async () => {
    const user = userEvent.setup();
    const base = createSample();
    const sectionOne = base.sections![0];
    const sectionTwo = { ...sectionOne, sectionId: 20, sectionCode: "MB", sectionName: "Microbiology" };
    mockGetSummary.mockResolvedValue(
      createSample({
        status: "UnderApproval",
        certificate: { sample: { matrix: null, simple: null }, sections: [] } as never,
        sections: [
          { ...sectionOne, status: "UnderApproval" },
          { ...sectionTwo, status: "UnderApproval" }
        ]
      })
    );
    vi.mocked(oosInvestigationService.getForSample).mockResolvedValue([]);
    vi.mocked(actionAvailabilityService.get).mockImplementation(async (_kind, _id, sectionId) => {
      if (sectionId === 20) {
        return [
          {
            action: "approval.approve",
            allowed: false,
            reasonCode: "Oos.InvestigationOpen",
            reasonParams: { number: "OOS-2026-0100" },
            reason: "OOS investigation OOS-2026-0100 is open."
          },
          {
            action: "approval.retestRetainedSample",
            allowed: false,
            reasonCode: "Oos.RetestNeedsApprovedPlan",
            reason: "A retest needs an approved retest plan."
          },
          {
            action: "approval.newSampleRequest",
            allowed: false,
            reasonCode: "Oos.RetestNeedsApprovedPlan",
            reason: "A retest needs an approved retest plan."
          }
        ];
      }
      return [
        { action: "approval.approve", allowed: true, reason: null },
        { action: "approval.reject", allowed: true, reason: null },
        { action: "approval.retestRetainedSample", allowed: true, reason: null },
        { action: "approval.newSampleRequest", allowed: true, reason: null }
      ];
    });

    mountAt();

    const sectionInput = await screen.findByRole("combobox", { name: /Laboratory Section/i });
    await user.click(sectionInput);
    await user.click(await screen.findByRole("option", { name: "Physicochemical" }));

    const retestRadio = await screen.findByRole("radio", { name: /Retest Retained Sample/i });
    await user.click(retestRadio);
    expect((retestRadio as HTMLInputElement).checked).toBe(true);

    await user.click(screen.getByRole("combobox", { name: /Laboratory Section/i }));
    await user.click(await screen.findByRole("option", { name: "Microbiology" }));

    await waitFor(() => {
      expect(screen.queryByRole("radio", { name: /Retest Retained Sample/i })).toBeNull();
    });
    expect((screen.getByRole("radio", { name: /Approve/i }) as HTMLInputElement).checked).toBe(true);
    expect((await screen.findAllByText("OOS investigation OOS-2026-0100 is open.")).length).toBeGreaterThan(0);
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
        reason: "This sample has an out-of-specification result without an investigation.",
        reasonCode: "Oos.FailingResultWithoutInvestigation"
      }
    ]);
    vi.mocked(oosInvestigationService.getForSample).mockResolvedValue([]);
    vi.mocked(oosInvestigationService.openManually).mockResolvedValue({
      id: 55,
      number: "OOS-2026-0055",
      version: 1
    });

    mountAt();

    expect(await screen.findByRole("radio", { name: /Retest Retained Sample/i })).toBeTruthy();
    expect(screen.getByRole("radio", { name: /New Sample Required/i })).toBeTruthy();
    expect((await screen.findAllByText("This sample has an out-of-specification result without an investigation.")).length).toBeGreaterThan(0);

    await user.click(await screen.findByRole("button", { name: /Open investigation/i }));
    await waitFor(() =>
      expect(oosInvestigationService.openManually).toHaveBeenCalledWith(77, 10, 5)
    );
  }, 15000);

  it("hides Open investigation for other block codes and shows manual-open server refusal", async () => {
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
        reason: "This section cannot be approved because OOS was confirmed.",
        reasonCode: "Oos.ConfirmedCannotBeApproved"
      }
    ]);

    const { rerender } = mountAt();
    expect((await screen.findAllByText("This section cannot be approved because OOS was confirmed.")).length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: /Open investigation/i })).toBeNull();

    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      {
        action: "approval.approve",
        allowed: false,
        reason: "This sample has an out-of-specification result without an investigation.",
        reasonCode: "Oos.FailingResultWithoutInvestigation"
      }
    ]);
    vi.mocked(oosInvestigationService.openManually).mockRejectedValue({
      response: { data: { message: "Manual opening requires a reviewed failing section without an investigation." } }
    });

    rerender(dialogAt(78));
    await user.click(await screen.findByRole("button", { name: /Open investigation/i }));
    const refusal = await screen.findByText(
      "Manual opening requires a reviewed failing section without an investigation."
    );
    expect(refusal.closest('[role="alert"]')).toBeTruthy();
  }, 15000);

  it("passes an AbortSignal to getForSample and ignores stale responses when sampleId changes", async () => {
    let resolveFirst!: (val: never[]) => void;
    const firstPromise = new Promise<never[]>((resolve) => {
      resolveFirst = resolve;
    });
    vi.mocked(oosInvestigationService.getForSample)
      .mockReturnValueOnce(firstPromise as never)
      .mockResolvedValueOnce([]);
    mockGetSummary.mockResolvedValue(createSample({ status: "UnderApproval" }));

    const { rerender } = mountAt(77);
    expect(oosInvestigationService.getForSample).toHaveBeenCalledWith(77, expect.any(AbortSignal));
    const firstSignal = vi.mocked(oosInvestigationService.getForSample).mock.calls[0][1] as AbortSignal;
    expect(firstSignal.aborted).toBe(false);

    rerender(dialogAt(78));
    expect(firstSignal.aborted).toBe(true);
    expect(oosInvestigationService.getForSample).toHaveBeenCalledWith(78, expect.any(AbortSignal));

    resolveFirst([
      {
        id: 999,
        number: "OOS-STALE-001",
        kind: "Oos",
        status: "PhaseOne",
        sampleId: 77,
        sampleReferenceNumber: "RM0926077",
        sampleDisplayName: "Stale",
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
      } as never
    ]);

    await waitFor(() => expect(mockGetSummary).toHaveBeenCalledWith(78, expect.objectContaining({ signal: expect.any(AbortSignal) })));
    expect(screen.queryByText("OOS-STALE-001")).toBeNull();
  }, 15000);
});
