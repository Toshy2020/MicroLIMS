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

const mockGetSummary = vi.fn();
const mockCompleteReview = vi.fn();
const mockDecideApproval = vi.fn();

// Network boundary: the server's answer about each gated action.
vi.mock("../../services/actionAvailabilityService", () => ({
  actionAvailabilityService: { get: vi.fn() }
}));

vi.mock("./services/SampleSummaryService", () => ({
  SampleSummaryService: {
    getSummary: (...args: unknown[]) => mockGetSummary(...args),
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
  mockCompleteReview.mockReset();
  mockDecideApproval.mockReset();
  vi.mocked(actionAvailabilityService.get).mockReset();
  vi.mocked(actionAvailabilityService.get).mockResolvedValue([]);
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

describe("SampleSummaryDialog gated actions", () => {
  it("a reviewer who performed a test sees Complete review disabled with the server's sentence", async () => {
    const sentence = "You performed a test on this sample, so you cannot review it.";
    mockGetSummary.mockResolvedValue(createSample({ category: "FinishedProduct" }));
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      { action: "review.complete", allowed: false, reason: sentence }
    ]);
    const user = userEvent.setup();
    mount();

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
    mount();

    expect(await screen.findByText(sentence)).toBeTruthy();
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
    mount();

    await user.click(await screen.findByRole("button", { name: /Submit Decision/i }));
    await sign(user);
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
    await renderInArabic(dialog, ["common", "testingWorkspace"]);

    expect(document.documentElement.dir).toBe("rtl");
    expect(await screen.findByText("ملخص العينة")).toBeTruthy();
    const refNodes = await screen.findAllByText("RM0926077");
    expect(refNodes.some((node) => node.closest("bdi")?.getAttribute("dir") === "ltr")).toBe(true);
  }, 15000);
});
