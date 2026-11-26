import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, expect, it, vi } from "vitest";
import { AuthProvider } from "../../contexts/AuthContext";
import { LiveProvider } from "../../contexts/LiveContext";
import { createFakeLiveConnection } from "../../contexts/testing/fakeLiveConnection";
import { PERMISSIONS } from "../../routes/routes";
import { signInForTest } from "../../services/drafts/draftTestUtils";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { SampleSummaryDialog } from "./SampleSummaryDialog";
import { SampleSummaryService } from "./services/SampleSummaryService";
import type { SampleSummary } from "./types/sampleSummaryTypes";

vi.mock("./services/SampleSummaryService", () => ({
  SampleSummaryService: { getSummary: vi.fn() }
}));
vi.mock("../../services/actionAvailabilityService", () => ({
  actionAvailabilityService: { get: vi.fn().mockResolvedValue([]) }
}));
vi.mock("../../services/labelService", () => ({
  labelService: { getStatus: vi.fn().mockResolvedValue([]) }
}));

afterEach(() => {
  cleanup();
  localStorage.clear();
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
  certificate: { sample: { matrix: null, simple: null, supportingRows: [], complies: false, conclusionText: "", resultDate: null }, sections: [] },
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

it("keeps review edits until the reviewer confirms reloading the sample", async () => {
  const user = userEvent.setup();
  const connection = createFakeLiveConnection();
  signInForTest(7);
  localStorage.setItem("microlims_permissions", JSON.stringify([PERMISSIONS.SAMPLES_REVIEW]));
  vi.mocked(SampleSummaryService.getSummary)
    .mockResolvedValueOnce(createSample())
    .mockResolvedValue(createSample({ referenceNumber: "RM0926077-UPDATED" }));

  render(
    <PinnedLightTheme>
      <AuthProvider>
        <LiveProvider connection={connection}>
          <MemoryRouter>
            <SampleSummaryDialog open sampleId={77} onClose={vi.fn()} />
          </MemoryRouter>
        </LiveProvider>
      </AuthProvider>
    </PinnedLightTheme>
  );

  const comment = await screen.findByRole<HTMLTextAreaElement>("textbox", { name: "Comment (optional)" });
  await user.type(comment, "My review comment");
  await waitFor(() => expect(connection.invoke).toHaveBeenCalledWith("WatchRecord", "sample", 77));

  act(() => connection.emitSignal({ kind: "record", key: "sample:77", actorUserId: 99 }));
  await user.click(await screen.findByRole("button", { name: "Reload" }));
  await user.click(await screen.findByRole("button", { name: "Cancel" }));
  expect(comment.value).toBe("My review comment");
  expect(SampleSummaryService.getSummary).toHaveBeenCalledTimes(1);

  await user.click(await screen.findByRole("button", { name: "Reload" }));
  await user.click(await screen.findByRole("button", { name: "Discard and reload" }));
  expect(await screen.findAllByText("RM0926077-UPDATED")).not.toHaveLength(0);
  expect((await screen.findByRole<HTMLTextAreaElement>("textbox", { name: "Comment (optional)" })).value).toBe("");
});
