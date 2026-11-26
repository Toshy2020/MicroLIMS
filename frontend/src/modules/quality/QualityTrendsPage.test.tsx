import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { QualityTrendsPage } from "./QualityTrendsPage";
import { qualityTrendsService, type QualityTrendReportDto } from "../../services/qualityTrendsService";

vi.mock("../../services/qualityTrendsService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../services/qualityTrendsService")>();
  return { ...actual, qualityTrendsService: { getTrends: vi.fn() } };
});

const report = (overrides: Partial<QualityTrendReportDto> = {}): QualityTrendReportDto => ({
  groupBy: "type",
  fromUtc: "2025-11-26T00:00:00Z",
  toUtc: "2026-11-26T23:59:59Z",
  repeatWindowDays: 90,
  groups: [
    {
      key: "Deviation",
      label: "Deviation",
      totalCount: 12,
      openCount: 4,
      closedCount: 6,
      averageTimeToCloseDays: 11.25,
      onTimeRate: 0.8333,
      capaEffectivenessRate: null,
      cancelledCount: 1,
      rejectedCount: 1
    },
    {
      key: "Capa",
      label: "CAPA",
      totalCount: 3,
      openCount: 1,
      closedCount: 2,
      averageTimeToCloseDays: null,
      onTimeRate: null,
      capaEffectivenessRate: 0.5,
      cancelledCount: 0,
      rejectedCount: 0
    }
  ],
  repeatCauses: [
    {
      causeCategoryId: 5,
      causeCategoryName: "Equipment failure",
      targetType: "Equipment",
      targetId: 31,
      targetLabel: "HPLC-02",
      occurrenceCount: 3,
      firstRaisedAtUtc: "2026-09-01T08:00:00Z",
      lastRaisedAtUtc: "2026-10-12T08:00:00Z",
      recordNumbers: ["DEV-2026-0007", "DEV-2026-0012", "DEV-2026-0019"]
    }
  ],
  ...overrides
});

function renderPage() {
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <QualityTrendsPage />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("QualityTrendsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(qualityTrendsService.getTrends).mockResolvedValue(report());
  });

  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("loads the default type grouping and shows the server's figures as percentages and days", async () => {
    renderPage();

    expect(await screen.findByText("CAPA")).toBeTruthy();
    expect(qualityTrendsService.getTrends).toHaveBeenCalledWith(
      expect.objectContaining({ groupBy: "type", repeatWindowDays: 90 }),
      expect.anything()
    );
    const deviation = screen.getByText("Deviation", { selector: "td *, td" }).closest("tr") as HTMLElement;
    const cells = within(deviation).getAllByRole("cell").map((c) => c.textContent);
    expect(cells).toEqual(expect.arrayContaining(["12", "4", "6", "1", "11.25", "83.3%", "—"]));
    const capa = screen.getByText("CAPA").closest("tr") as HTMLElement;
    expect(within(capa).getAllByRole("cell").map((c) => c.textContent)).toEqual(
      expect.arrayContaining(["50.0%", "—"])
    );
  });

  it("offers exactly the groupings the API accepts and reloads when one is chosen", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText("CAPA");

    await user.click(screen.getByRole("combobox", { name: /Group by/ }));
    expect(screen.getAllByRole("option").map((o) => o.textContent)).toEqual([
      "Record type",
      "Classification",
      "Cause category",
      "Section",
      "Item",
      "Source"
    ]);
    await user.click(screen.getByRole("option", { name: "Item" }));

    await waitFor(() =>
      expect(qualityTrendsService.getTrends).toHaveBeenLastCalledWith(
        expect.objectContaining({ groupBy: "item" }),
        expect.anything()
      )
    );
  });

  it("sends the date range and the repeat-cause window", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText("CAPA");

    await user.type(screen.getByLabelText("Raised from"), "2026-01-01");
    await user.type(screen.getByLabelText("Raised to"), "2026-06-30");
    const window = screen.getByLabelText("Repeat window (days)");
    await user.clear(window);
    await user.type(window, "180");

    await waitFor(() =>
      expect(qualityTrendsService.getTrends).toHaveBeenLastCalledWith(
        { groupBy: "type", from: "2026-01-01", to: "2026-06-30", repeatWindowDays: 180 },
        expect.anything()
      )
    );
  });

  it("lists repeat causes with the record numbers behind them", async () => {
    renderPage();

    const heading = await screen.findByRole("heading", { name: "Repeat causes" });
    const section = heading.closest("section") as HTMLElement;
    expect(within(section).getByText("Equipment failure")).toBeTruthy();
    expect(within(section).getByText("HPLC-02")).toBeTruthy();
    expect(within(section).getByText("DEV-2026-0007, DEV-2026-0012, DEV-2026-0019")).toBeTruthy();
    expect(within(section).getByText("3")).toBeTruthy();
  });

  it("says when there are no repeat causes", async () => {
    vi.mocked(qualityTrendsService.getTrends).mockResolvedValue(report({ repeatCauses: [] }));
    renderPage();

    expect(await screen.findByText("No repeat causes in the selected window.")).toBeTruthy();
  });

  it("shows the server's refusal (for example a range over five years) and retries", async () => {
    const user = userEvent.setup();
    vi.mocked(qualityTrendsService.getTrends).mockRejectedValueOnce(
      Object.assign(new Error("Request failed"), {
        isAxiosError: true,
        response: { data: { message: "The trend date range cannot exceed 5 years." } }
      })
    );
    renderPage();

    expect((await screen.findByRole("alert")).textContent).toContain("The trend date range cannot exceed 5 years.");
    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByText("CAPA")).toBeTruthy();
  });

  it("says so when no record matches", async () => {
    vi.mocked(qualityTrendsService.getTrends).mockResolvedValue(report({ groups: [], repeatCauses: [] }));
    renderPage();

    expect(await screen.findByText("No quality records match these filters.")).toBeTruthy();
  });

  it("renders in Arabic", async () => {
    await renderInArabic(
      <MemoryRouter>
        <QualityTrendsPage />
      </MemoryRouter>,
      ["common", "quality"]
    );

    expect(await screen.findByText("CAPA")).toBeTruthy();
    expect(screen.getByRole("combobox", { name: /تجميع حسب/ })).toBeTruthy();
  });
});
