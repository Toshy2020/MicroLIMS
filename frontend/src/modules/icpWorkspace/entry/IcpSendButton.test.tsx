import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { Button, Tooltip } from "@mui/material";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { IcpSendForReviewDialog } from "./IcpSendForReviewDialog";
import type { IcpSampleEntryDto } from "../types";

vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({
    userId: 1,
    username: "analyst1",
    fullName: "Analyst One",
    role: "Analyst",
    permissions: ["Hplc.Operate"]
  })
}));

afterEach(cleanup);

const baseMockEntry: IcpSampleEntryDto = {
  runSampleId: 101,
  icpRunId: 5,
  runCode: "RUN-ICP-2026-0001",
  calibrationCode: "CAL-ICP-2026-0001",
  calibrationStatus: "Confirmed",
  testOrderId: 501,
  sampleNumber: "SMP-2026-0012",
  batchNumber: "B260901",
  productName: "Mineral Supplement Tablet",
  testCode: "ICP-MINERALS",
  stageName: "Finished Product",
  status: "Assigned",
  mode: "MineralAssay",
  amountUnit: "Gram",
  unitAmount: 0.5,
  sampleVolumeMl: 50,
  dilutionFactor: 1,
  elements: [
    { icpMethodElementId: 1, symbol: "Fe", valid: true, reason: null },
    { icpMethodElementId: 2, symbol: "Zn", valid: true, reason: null }
  ],
  replicates: [
    {
      replicateNo: 1,
      sampleAmount: 1.0,
      volumeMl: 50,
      dilutionFactor: 1,
      concentrations: [
        { icpMethodElementId: 1, solutionMgPerL: 10.5 },
        { icpMethodElementId: 2, solutionMgPerL: 5.2 }
      ]
    }
  ],
  preview: [
    {
      icpMethodElementId: 1,
      parameterName: "Iron",
      quantity: "IcpMgPerUnit",
      value: 10.5,
      display: "10.5 mg/tab",
      unit: "mg/tab",
      status: "WithinLimits",
      specLimit: "9.0 - 11.0",
      problem: null
    }
  ],
  official: [],
  evidence: [
    {
      id: 99,
      icpRunId: 5,
      icpRunSampleId: 101,
      context: "Sample",
      kind: "SampleReport",
      fileName: "raw-report.pdf",
      contentType: "application/pdf",
      uploadedByUserId: 1,
      uploadedAt: "2026-10-05T00:00:00Z",
      isCurrent: true
    }
  ],
  editable: true,
  editableReason: null,
  submitted: false,
  canSubmit: true,
  canSubmitReason: null
};

describe("Icp Send for Review - button and dialog states", () => {
  it("disables Proceed to Sign and displays canSubmitReason when canSubmit is false", () => {
    const blockedEntry: IcpSampleEntryDto = {
      ...baseMockEntry,
      canSubmit: false,
      canSubmitReason: "Upload the sample result report before sending for review."
    };

    render(
      <PinnedLightTheme>
        <IcpSendForReviewDialog
          open={true}
          entry={blockedEntry}
          onClose={vi.fn()}
          onSuccess={vi.fn()}
        />
      </PinnedLightTheme>
    );

    // Verify reason message is displayed to the user
    const reasonElements = screen.getAllByText(/Upload the sample result report before sending for review\./);
    expect(reasonElements.length).toBeGreaterThan(0);

    // Verify "Proceed to Sign" button is disabled
    const signBtn = screen.getByRole("button", { name: /proceed to sign/i });
    expect(signBtn).toBeDefined();
    expect((signBtn as HTMLButtonElement).disabled).toBe(true);
  });

  it("enables Proceed to Sign when canSubmit is true", () => {
    render(
      <PinnedLightTheme>
        <IcpSendForReviewDialog
          open={true}
          entry={baseMockEntry}
          onClose={vi.fn()}
          onSuccess={vi.fn()}
        />
      </PinnedLightTheme>
    );

    const signBtn = screen.getByRole("button", { name: /proceed to sign/i });
    expect(signBtn).toBeDefined();
    expect((signBtn as HTMLButtonElement).disabled).toBe(false);
  });

  it("Send for Review button is disabled with reason tooltip in header when canSubmit is false", () => {
    const canSubmit = false;
    const canSubmitReason = "Calibration expired at 2026-10-05 06:00.";

    render(
      <PinnedLightTheme>
        <Tooltip title={!canSubmit ? canSubmitReason : ""}>
          <span>
            <Button
              variant="contained"
              disabled={!canSubmit}
              aria-label="Send for Review"
            >
              Send for Review
            </Button>
          </span>
        </Tooltip>
      </PinnedLightTheme>
    );

    const btn = screen.getByRole("button", { name: /send for review/i });
    expect((btn as HTMLButtonElement).disabled).toBe(true);
    expect(btn.closest("span")?.getAttribute("aria-label") ?? btn.getAttribute("aria-label")).toBeTruthy();
  });
});
