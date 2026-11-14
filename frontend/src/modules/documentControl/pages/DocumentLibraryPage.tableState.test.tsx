import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { tableViewService, type TableViewDto } from "../../../components/lab/table/services/tableViewService";
import { documentControlService } from "../services/documentControlService";
import type {
  DocumentDepartmentDto,
  DocumentMasterSummaryDto,
  DocumentTypeDto
} from "../types/documentControlTypes";
import { DocumentLibraryPage } from "./DocumentLibraryPage";

// Document Library on the shared table state (smarter-tables Task 10): filters and page in the address, saved views.

vi.mock("../services/documentControlService", () => ({
  documentControlService: {
    getLibrary: vi.fn(),
    getTypes: vi.fn(),
    getDepartments: vi.fn(),
    getById: vi.fn(),
    voidMaster: vi.fn(),
    viewFileBlob: vi.fn()
  }
}));
vi.mock("../../../components/lab/table/services/tableViewService", () => ({
  tableViewService: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() }
}));

const TYPES: DocumentTypeDto[] = [
  { id: 1, code: "SOP", name: "Standard Operating Procedure", defaultReviewCycleMonths: 24, isActive: true, requiresQaApprover: true }
];

const DEPTS: DocumentDepartmentDto[] = [
  { id: 2, code: "QA", name: "Quality Assurance", isActive: true, sections: [] }
];

const doc = (over: Partial<DocumentMasterSummaryDto>): DocumentMasterSummaryDto => ({
  id: 10,
  microLimsDocumentId: "DOC-0000010",
  companyDocumentCode: "SOP-001",
  title: "Microbial Limit Testing",
  documentTypeId: 1,
  documentTypeName: "Standard Operating Procedure",
  documentTypeCode: "SOP",
  departmentId: 2,
  departmentName: "Quality Assurance",
  sectionId: 1,
  sectionName: "Microbiology",
  sectionIds: [1],
  sectionNames: ["Microbiology"],
  documentOwnerUserId: 5,
  documentOwnerUserName: "Alice Owner",
  confidentiality: "Internal",
  category: null,
  recordOrigin: "Native",
  recordStatus: "Active",
  currentEffectiveRevisionId: 101,
  currentRevisionNumber: "02",
  currentRevisionStatus: "Effective",
  effectiveDate: "2026-01-15T00:00:00Z",
  nextReviewDate: "2027-01-15T00:00:00Z",
  hasControlledPdf: true,
  hasSourceFile: false,
  createdAt: "2025-12-01T00:00:00Z",
  ...over
});

function savedView(stateJson: string): TableViewDto {
  return {
    id: 7,
    tableKey: "documentControl.library",
    name: "QA Overdue SOPs",
    scope: "Personal",
    sectionId: null,
    sectionName: null,
    isMine: true,
    canEdit: true,
    isDefault: false,
    ownerName: "Analyst",
    stateJson
  };
}

function LocationProbe() {
  const location = useLocation();
  return <output aria-label="Address">{location.search}</output>;
}

function renderPage(initialUrl = "/") {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialUrl]}>
          <DocumentLibraryPage />
          <LocationProbe />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const address = () => new URLSearchParams(screen.getByLabelText("Address").textContent ?? "");
const ready = () => waitFor(() => expect(screen.getByText("SOP-001")).toBeTruthy());
const hasHeader = (label: string) =>
  screen.getAllByRole("columnheader").some((h) => h.textContent?.trim() === label);

async function pickSelect(user: ReturnType<typeof userEvent.setup>, label: string, option: string) {
  await user.click(screen.getByLabelText(label));
  await user.click(await screen.findByRole("option", { name: option }));
}

describe("DocumentLibraryPage on the shared table state", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(documentControlService.getTypes).mockResolvedValue(TYPES);
    vi.mocked(documentControlService.getDepartments).mockResolvedValue(DEPTS);
    vi.mocked(documentControlService.getLibrary).mockResolvedValue({
      items: [doc({ id: 10 })],
      totalCount: 1,
      page: 1,
      pageSize: 25,
      totalPages: 1
    });
    vi.mocked(tableViewService.list).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("URL round trip: search, dropdowns and toggles are in the address and restore on reload", async () => {
    const user = userEvent.setup();
    const first = renderPage("/");
    await ready();

    await user.type(screen.getByRole("textbox", { name: "Search code, title, or keywords" }), "micro");
    await pickSelect(user, "Document Type", "SOP: Standard Operating Procedure");
    await pickSelect(user, "Department", "QA: Quality Assurance");
    await pickSelect(user, "Status", "Effective");
    await user.click(screen.getByRole("button", { name: "Show voided" }));
    await user.click(screen.getByRole("button", { name: "Review overdue only" }));

    await waitFor(
      () => expect(address().get("f.reviewOverdue")).toBe("true"),
      { timeout: 3000 }
    );
    expect(address().get("q")).toBe("micro");
    expect(address().get("f.documentTypeId")).toBe("1");
    expect(address().get("f.departmentId")).toBe("2");
    expect(address().get("f.revisionStatus")).toBe("Effective");
    expect(address().get("f.showVoided")).toBe("true");

    const url = `/${screen.getByLabelText("Address").textContent}`;
    first.unmount();

    vi.mocked(documentControlService.getLibrary).mockClear();
    renderPage(url);
    await ready();

    await waitFor(() =>
      expect(documentControlService.getLibrary).toHaveBeenCalledWith(
        expect.objectContaining({
          searchTerm: "micro",
          documentTypeId: 1,
          departmentId: 2,
          revisionStatus: "Effective",
          includeCancelledAndVoided: true,
          isOverdue: true,
          page: 1,
          pageSize: 25
        })
      )
    );
    expect((screen.getByRole("textbox", { name: "Search code, title, or keywords" }) as HTMLInputElement).value).toBe("micro");
    expect((screen.getByRole("button", { name: "Show voided" })).getAttribute("aria-pressed")).toBe("true");
    expect((screen.getByRole("button", { name: "Review overdue only" })).getAttribute("aria-pressed")).toBe("true");
  });

  it("Reset clears every filter from the address", async () => {
    const user = userEvent.setup();
    renderPage("/?q=sop&f.documentTypeId=1&f.departmentId=2&f.revisionStatus=Effective&f.showVoided=true&f.reviewOverdue=true");
    await ready();

    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(address().has("q")).toBe(false));
    expect(address().has("f.documentTypeId")).toBe(false);
    expect(address().has("f.departmentId")).toBe(false);
    expect(address().has("f.revisionStatus")).toBe(false);
    expect(address().has("f.showVoided")).toBe(false);
    expect(address().has("f.reviewOverdue")).toBe(false);
  });

  it("saved view applies: its filters reach the server query and its columns are the only ones shown", async () => {
    vi.mocked(tableViewService.list).mockResolvedValue([
      savedView(
        JSON.stringify({
          v: 1,
          filters: { documentTypeId: "1", reviewOverdue: "true" },
          search: "sterility",
          sort: null,
          pageSize: 50,
          columns: ["companyDocumentCode", "title", "status"],
          density: "compact"
        })
      )
    ]);

    renderPage("/?view=7");
    await ready();

    await waitFor(() =>
      expect(documentControlService.getLibrary).toHaveBeenLastCalledWith(
        expect.objectContaining({
          searchTerm: "sterility",
          documentTypeId: 1,
          isOverdue: true,
          pageSize: 50
        })
      )
    );
    expect(screen.getAllByRole("columnheader").map((h) => h.textContent?.trim()).filter(Boolean)).toEqual([
      "Company Code",
      "Title",
      "Status",
      "Actions"
    ]);
  });

  it("the toolbar offers saved views, a column chooser, and no column sort on a server-paged list", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    expect(screen.getByRole("button", { name: /Views/ })).toBeTruthy();
    for (const h of screen.getAllByRole("columnheader")) {
      expect(h.getAttribute("aria-sort")).toBeNull();
    }

    expect(hasHeader("MicroLIMS ID")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(await screen.findByRole("checkbox", { name: "MicroLIMS ID" }));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(hasHeader("MicroLIMS ID")).toBe(false));
    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(await screen.findByRole("checkbox", { name: "MicroLIMS ID" }));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(hasHeader("MicroLIMS ID")).toBe(true));
  });
});
