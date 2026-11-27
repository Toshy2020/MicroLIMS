import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { oosInvestigationService } from "../../../services/oosInvestigationService";
import { RecordDetailDialog } from "./RecordDetailDialog";
import type { ResultRecordItem } from "../types/reportingTypes";

vi.mock("../../../services/oosInvestigationService", () => ({
  oosInvestigationService: {
    openManualExcursion: vi.fn()
  }
}));

const sampleRecord: ResultRecordItem = {
  id: 501,
  sampleId: 77,
  testOrderId: 88,
  sourceTable: "CountTestReadings",
  sourceId: 12,
  round: 1,
  referenceNumber: "WT-2026-001",
  category: "Water",
  subjectName: "Purified Water Loop",
  subjectDetail: "Point 3",
  batchNumber: null,
  controlNumber: null,
  testCode: "TAMC",
  testDisplayName: "Total Aerobic Microbial Count",
  resultKind: "Quantitative",
  numericValue: 85,
  reportedValue: "85",
  unit: "CFU/mL",
  isBelowDetectionLimit: false,
  detectionLimit: null,
  alertLimit: "50",
  actionLimit: "100",
  specLimit: "100",
  resultLevel: "AlertLevel",
  resultEnteredAt: "2026-11-20T09:00:00Z",
  resultEnteredByUserId: 5,
  resultEnteredByName: "Analyst One",
  sampleStatus: "UnderApproval",
  approvalStatus: "Approved",
  approvedByUserId: 2,
  approvedByName: "Section Head",
  approvedAt: "2026-11-20T12:00:00Z"
};

beforeEach(() => {
  localStorage.clear();
  vi.mocked(oosInvestigationService.openManualExcursion).mockReset();
});

afterEach(() => {
  cleanup();
});

describe("RecordDetailDialog excursion review", () => {
  it("requires a reason and opens an excursion review for the chart point testOrderId", async () => {
    const user = userEvent.setup();
    vi.mocked(oosInvestigationService.openManualExcursion).mockResolvedValue({
      id: 99,
      number: "EXC-2026-0004",
      version: 1
    });

    localStorage.setItem("microlims_permissions", JSON.stringify([PERMISSIONS.OOS_CONTRIBUTE]));
    render(
      <AuthProvider>
        <PinnedLightTheme>
          <MemoryRouter>
            <RecordDetailDialog open record={sampleRecord} onClose={vi.fn()} />
          </MemoryRouter>
        </PinnedLightTheme>
      </AuthProvider>
    );

    await user.click(screen.getByRole("button", { name: /Open excursion review/i }));

    const submitBtn = screen.getByRole("button", { name: /Confirm Excursion Review/i });
    expect(submitBtn.hasAttribute("disabled")).toBe(true);

    await user.type(screen.getByLabelText(/Reason/i), "Upward trend above alert limit");
    expect(submitBtn.hasAttribute("disabled")).toBe(false);

    await user.click(submitBtn);

    await waitFor(() =>
      expect(oosInvestigationService.openManualExcursion).toHaveBeenCalledWith(
        88,
        "Upward trend above alert limit"
      )
    );
  });

  it("shows server refusal when opening excursion review fails", async () => {
    const user = userEvent.setup();
    vi.mocked(oosInvestigationService.openManualExcursion).mockRejectedValue({
      response: { data: { message: "An excursion review is already open for this test order." } }
    });

    localStorage.setItem("microlims_permissions", JSON.stringify([PERMISSIONS.OOS_CONTRIBUTE]));
    render(
      <AuthProvider>
        <PinnedLightTheme>
          <MemoryRouter>
            <RecordDetailDialog open record={sampleRecord} onClose={vi.fn()} />
          </MemoryRouter>
        </PinnedLightTheme>
      </AuthProvider>
    );

    await user.click(screen.getByRole("button", { name: /Open excursion review/i }));
    await user.type(screen.getByLabelText(/Reason/i), "Trend shift");
    await user.click(screen.getByRole("button", { name: /Confirm Excursion Review/i }));

    await waitFor(() =>
      expect(
        screen.getByText("An excursion review is already open for this test order.")
      ).toBeTruthy()
    );
  });

  it("does not show Open excursion review without an auth context or OOS permission", () => {
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <RecordDetailDialog open record={sampleRecord} onClose={vi.fn()} />
        </MemoryRouter>
      </PinnedLightTheme>
    );
    expect(screen.queryByRole("button", { name: /Open excursion review/i })).toBeNull();

    cleanup();
    localStorage.setItem("microlims_permissions", JSON.stringify([]));
    render(
      <AuthProvider>
        <PinnedLightTheme>
          <MemoryRouter>
            <RecordDetailDialog open record={sampleRecord} onClose={vi.fn()} />
          </MemoryRouter>
        </PinnedLightTheme>
      </AuthProvider>
    );
    expect(screen.queryByRole("button", { name: /Open excursion review/i })).toBeNull();
  });

  it.each(["Approved", "Rejected", "Cancelled", "Voided"])(
    "gates the excursion review with a stated reason when the sample is %s",
    (status) => {
      localStorage.setItem("microlims_permissions", JSON.stringify([PERMISSIONS.OOS_CONTRIBUTE]));
      render(
        <AuthProvider>
          <PinnedLightTheme>
            <MemoryRouter>
              <RecordDetailDialog
                open
                record={{ ...sampleRecord, sampleStatus: status as ResultRecordItem["sampleStatus"] }}
                onClose={vi.fn()}
              />
            </MemoryRouter>
          </PinnedLightTheme>
        </AuthProvider>
      );

      const button = screen.getByRole("button", { name: /Open excursion review/i });
      expect(button.hasAttribute("disabled")).toBe(true);
      expect(screen.getByText(/already decided or cancelled/i)).toBeTruthy();
      expect(screen.queryByRole("button", { name: /Confirm Excursion Review/i })).toBeNull();
      expect(oosInvestigationService.openManualExcursion).not.toHaveBeenCalled();
    }
  );
});
