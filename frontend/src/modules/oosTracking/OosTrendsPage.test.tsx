import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { OosTrendsPage } from "./OosTrendsPage";
import { oosTrendsService, type OosTrendReportDto } from "../../services/oosTrendsService";
import { laboratorySectionService } from "../../services/laboratorySectionService";

vi.mock("../../services/oosTrendsService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../services/oosTrendsService")>();
  return { ...actual, oosTrendsService: { getTrends: vi.fn() } };
});

vi.mock("../../services/laboratorySectionService", () => ({
  laboratorySectionService: { getMySections: vi.fn() }
}));

const report = (overrides: Partial<OosTrendReportDto> = {}): OosTrendReportDto => ({
  groupBy: "cause",
  fromUtc: "2026-01-01T00:00:00Z",
  toUtc: "2026-11-30T23:59:59Z",
  groups: [
    {
      key: "3",
      label: "Analyst technique",
      totalCount: 7,
      openCount: 2,
      concludedCount: 5,
      confirmedCount: 1,
      invalidatedCount: 3,
      excursionClosedCount: 1,
      medianWorkingDaysToConclude: 6.5
    },
    {
      key: "unassigned",
      label: "Unassigned",
      totalCount: 1,
      openCount: 1,
      concludedCount: 0,
      confirmedCount: 0,
      invalidatedCount: 0,
      excursionClosedCount: 0,
      medianWorkingDaysToConclude: null
    }
  ],
  ...overrides
});

function renderPage() {
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <OosTrendsPage />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("OosTrendsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      {
        sectionId: 4,
        sectionCode: "MICRO",
        sectionName: "Microbiology",
        departmentId: 1,
        departmentCode: "QC",
        departmentName: "Quality Control"
      }
    ]);
    vi.mocked(oosTrendsService.getTrends).mockResolvedValue(report());
  });

  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("loads the default cause grouping and shows the server's counts and median", async () => {
    renderPage();

    expect(await screen.findByText("Analyst technique")).toBeTruthy();
    expect(oosTrendsService.getTrends).toHaveBeenCalledWith(
      expect.objectContaining({ groupBy: "cause" }),
      expect.anything()
    );
    const row = screen.getByText("Analyst technique").closest("tr") as HTMLElement;
    const cells = within(row).getAllByRole("cell").map((c) => c.textContent);
    expect(cells).toEqual(expect.arrayContaining(["7", "2", "5", "1", "3", "6.5"]));
    const unassigned = screen.getByText("Unassigned").closest("tr") as HTMLElement;
    expect(within(unassigned).getAllByRole("cell").slice(-1)[0]?.textContent).toBe("—");
  });

  it("offers exactly the groupings the API accepts and reloads when one is chosen", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText("Analyst technique");

    await user.click(screen.getByRole("combobox", { name: /Group by/ }));
    const options = screen.getAllByRole("option").map((o) => o.textContent);
    expect(options).toEqual(["Product", "Test", "Analyst", "Instrument", "Cause category", "Conclusion"]);
    await user.click(screen.getByRole("option", { name: "Test" }));

    await waitFor(() =>
      expect(oosTrendsService.getTrends).toHaveBeenLastCalledWith(
        expect.objectContaining({ groupBy: "test" }),
        expect.anything()
      )
    );
  });

  it("sends the date range, kind and section filters", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText("Analyst technique");

    await user.type(screen.getByLabelText("Opened from"), "2026-02-01");
    await user.type(screen.getByLabelText("Opened to"), "2026-02-28");
    await user.click(screen.getByRole("combobox", { name: /Kind/ }));
    await user.click(screen.getByRole("option", { name: "Excursion" }));
    await user.click(screen.getByRole("combobox", { name: /Section/ }));
    await user.click(await screen.findByRole("option", { name: "Microbiology" }));

    await waitFor(() =>
      expect(oosTrendsService.getTrends).toHaveBeenLastCalledWith(
        {
          groupBy: "cause",
          from: "2026-02-01",
          to: "2026-02-28T23:59:59.999Z",
          kind: "Excursion",
          sectionId: 4
        },
        expect.anything()
      )
    );
  });

  it("shows the server's refusal and lets the user retry", async () => {
    const user = userEvent.setup();
    vi.mocked(oosTrendsService.getTrends).mockRejectedValueOnce({
      response: { data: { message: "Choose a valid trend grouping." } }
    });
    renderPage();

    expect((await screen.findByRole("alert")).textContent).toContain("Choose a valid trend grouping.");
    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByText("Analyst technique")).toBeTruthy();
  });

  it("says so when no investigation matches", async () => {
    vi.mocked(oosTrendsService.getTrends).mockResolvedValue(report({ groups: [] }));
    renderPage();

    expect(await screen.findByText("No investigations match these filters.")).toBeTruthy();
  });

  it("keeps the trend table when the section lookup fails and names the missing source", async () => {
    vi.mocked(laboratorySectionService.getMySections).mockRejectedValue(new Error("network"));
    renderPage();

    expect(await screen.findByText("Analyst technique")).toBeTruthy();
    expect(await screen.findByText(/Some data could not be loaded: Sections/)).toBeTruthy();
  });

  it("shows the reporting period with its time zone", async () => {
    renderPage();
    await screen.findByText("Analyst technique");
    expect(screen.getByText(/^Period: .*2026.* [A-Z]{3,5} to .*2026.* [A-Z]{3,5}$/)).toBeTruthy();
  });

  it("renders in Arabic with right-to-left direction", async () => {
    await renderInArabic(
      <MemoryRouter>
        <OosTrendsPage />
      </MemoryRouter>,
      ["common", "oosTracking"]
    );

    expect(await screen.findByText("Analyst technique")).toBeTruthy();
    expect(screen.getByRole("combobox", { name: /تجميع حسب/ })).toBeTruthy();
  });
});
