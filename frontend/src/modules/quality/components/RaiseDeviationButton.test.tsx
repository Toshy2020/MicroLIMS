import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { RaiseDeviationButton } from "./RaiseDeviationButton";
import { qualityEntryService, type DeviationDto } from "../../../services/qualityEntryService";

vi.mock("../../../services/qualityEntryService", () => ({
  qualityEntryService: {
    forTarget: vi.fn(),
    raiseDeviation: vi.fn(),
    raiseFromOosRequest: vi.fn(),
    escalateMonitoringDeviation: vi.fn(),
    raiseFromAuditFinding: vi.fn()
  }
}));

function seedAuth(permissions: string[], userId = 10) {
  const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "Analyst", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
  localStorage.setItem("microlims_full_name", "Alex Analyst");
  localStorage.setItem("microlims_username", "alex");
  localStorage.setItem("microlims_user_id", String(userId));
}

const createdDeviation: DeviationDto = {
  id: 91,
  version: 1,
  record: {
    id: 91,
    number: "DEV-2026-0091",
    type: "Deviation",
    title: "Broken seal on sample",
    description: "Seal was torn at receipt",
    sectionId: 3,
    raisedByUserId: 10,
    raisedAtUtc: "2026-11-26T09:00:00Z",
    ownerUserId: null,
    version: 1,
    attachments: []
  },
  status: "Raised",
  occurredAtUtc: "2026-11-26T08:30:00Z",
  location: null,
  immediateAction: "Quarantined sample",
  classificationId: null,
  dueDate: null,
  impactAssessment: null,
  rootCauseCategoryId: null,
  rootCauseDescription: null,
  proposedActions: null,
  capaRequired: null,
  rejectionReason: null,
  cancelReason: null
};

describe("RaiseDeviationButton", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("hides the button when the user lacks Quality.Raise", () => {
    seedAuth([PERMISSIONS.QUALITY_VIEW]);
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter>
            <RaiseDeviationButton targetType="Sample" targetId={42} sectionId={3} />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    expect(screen.queryByRole("button", { name: /raise deviation/i })).toBeNull();
  });

  it("raising from a sample pre-links the sample and sends the exact payload", async () => {
    seedAuth([PERMISSIONS.QUALITY_VIEW, PERMISSIONS.QUALITY_RAISE]);
    vi.mocked(qualityEntryService.raiseDeviation).mockResolvedValue(createdDeviation);
    const onRaised = vi.fn();
    const user = userEvent.setup();

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter>
            <RaiseDeviationButton targetType="Sample" targetId={42} sectionId={3} onRaised={onRaised} />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    await user.click(screen.getByRole("button", { name: /raise deviation/i }));

    await user.type(screen.getByLabelText(/title/i), "Broken seal on sample");
    await user.type(screen.getByLabelText(/description/i), "Seal was torn at receipt");
    await user.type(screen.getByLabelText(/immediate action/i), "Quarantined sample");

    await user.click(screen.getByRole("button", { name: /^raise deviation$/i }));

    await waitFor(() => expect(qualityEntryService.raiseDeviation).toHaveBeenCalledTimes(1));
    expect(qualityEntryService.raiseDeviation).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "Broken seal on sample",
        description: "Seal was torn at receipt",
        immediateAction: "Quarantined sample",
        sectionId: 3,
        links: [{ targetType: "Sample", targetId: 42 }]
      })
    );
    expect(onRaised).toHaveBeenCalledWith(createdDeviation);
  });

  it("opens a pre-filled deviation for an OOS deviation request and sends If-Match version", async () => {
    seedAuth([PERMISSIONS.QUALITY_VIEW, PERMISSIONS.QUALITY_APPROVE]);
    vi.mocked(qualityEntryService.raiseFromOosRequest).mockResolvedValue(createdDeviation);
    const onRaised = vi.fn();
    const user = userEvent.setup();

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter>
            <RaiseDeviationButton
              sourceKind="oosRequest"
              sourceId={15}
              sourceVersion={4}
              initialTitle="OOS-2026-0015 deviation"
              initialDescription="Confirmed OOS result requires deviation"
              onRaised={onRaised}
            />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    await user.click(screen.getByRole("button", { name: /raise deviation/i }));

    const titleInput = screen.getByLabelText(/title/i) as HTMLInputElement;
    expect(titleInput.value).toBe("OOS-2026-0015 deviation");
    expect(screen.getByText("Confirmed OOS result requires deviation")).toBeTruthy();

    await user.type(screen.getByLabelText(/immediate action/i), "Lot placed on hold");
    await user.click(screen.getByRole("button", { name: /^raise deviation$/i }));

    await waitFor(() => expect(qualityEntryService.raiseFromOosRequest).toHaveBeenCalledTimes(1));
    expect(qualityEntryService.raiseFromOosRequest).toHaveBeenCalledWith(
      15,
      expect.objectContaining({
        title: "OOS-2026-0015 deviation",
        immediateAction: "Lot placed on hold"
      }),
      4
    );
    expect(onRaised).toHaveBeenCalledWith(createdDeviation);
  });

  it("escalates a monitoring deviation using from-monitoring-deviation with version", async () => {
    seedAuth([PERMISSIONS.QUALITY_VIEW, PERMISSIONS.QUALITY_INVESTIGATE]);
    vi.mocked(qualityEntryService.escalateMonitoringDeviation).mockResolvedValue(createdDeviation);
    const user = userEvent.setup();

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter>
            <RaiseDeviationButton
              sourceKind="monitoringDeviation"
              sourceId={77}
              sourceVersion={9}
              initialTitle="MON-2026-0077"
              initialDescription="Action limit exceeded in Cleanroom A"
            />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    await user.click(screen.getByRole("button", { name: /raise deviation/i }));
    await user.type(screen.getByLabelText(/immediate action/i), "Re-sanitized room");
    await user.click(screen.getByRole("button", { name: /^raise deviation$/i }));

    await waitFor(() => expect(qualityEntryService.escalateMonitoringDeviation).toHaveBeenCalledTimes(1));
    expect(qualityEntryService.escalateMonitoringDeviation).toHaveBeenCalledWith(
      77,
      expect.objectContaining({
        title: "MON-2026-0077",
        immediateAction: "Re-sanitized room"
      }),
      9
    );
  });
});
