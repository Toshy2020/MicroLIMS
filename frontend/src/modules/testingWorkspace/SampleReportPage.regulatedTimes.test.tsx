import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { SampleReportPage } from "./SampleReportPage";
import { dt, d } from "./reportPrimitives";

const mockGetSummary = vi.fn();
const mockGetArchived = vi.fn();

vi.mock("./services/SampleSummaryService", () => ({
  SampleSummaryService: { getSummary: (...args: unknown[]) => mockGetSummary(...args) }
}));
vi.mock("./services/ArchivedRecordsService", () => ({
  ArchivedRecordsService: { getForEntity: (...args: unknown[]) => mockGetArchived(...args) }
}));

const summary = {
  sampleId: 1,
  referenceNumber: "FP1126005",
  category: "FinishedProduct",
  displayName: "Product",
  status: "Approved",
  receivedAt: "2026-10-26T10:00:07Z",
  mfgDate: "2026-01-01T00:00:00Z",
  expDate: "2028-10-01T00:00:00Z",
  reviewedByName: "Rana Reviewer",
  reviewedAt: "2026-11-17T09:41:07Z",
  approvedByName: "Sami Head",
  approvedAt: "2026-11-17T10:41:07Z",
  signatures: [
    {
      role: "Reviewer",
      printedName: "Rana Reviewer",
      username: "rana",
      meaning: "Reviewed",
      meaningText: "I reviewed this record",
      signedAt: "2026-11-17T09:41:07Z",
      comment: null
    }
  ],
  testOrders: [],
  timeline: [],
  certificate: { sample: { matrix: null, simple: null } }
};

// A time of day, then a zone name or offset after it (for example "09:41 UTC" or "12:41 GMT+3").
const WITH_ZONE = /\d{2}:\d{2} [A-Za-z][A-Za-z0-9+:-]*$/;

function renderReport() {
  render(
    <MemoryRouter initialEntries={["/samples/1/report"]}>
      <Routes>
        <Route path="/samples/:id/report" element={<SampleReportPage />} />
      </Routes>
    </MemoryRouter>
  );
}

beforeEach(() => {
  mockGetSummary.mockResolvedValue(summary);
  mockGetArchived.mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
  vi.unstubAllEnvs();
  vi.clearAllMocks();
});

describe("printed sample report shows time zones on regulated times", () => {
  it("shows the zone beside the received time and the signature time", async () => {
    renderReport();
    await screen.findAllByText("FP1126005");
    expect(document.querySelector(".header-date")?.textContent).toMatch(WITH_ZONE);
    const signed = document.querySelector(".sig-time");
    expect(signed?.textContent).toMatch(WITH_ZONE);
  });
});

describe("report dates are calendar days in every time zone", () => {
  it.each(["America/Los_Angeles", "Pacific/Honolulu"])("in %s", async (zone) => {
    vi.stubEnv("TZ", zone);
    renderReport();
    await screen.findAllByText("FP1126005");
    expect(screen.getByText("01 Oct 2028")).toBeTruthy();
    expect(screen.getByText("01 Jan 2026")).toBeTruthy();
    expect(d("2028-10-01T00:00:00Z")).toBe("01 Oct 2028");
    expect(dt("2026-11-17T09:41:07Z")).toMatch(WITH_ZONE);
  });
});
