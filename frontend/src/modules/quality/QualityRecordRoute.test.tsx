import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn()
}));
vi.mock("../../services/apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../services/apiClient")>();
  return { ...actual, apiClient: http };
});

import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { QualityRecordPage } from "./QualityRecordRoute";
import { actionDetail, link, recordDetail } from "./testFixtures";
import { CLASSIFICATIONS, USERS, ok } from "./steps/stepTestUtils";
import type { QualityRecordDetailDto } from "../../services/qualityService";

function serve(detail: QualityRecordDetailDto) {
  http.get.mockImplementation((url: string) => {
    if (url === "/quality/records/1") return ok(detail);
    if (url === "/quality/1/links") return ok({ items: [link()], totalCount: 1, page: 1, pageSize: 100 });
    if (url === "/quality/1/comments") return ok([]);
    if (url === "/quality/1/extensions") return ok([]);
    if (url === "/users/directory") return ok(USERS);
    if (url === "/quality/config/classifications") return ok(CLASSIFICATIONS);
    return Promise.reject(new Error(`unexpected GET ${url}`));
  });
}

function setAuth(permissions: string[], userId: number) {
  localStorage.setItem("microlims_username", "qqa");
  localStorage.setItem("microlims_role", "Qa");
  localStorage.setItem("microlims_full_name", "Quinn QA");
  localStorage.setItem("microlims_user_id", String(userId));
  localStorage.setItem("microlims_permissions", JSON.stringify(permissions));
}

function renderRoute() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={["/quality/1"]}>
          <Routes>
            <Route path="/quality/:id" element={<QualityRecordPage />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const QA = ["Quality.View", "Quality.Raise", "Quality.Approve"];

describe("QualityRecordRoute", () => {
  beforeEach(() => {
    http.post.mockImplementation(() => ok({}));
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.clearAllMocks();
  });

  it("loads the deviation step forms into the actions slot and reloads the record after a step", async () => {
    const user = userEvent.setup();
    const raised = recordDetail({ status: "Raised", deviation: { ...(recordDetail().deviation as NonNullable<QualityRecordDetailDto["deviation"]>), status: "Raised" } });
    serve(raised);
    setAuth(QA, 9);
    renderRoute();

    const slot = await screen.findByRole("region", { name: "Actions for this record" });
    expect(within(slot).getByRole("heading", { name: "Assess the deviation" })).toBeTruthy();

    await user.click(within(slot).getByRole("button", { name: "Reject as not a deviation" }));
    await user.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: "Reject as not a deviation" }));
    await user.type(await screen.findByLabelText("Reason for rejecting"), "Seasonal variation");
    await user.type(screen.getByLabelText(/^Password/), "pw");
    const detailReadsBefore = http.get.mock.calls.filter((call) => call[0] === "/quality/records/1").length;
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      expect(http.post).toHaveBeenCalledWith(
        "/quality/deviations/1/reject",
        { reason: "Seasonal variation", password: "pw" },
        { headers: { "If-Match": '"3"' } }
      );
    });
    await waitFor(() =>
      expect(http.get.mock.calls.filter((call) => call[0] === "/quality/records/1").length).toBeGreaterThan(detailReadsBefore)
    );
  });

  it("loads the CAPA step forms for a CAPA record", async () => {
    serve(
      recordDetail({
        type: "Capa",
        number: "CAPA-2026-0003",
        status: "PlanApproved",
        deviation: null,
        capa: {
          status: "PlanApproved",
          sourceRecordId: null,
          sourceRecordNumber: null,
          effectivenessCriteria: "c",
          effectivenessMethod: "m",
          effectivenessPlannedDate: "2027-03-01",
          effectivenessResult: null,
          effectivenessFindings: null,
          successorCapaId: null,
          successorCapaNumber: null
        },
        actions: [actionDetail()]
      })
    );
    setAuth(QA, 9);
    renderRoute();

    const slot = await screen.findByRole("region", { name: "Actions for this record" });
    expect(within(slot).getByRole("heading", { name: "Completion verification" })).toBeTruthy();
  });

  it("loads the complaint step forms", async () => {
    serve(
      recordDetail({
        type: "Complaint",
        number: "CMP-2026-0005",
        status: "Received",
        deviation: null,
        complaint: {
          status: "Received",
          source: "Customer",
          receivedOn: "2026-11-18",
          externalReference: null,
          itemId: null,
          itemName: null,
          lotText: null,
          complaintText: "Discoloured",
          whatToTest: null,
          findings: null,
          outcome: null,
          conclusionSummary: null
        }
      })
    );
    setAuth(QA, 9);
    renderRoute();

    const slot = await screen.findByRole("region", { name: "Actions for this record" });
    expect(within(slot).getByRole("heading", { name: "Request laboratory investigation" })).toBeTruthy();
  });
});
