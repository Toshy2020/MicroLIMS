import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { DashboardLabProvider, makeDashboardLab } from "./DashboardLabContext";
import { DashboardService } from "./services/DashboardService";
import type { ReviewerDashboard } from "./types/dashboard";
import { ReviewerDashboardPage } from "./ReviewerDashboardPage";

// Network boundary only.
vi.mock("./services/DashboardService", () => ({
  DashboardService: { getReviewerDashboard: vi.fn() }
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("reviewer dashboard shows time zones", () => {
  it("shows the date, time and zone of a recent review", async () => {
    vi.mocked(DashboardService.getReviewerDashboard).mockResolvedValue({
      pendingReviewCount: 0,
      overdueReviewCount: 0,
      dueTodayCount: 0,
      retestsInProgressCount: 0,
      completedTodayCount: 1,
      reviewQueue: [],
      attentionItems: [],
      recentlyReviewed: [
        {
          sampleId: 5,
          referenceNumber: "FP1126005",
          subjectName: "Product",
          testCode: "TAMC",
          status: "Approved",
          reviewedAt: "2026-11-17T09:41:07Z"
        }
      ]
    } as unknown as ReviewerDashboard);
    render(
      <PinnedLightTheme>
        <MemoryRouter initialEntries={["/dashboard"]}>
          <DashboardLabProvider value={makeDashboardLab("MICRO", [])}>
            <ReviewerDashboardPage />
          </DashboardLabProvider>
        </MemoryRouter>
      </PinnedLightTheme>
    );
    const reviewed = await screen.findByText(/^17 Nov 2026, \d{2}:\d{2} [A-Za-z][A-Za-z0-9+:-]*$/);
    expect(reviewed).toBeTruthy();
  });
});
