import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { renderInArabic, resetLanguage } from "../../../i18n/testUtils";
import { ReferenceStrainReportService } from "../services/ReferenceStrainReportService";
import type {
  ReferenceStrainListItem,
  ReferenceStrainSearchParams,
  ReferenceStrainSearchResponse,
} from "../types/referenceStrainTypes";
import { ReferenceStrainResultsTable } from "./ReferenceStrainResultsTable";

vi.mock("../services/ReferenceStrainReportService", () => ({
  ReferenceStrainReportService: {
    getById: vi.fn(),
    exportCsv: vi.fn(),
  },
}));

const mockedExportCsv = vi.mocked(ReferenceStrainReportService.exportCsv);

function makeStrain(
  overrides: Partial<ReferenceStrainListItem> & { id: number; cryovialCode: string }
): ReferenceStrainListItem {
  return {
    strainName: "Staphylococcus aureus",
    atccNumber: "6538",
    manufacturerName: "Microbiologics",
    sourceMaterialName: "KWIK-STIK",
    sourceMaterialBatchNumber: "M-100",
    receiptDate: "2026-09-01T00:00:00Z",
    preparedAt: "2026-09-10T00:00:00Z",
    expiryDate: "2027-09-10T00:00:00Z",
    numberOfVialsPrepared: 10,
    vialsRemaining: 8,
    storageCondition: "-80°C Freezer",
    approvalStatus: "Approved",
    isDestroyed: false,
    preparedByName: "Analyst One",
    approvedByName: "Reviewer One",
    approvedAt: "2026-09-11T00:00:00Z",
    directUsageCount: 4,
    ...overrides,
  };
}

const APPLIED_PARAMS: ReferenceStrainSearchParams = {
  search: "aureus",
  approvalStatus: "Approved",
  page: 1,
  pageSize: 25,
  sortBy: "PreparedAt",
  sortDescending: true,
};

function renderTable(
  response: ReferenceStrainSearchResponse,
  params: ReferenceStrainSearchParams = APPLIED_PARAMS
) {
  localStorage.setItem("microlims_permissions", JSON.stringify([PERMISSIONS.DATA_EXPORT]));
  localStorage.setItem("microlims_fullname", "Dr. Ada");
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <AuthProvider>
          <ReferenceStrainResultsTable
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

describe("ReferenceStrainResultsTable", () => {
  it("renders all columns in order", () => {
    const items = [makeStrain({ id: 1, cryovialCode: "CRYO-001" })];
    renderTable({ items, totalCount: 1, page: 1, pageSize: 25 });

    const headers = screen
      .getAllByRole("columnheader")
      .map((h) => h.textContent?.trim() ?? "")
      .filter((t) => t.length > 0);

    expect(headers).toEqual([
      "Strain / ATCC",
      "Cryovial Code",
      "Manufacturer",
      "Receipt Date",
      "Prep Date",
      "Expiry Date",
      "Vials Remaining",
      "Storage",
      "Approval Status",
      "Prepared By",
      "Approved By",
      "GPT Usage",
      "Actions",
    ]);
  });

  it("preserves row sort order from results", () => {
    const items = [
      makeStrain({ id: 2, cryovialCode: "CRYO-2" }),
      makeStrain({ id: 1, cryovialCode: "CRYO-1" }),
      makeStrain({ id: 3, cryovialCode: "CRYO-3" }),
    ];
    renderTable({ items, totalCount: 3, page: 1, pageSize: 25 });

    const rows = screen.getAllByRole("row").slice(1);
    expect(within(rows[0]).getByText("CRYO-2")).toBeTruthy();
    expect(within(rows[1]).getByText("CRYO-1")).toBeTruthy();
    expect(within(rows[2]).getByText("CRYO-3")).toBeTruthy();
  });

  it("server export call ReferenceStrainReportService.exportCsv(appliedParams) is unchanged", async () => {
    const user = userEvent.setup();
    mockedExportCsv.mockResolvedValue(undefined);
    const items = [makeStrain({ id: 1, cryovialCode: "CRYO-001" })];
    renderTable({ items, totalCount: 1, page: 1, pageSize: 25 }, APPLIED_PARAMS);

    await user.click(screen.getByRole("button", { name: "Export CSV" }));

    await waitFor(() => {
      expect(mockedExportCsv).toHaveBeenCalledTimes(1);
    });
    expect(mockedExportCsv).toHaveBeenCalledWith(APPLIED_PARAMS);
  });

  it("PDF export receives only the selected records", async () => {
    const user = userEvent.setup();
    const items = [
      makeStrain({ id: 1, cryovialCode: "CRYO-SEL-001" }),
      makeStrain({ id: 2, cryovialCode: "CRYO-UNSEL-002" }),
    ];
    renderTable({ items, totalCount: 2, page: 1, pageSize: 25 });

    await user.click(screen.getByRole("checkbox", { name: "Select row 1" }));
    const pdfButton = screen.getByRole("button", { name: /Print \/ PDF View \(1\)/i });
    await user.click(pdfButton);

    expect(writtenPdfHtml).toHaveLength(1);
    expect(writtenPdfHtml[0]).toContain("CRYO-SEL-001");
    expect(writtenPdfHtml[0]).not.toContain("CRYO-UNSEL-002");
  });

  it("renders Arabic title and column headers when language is Arabic", async () => {
    localStorage.setItem("microlims_permissions", JSON.stringify([PERMISSIONS.DATA_EXPORT]));
    localStorage.setItem("microlims_fullname", "Dr. Ada");
    await renderInArabic(
      <MemoryRouter>
        <AuthProvider>
          <ReferenceStrainResultsTable
            results={{ items: [makeStrain({ id: 1, cryovialCode: "CRYO-AR-1" })], totalCount: 1, page: 1, pageSize: 25 }}
            loading={false}
            error={null}
            appliedParams={APPLIED_PARAMS}
            onPageChange={vi.fn()}
            onPageSizeChange={vi.fn()}
            onClearFilters={vi.fn()}
          />
        </AuthProvider>
      </MemoryRouter>,
      ["common", "reports"]
    );

    try {
      expect(document.documentElement.dir).toBe("rtl");
      expect(screen.getByText("السلالات المرجعية ودفعات مزارع العمل")).toBeTruthy();
      expect(screen.getByRole("button", { name: "تصدير CSV" })).toBeTruthy();
    } finally {
      await resetLanguage();
    }
  });
});
