import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { QualityRecordsPanel } from "./QualityRecordsPanel";
import { qualityEntryService } from "../../../services/qualityEntryService";

vi.mock("../../../services/qualityEntryService", () => ({
  qualityEntryService: {
    forTarget: vi.fn(),
    raiseDeviation: vi.fn(),
    raiseFromOosRequest: vi.fn(),
    escalateMonitoringDeviation: vi.fn(),
    raiseFromAuditFinding: vi.fn()
  }
}));

function seedAuth(permissions: string[]) {
  const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "Analyst", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
  localStorage.setItem("microlims_full_name", "Alex Analyst");
  localStorage.setItem("microlims_username", "alex");
  localStorage.setItem("microlims_user_id", "10");
}

describe("QualityRecordsPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("renders linked records with links to /quality/:id and shows restricted records by number only", async () => {
    seedAuth([PERMISSIONS.QUALITY_VIEW]);
    vi.mocked(qualityEntryService.forTarget).mockResolvedValue([
      { id: 12, number: "DEV-2026-0012", type: "Deviation", title: "Broken seal" },
      { id: 19, number: "CAPA-2026-0019", type: "Capa", title: "", isRestricted: true }
    ]);

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter>
            <QualityRecordsPanel targetType="Sample" targetId={42} />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    const devLink = await screen.findByRole("link", { name: /DEV-2026-0012/ });
    expect(devLink.getAttribute("href")).toBe("/quality/12");
    expect(screen.getByText("Broken seal")).toBeTruthy();

    const restrictedLink = screen.getByRole("link", { name: /CAPA-2026-0019/ });
    expect(restrictedLink.getAttribute("href")).toBe("/quality/19");
    expect(screen.getByText(/restricted/i)).toBeTruthy();
    expect(qualityEntryService.forTarget).toHaveBeenCalledWith("Sample", 42, 0, 25, expect.any(AbortSignal));
  });

  it("shows empty state when no records are linked", async () => {
    seedAuth([PERMISSIONS.QUALITY_VIEW]);
    vi.mocked(qualityEntryService.forTarget).mockResolvedValue([]);

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter>
            <QualityRecordsPanel targetType="Equipment" targetId={7} />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    expect(await screen.findByText(/no quality records/i)).toBeTruthy();
  });

  it("shows error state and retries when forTarget fails", async () => {
    seedAuth([PERMISSIONS.QUALITY_VIEW]);
    vi.mocked(qualityEntryService.forTarget)
      .mockRejectedValueOnce(new Error("Network error"))
      .mockResolvedValueOnce([{ id: 5, number: "DEV-2026-0005", type: "Deviation", title: "Recovered" }]);
    const user = userEvent.setup();

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter>
            <QualityRecordsPanel targetType="TestOrder" targetId={99} />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    expect(await screen.findByRole("alert")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: /retry/i }));
    expect(await screen.findByText("Recovered")).toBeTruthy();
  });
});
