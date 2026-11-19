import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { MediaGptReportService } from "../services/MediaGptReportService";
import type {
  MediaGptListItem,
  MediaGptSearchParams,
  MediaGptSearchResponse,
} from "../types/mediaGptTypes";
import { MediaGptResultsTable } from "./MediaGptResultsTable";

vi.mock("../services/MediaGptReportService", () => ({
  MediaGptReportService: {
    getById: vi.fn(),
    exportCsv: vi.fn(),
  },
}));

const mockedExportCsv = vi.mocked(MediaGptReportService.exportCsv);

function makeLot(overrides: Partial<MediaGptListItem> & { id: number; lotNumber: string }): MediaGptListItem {
  return {
    mediaType: "Tryptic Soy Agar (TSA)",
    preparedAt: "2026-10-01T08:00:00Z",
    expiryDate: "2026-11-01T08:00:00Z",
    evaluationType: "GrowthPromotion",
    evaluationStatus: "Completed",
    evaluationOutcome: "Conform",
    approvalStatus: "Approved",
    isReleasedForUse: true,
    preparedByName: "Analyst One",
    approvedByName: "Reviewer One",
    approvedAt: "2026-10-02T10:00:00Z",
    challengeCount: 3,
    conformedChallengeCount: 3,
    ...overrides,
  };
}

const APPLIED_PARAMS: MediaGptSearchParams = {
  evaluationType: "GrowthPromotion",
  mediaType: "Tryptic Soy Agar (TSA)",
  page: 1,
  pageSize: 25,
  sortBy: "PreparedAt",
  sortDescending: true,
};

function renderTable(
  response: MediaGptSearchResponse,
  params: MediaGptSearchParams = APPLIED_PARAMS
) {
  localStorage.setItem("microlims_permissions", JSON.stringify([PERMISSIONS.DATA_EXPORT]));
  localStorage.setItem("microlims_fullname", "Dr. Ada");
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <AuthProvider>
          <MediaGptResultsTable
            results={response}
            loading={false}
            error={null}
            appliedParams={params}
            onPageChange={vi.fn()}
            onPageSizeChange={vi.fn()}
            onClearFilters={vi.fn()}
          />
        </AuthProvider>
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

let writtenPdfHtml: string[] = [];
let windowOpenSpy: ReturnType<typeof vi.spyOn>;

beforeEach(() => {
  localStorage.clear();
  vi.clearAllMocks();
  writtenPdfHtml = [];
  windowOpenSpy = vi.spyOn(window, "open").mockImplementation(() => {
    const fakeDoc = {
      open: vi.fn(),
      write: (html: string) => {
        writtenPdfHtml.push(html);
      },
      close: vi.fn(),
    };
    return { document: fakeDoc } as unknown as Window;
  });
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  windowOpenSpy.mockRestore();
});

describe("MediaGptResultsTable", () => {
  it("renders all columns in order", () => {
    const items = [makeLot({ id: 1, lotNumber: "LOT-001" })];
    renderTable({ items, totalCount: 1, page: 1, pageSize: 25 });

    const headers = screen
      .getAllByRole("columnheader")
      .map((h) => h.textContent?.trim() ?? "")
      .filter((t) => t.length > 0);

    expect(headers).toEqual([
      "Lot Number",
      "Media Type",
      "Prep Date",
      "Expiry Date",
      "Evaluation",
      "Outcome",
      "Approval Status",
      "Prepared By",
      "Approved By",
      "Actions",
    ]);
  });

  it("preserves row sort order from results", () => {
    const items = [
      makeLot({ id: 2, lotNumber: "LOT-B" }),
      makeLot({ id: 1, lotNumber: "LOT-A" }),
      makeLot({ id: 3, lotNumber: "LOT-C" }),
    ];
    renderTable({ items, totalCount: 3, page: 1, pageSize: 25 });

    const rows = screen.getAllByRole("row").slice(1);
    expect(within(rows[0]).getByText("LOT-B")).toBeTruthy();
    expect(within(rows[1]).getByText("LOT-A")).toBeTruthy();
    expect(within(rows[2]).getByText("LOT-C")).toBeTruthy();
  });

  it("server export call MediaGptReportService.exportCsv(appliedParams) is unchanged", async () => {
    const user = userEvent.setup();
    mockedExportCsv.mockResolvedValue(undefined);
    const items = [makeLot({ id: 1, lotNumber: "LOT-001" })];
    renderTable({ items, totalCount: 1, page: 1, pageSize: 25 }, APPLIED_PARAMS);

    await user.click(screen.getByRole("button", { name: "Export CSV (Flattened)" }));

    await waitFor(() => {
      expect(mockedExportCsv).toHaveBeenCalledTimes(1);
    });
    expect(mockedExportCsv).toHaveBeenCalledWith(APPLIED_PARAMS);
  });

  it("PDF export receives only the selected records", async () => {
    const user = userEvent.setup();
    const items = [
      makeLot({ id: 1, lotNumber: "LOT-SEL-001" }),
      makeLot({ id: 2, lotNumber: "LOT-UNSEL-002" }),
    ];
    renderTable({ items, totalCount: 2, page: 1, pageSize: 25 });

    await user.click(screen.getByRole("checkbox", { name: "Select row 1" }));
    const pdfButton = screen.getByRole("button", { name: /Print \/ PDF View \(1\)/i });
    await user.click(pdfButton);

    expect(writtenPdfHtml).toHaveLength(1);
    expect(writtenPdfHtml[0]).toContain("LOT-SEL-001");
    expect(writtenPdfHtml[0]).not.toContain("LOT-UNSEL-002");
  });

  it("renders all 50 rows when pageSize is 50 and shows exactly one pagination", async () => {
    const user = userEvent.setup();
    const items = Array.from({ length: 50 }, (_, i) =>
      makeLot({ id: i + 1, lotNumber: `LOT-ROW-${i + 1}` })
    );
    const onPageChange = vi.fn();
    const onPageSizeChange = vi.fn();
    localStorage.setItem("microlims_permissions", JSON.stringify([PERMISSIONS.DATA_EXPORT]));
    localStorage.setItem("microlims_fullname", "Dr. Ada");
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <AuthProvider>
            <MediaGptResultsTable
              results={{ items, totalCount: 120, page: 1, pageSize: 50 }}
              loading={false}
              error={null}
              appliedParams={{ ...APPLIED_PARAMS, page: 1, pageSize: 50 }}
              onPageChange={onPageChange}
              onPageSizeChange={onPageSizeChange}
              onClearFilters={vi.fn()}
            />
          </AuthProvider>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const dataRows = screen.getAllByRole("row").slice(1);
    expect(dataRows).toHaveLength(50);
    expect(screen.getByText("LOT-ROW-50")).toBeTruthy();
    expect(screen.getAllByRole("button", { name: /next page/i })).toHaveLength(1);
    expect(screen.getAllByRole("combobox", { name: /rows per page/i })).toHaveLength(1);

    await user.click(screen.getByRole("button", { name: /next page/i }));
    expect(onPageChange).toHaveBeenCalledWith(2);

    await user.click(screen.getByRole("combobox", { name: /rows per page/i }));
    await user.click(screen.getByRole("option", { name: "100" }));
    expect(onPageSizeChange).toHaveBeenCalledWith(100);
  });
});
