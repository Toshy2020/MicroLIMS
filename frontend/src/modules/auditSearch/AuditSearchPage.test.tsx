import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuditSearchPage } from "./AuditSearchPage";
import { AuditSearchService } from "./services/AuditSearchService";
import { buildAuditPayload, INITIAL_AUDIT_FILTERS } from "./utils/auditPayload";
import type { AuditLogItem } from "./types/auditTypes";

const auth = vi.hoisted(() => ({ current: { permissions: [] as string[] } }));

vi.mock("../../contexts/AuthContext", () => ({ useAuth: () => auth.current }));

vi.mock("./services/AuditSearchService", () => ({
  AuditSearchService: {
    searchPage: vi.fn(),
    exportCsv: vi.fn(),
    getSecurityEvents: vi.fn().mockResolvedValue({ items: [], nextCursor: null, hasMore: false }),
    getLoginHistory: vi.fn().mockResolvedValue({ items: [], nextCursor: null, hasMore: false }),
    getForEntity: vi.fn().mockResolvedValue([]),
    getTraceability: vi.fn(),
    getTraceabilityForEntity: vi.fn()
  }
}));

const item = (id: number, entityId = "5", extra: Partial<AuditLogItem> = {}): AuditLogItem => ({
  id,
  entityName: "Sample",
  entityId,
  action: "Create",
  previousValue: null,
  newValue: "{}",
  userId: 1,
  userName: "Ahmed",
  userCurrentRole: null,
  userUsername: null,
  timestamp: "2026-10-01T10:00:00Z",
  batchNumber: null,
  controlNumber: null,
  sampleReferenceNumber: null,
  mediaLotNumber: null,
  referenceStrainCode: null,
  cryovialCode: null,
  sampleId: null,
  testOrderId: null,
  actionCategory: null,
  actorType: null,
  correlationId: null,
  reason: null,
  entityIdNegative: false,
  correctedByAuditLogId: null,
  ...extra
});

function renderPage() {
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <AuditSearchPage />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

beforeEach(() => {
  auth.current = { permissions: ["Audit.View"] };
  vi.mocked(AuditSearchService.searchPage).mockReset();
  vi.mocked(AuditSearchService.exportCsv).mockReset().mockResolvedValue();
});
afterEach(cleanup);

describe("buildAuditPayload", () => {
  it("sends only the filters that are set, including the new ones", () => {
    const payload = buildAuditPayload(
      { ...INITIAL_AUDIT_FILTERS, actionCategory: "Security", actorType: "System", correlationId: " abc ", reason: "reset", negativeEntityId: true },
      50
    );
    expect(payload).toEqual({
      Take: 50,
      ActionCategory: "Security",
      ActorType: "System",
      CorrelationId: "abc",
      Reason: "reset",
      NegativeEntityId: true
    });
  });

  it("omits Take for an export and sends nothing for an empty filter", () => {
    expect(buildAuditPayload(INITIAL_AUDIT_FILTERS)).toEqual({});
  });
});

describe("AuditSearchPage", () => {
  it("pages with the server cursor and does not request a page past the end", async () => {
    vi.mocked(AuditSearchService.searchPage)
      .mockResolvedValueOnce({ items: [item(30, "30")], nextCursor: 30, hasMore: true })
      .mockResolvedValueOnce({ items: [item(20, "20")], nextCursor: null, hasMore: false });
    renderPage();

    await screen.findByText("ID: 30");
    expect(vi.mocked(AuditSearchService.searchPage).mock.calls[0][0]).toMatchObject({ Take: 25 });
    expect(vi.mocked(AuditSearchService.searchPage).mock.calls[0][0]).not.toHaveProperty("BeforeId");

    fireEvent.click(screen.getByRole("button", { name: /next page/i }));
    await screen.findByText("ID: 20");
    expect(vi.mocked(AuditSearchService.searchPage).mock.calls[1][0]).toMatchObject({ Take: 25, BeforeId: 30 });
    expect((screen.getByRole("button", { name: /next page/i }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("flags rows with a negative record ID and shows when they were corrected", async () => {
    vi.mocked(AuditSearchService.searchPage).mockResolvedValue({
      items: [
        item(2, "-2147482647", { entityIdNegative: true }),
        item(3, "-2147482000", { entityIdNegative: true, correctedByAuditLogId: 99 })
      ],
      nextCursor: null,
      hasMore: false
    });
    renderPage();

    expect(await screen.findByText("Needs correction")).toBeTruthy();
    expect(screen.getByText("Corrected (#99)")).toBeTruthy();
  });

  it("exports with the applied filter, not the page cursor", async () => {
    vi.mocked(AuditSearchService.searchPage).mockResolvedValue({ items: [item(1)], nextCursor: null, hasMore: false });
    renderPage();
    await screen.findByText("ID: 5");

    fireEvent.click(screen.getByRole("button", { name: /export csv/i }));
    await waitFor(() => expect(AuditSearchService.exportCsv).toHaveBeenCalledTimes(1));
    const payload = vi.mocked(AuditSearchService.exportCsv).mock.calls[0][0];
    expect(payload).not.toHaveProperty("BeforeId");
    expect(payload).not.toHaveProperty("Take");
  });

  it("hides the Security Events and Login History tabs without System.ViewSecurityAudit", async () => {
    vi.mocked(AuditSearchService.searchPage).mockResolvedValue({ items: [], nextCursor: null, hasMore: false });
    renderPage();
    await screen.findByText("No audit events found");
    expect(screen.queryByRole("tab", { name: "Security Events" })).toBeNull();
    expect(screen.queryByRole("tab", { name: "Login History" })).toBeNull();
  });

  it("shows the tabs to holders of System.ViewSecurityAudit and loads the security trail", async () => {
    auth.current = { permissions: ["Audit.View", "System.ViewSecurityAudit"] };
    vi.mocked(AuditSearchService.searchPage).mockResolvedValue({ items: [], nextCursor: null, hasMore: false });
    renderPage();

    fireEvent.click(await screen.findByRole("tab", { name: "Security Events" }));
    await waitFor(() => expect(AuditSearchService.getSecurityEvents).toHaveBeenCalled());
    fireEvent.click(screen.getByRole("tab", { name: "Login History" }));
    await waitFor(() => expect(AuditSearchService.getLoginHistory).toHaveBeenCalled());
  });
});
