import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider, type Role } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { tableViewService } from "../../../components/lab/table/services/tableViewService";
import { documentControlService } from "../services/documentControlService";
import type {
  DocumentDepartmentDto,
  DocumentMasterDto,
  DocumentMasterSummaryDto,
  DocumentTypeDto
} from "../types/documentControlTypes";
import { DocumentLibraryPage } from "./DocumentLibraryPage";

// Pins the Document Library page: columns, every filter, row actions, permissions
// and the exact service calls. The same assertions hold before and after moving onto RegisterTable.

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

function seedAuth(permissions: string[], role: Role = "Analyst") {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role, permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
  localStorage.setItem("microlims_user_id", "1");
}

const TYPES: DocumentTypeDto[] = [
  { id: 1, code: "SOP", name: "Standard Operating Procedure", defaultReviewCycleMonths: 24, isActive: true, requiresQaApprover: true },
  { id: 2, code: "WI", name: "Work Instruction", defaultReviewCycleMonths: 12, isActive: true, requiresQaApprover: false }
];

const DEPTS: DocumentDepartmentDto[] = [
  { id: 1, code: "QC", name: "Quality Control", isActive: true, sections: [] },
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
  departmentId: 1,
  departmentName: "Quality Control",
  sectionId: 1,
  sectionName: "Microbiology",
  sectionIds: [1, 2],
  sectionNames: ["Microbiology", "Sterility"],
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
  nextReviewDate: "2020-01-01T00:00:00Z",
  hasControlledPdf: true,
  hasSourceFile: true,
  createdAt: "2025-12-01T00:00:00Z",
  ...over
});

const DOCS: DocumentMasterSummaryDto[] = [
  doc({ id: 10 }),
  doc({
    id: 11,
    microLimsDocumentId: "DOC-0000011",
    companyDocumentCode: "WI-002",
    title: "Balance Calibration Check",
    documentTypeId: 2,
    documentTypeCode: "WI",
    departmentId: 2,
    departmentName: "Quality Assurance",
    sectionNames: [],
    sectionName: "Calibration",
    documentOwnerUserName: "Bob Metrologist",
    currentEffectiveRevisionId: null,
    currentRevisionNumber: null,
    currentRevisionStatus: "Draft",
    effectiveDate: null,
    nextReviewDate: null,
    hasControlledPdf: false,
    hasSourceFile: false
  }),
  doc({
    id: 12,
    microLimsDocumentId: "DOC-0000012",
    companyDocumentCode: "SOP-003",
    title: "Retired Procedure",
    recordStatus: "Void",
    currentEffectiveRevisionId: null,
    currentRevisionStatus: "Cancelled",
    effectiveDate: null,
    nextReviewDate: null,
    hasControlledPdf: false,
    hasSourceFile: false
  })
];

function LocationProbe() {
  const location = useLocation();
  return <output aria-label="Location">{`${location.pathname}${location.search}`}</output>;
}

function renderPage(initialUrl = "/document-control/library") {
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

const tableRows = () => screen.getAllByRole("row").slice(1).map((r) => r.textContent ?? "");
const ready = () => waitFor(() => expect(screen.getByText("SOP-001")).toBeTruthy());

async function pickSelect(user: ReturnType<typeof userEvent.setup>, label: string, option: string) {
  await user.click(screen.getByLabelText(label));
  await user.click(await screen.findByRole("option", { name: option }));
}

describe("DocumentLibraryPage register", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedAuth([], "Analyst");
    vi.mocked(documentControlService.getTypes).mockResolvedValue(TYPES);
    vi.mocked(documentControlService.getDepartments).mockResolvedValue(DEPTS);
    vi.mocked(documentControlService.getLibrary).mockResolvedValue({
      items: DOCS,
      totalCount: DOCS.length,
      page: 1,
      pageSize: 10,
      totalPages: 1
    });
    vi.mocked(documentControlService.viewFileBlob).mockResolvedValue({
      blob: new Blob(["pdf"]),
      url: "blob:mock-pdf"
    });
    vi.mocked(tableViewService.list).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("loads types, departments and library once, and shows every column and cell format", async () => {
    renderPage();
    await ready();

    expect(documentControlService.getTypes).toHaveBeenCalledTimes(1);
    expect(documentControlService.getDepartments).toHaveBeenCalledTimes(1);
    expect(documentControlService.getLibrary).toHaveBeenCalledWith({
      searchTerm: undefined,
      documentTypeId: undefined,
      departmentId: undefined,
      revisionStatus: undefined,
      includeCancelledAndVoided: false,
      isOverdue: undefined,
      page: 1,
      // RegisterTable / useTableState standardise default page size to 25 (replacing legacy 10).
      pageSize: 25
    });

    expect(screen.getAllByRole("columnheader").map((h) => h.textContent?.trim())).toEqual([
      "Company Code",
      "MicroLIMS ID",
      "Title",
      "Type",
      "Dept / Section",
      "Owner",
      "Rev",
      "Status",
      "Files",
      "Effective Date",
      "Actions"
    ]);

    const [first, second, third] = tableRows();
    expect(first).toContain("SOP-001");
    expect(first).toContain("DOC-0000010");
    expect(first).toContain("Microbial Limit Testing");
    expect(first).toContain("SOP");
    expect(first).toContain("Quality Control");
    expect(first).toContain("Microbiology, Sterility");
    expect(first).toContain("Alice Owner");
    expect(first).toContain("02");
    expect(first).toContain("Effective");
    expect(first).toContain("REVIEW OVERDUE");

    expect(second).toContain("WI-002");
    expect(second).toContain("01");
    expect(second).toContain("Draft");
    expect(second).toContain("Pending");
    expect(second).toContain("—");

    expect(third).toContain("SOP-003");
    expect(third).toContain("VOID");
  });

  it("search, dropdowns and toggle buttons pass exact filter params and Reset clears them", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    const resetBtn = () => screen.getByRole("button", { name: "Reset" }) as HTMLButtonElement;
    expect(resetBtn().disabled).toBe(true);

    const searchInput = screen.getByRole("textbox", { name: "Search code, title, or keywords" });
    await user.type(searchInput, "Microbial");
    await waitFor(
      () => {
        expect(documentControlService.getLibrary).toHaveBeenLastCalledWith(
          expect.objectContaining({ searchTerm: "Microbial", page: 1 })
        );
      },
      { timeout: 3000 }
    );
    expect(resetBtn().disabled).toBe(false);

    await pickSelect(user, "Document Type", "SOP: Standard Operating Procedure");
    await waitFor(() =>
      expect(documentControlService.getLibrary).toHaveBeenLastCalledWith(
        expect.objectContaining({ documentTypeId: 1, page: 1 })
      )
    );

    await pickSelect(user, "Department", "QA: Quality Assurance");
    await waitFor(() =>
      expect(documentControlService.getLibrary).toHaveBeenLastCalledWith(
        expect.objectContaining({ departmentId: 2, page: 1 })
      )
    );

    await pickSelect(user, "Status", "Effective");
    await waitFor(() =>
      expect(documentControlService.getLibrary).toHaveBeenLastCalledWith(
        expect.objectContaining({ revisionStatus: "Effective", page: 1 })
      )
    );

    await user.click(screen.getByRole("button", { name: "Show voided" }));
    await waitFor(() =>
      expect(documentControlService.getLibrary).toHaveBeenLastCalledWith(
        expect.objectContaining({ includeCancelledAndVoided: true, page: 1 })
      )
    );

    await user.click(screen.getByRole("button", { name: "Review overdue only" }));
    await waitFor(() =>
      expect(documentControlService.getLibrary).toHaveBeenLastCalledWith(
        expect.objectContaining({ isOverdue: true, page: 1 })
      )
    );

    await user.click(resetBtn());
    await waitFor(() =>
      expect(documentControlService.getLibrary).toHaveBeenLastCalledWith(
        expect.objectContaining({
          searchTerm: undefined,
          documentTypeId: undefined,
          departmentId: undefined,
          revisionStatus: undefined,
          includeCancelledAndVoided: false,
          isOverdue: undefined,
          page: 1
        })
      )
    );
  });

  it("navigates to document details, opens controlled PDF, and enforces Void/Register permissions", async () => {
    const user = userEvent.setup();
    seedAuth([PERMISSIONS.DOCUMENTS_REGISTER], "SectionHead");
    vi.mocked(documentControlService.getById).mockResolvedValue({
      id: 10,
      companyDocumentCode: "SOP-001",
      microLimsDocumentId: "DOC-0000010",
      title: "Microbial Limit Testing",
      currentEffectiveRevisionId: 101,
      revisions: [
        {
          id: 101,
          revisionNumber: "02",
          revisionStatus: "Effective",
          files: [
            {
              id: 500,
              documentRevisionId: 101,
              fileRole: "ControlledPdf",
              fileName: "sop-001.pdf",
              contentType: "application/pdf",
              sizeBytes: 1024,
              contentSha256: "abc",
              isActive: true,
              supersededByFileId: null,
              uploadedAt: "2026-01-01T00:00:00Z",
              uploadedByUserId: 1,
              uploadedByUserName: "Alice"
            }
          ]
        }
      ]
    } as unknown as DocumentMasterDto);

    renderPage();
    await ready();

    expect(screen.getByRole("button", { name: "Register New Document" })).toBeTruthy();

    // Only WI-002 (Active, never effective) has a Void button; SOP-001 (effective) and SOP-003 (Void) do not.
    expect(screen.queryByRole("button", { name: "Void document master SOP-001" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Void document master SOP-003" })).toBeNull();
    expect(screen.getByRole("button", { name: "Void document master WI-002" })).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "View controlled PDF for SOP-001" }));
    await waitFor(() => expect(documentControlService.getById).toHaveBeenCalledWith(10));
    const pdfDialog = await screen.findByRole("dialog");
    await user.click(within(pdfDialog).getByRole("button", { name: "Done" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    await user.click(screen.getByRole("button", { name: "Void document master WI-002" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/Void Document Master/i)).toBeTruthy();
    await user.click(within(dialog).getByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    await user.click(screen.getByRole("button", { name: "Open details for SOP-001" }));
    expect(screen.getByLabelText("Location").textContent).toBe("/document-control/documents/10");
  });

  it("hides Register and Void controls for a plain Analyst", async () => {
    renderPage();
    await ready();
    expect(screen.queryByRole("button", { name: "Register New Document" })).toBeNull();
    expect(screen.queryByRole("button", { name: /Void document master/ })).toBeNull();
  });

  it("requests the next page from the server when paging", async () => {
    const user = userEvent.setup();
    vi.mocked(documentControlService.getLibrary).mockResolvedValue({
      items: DOCS,
      totalCount: 60,
      page: 1,
      pageSize: 10,
      totalPages: 6
    });
    renderPage();
    await ready();

    await user.click(screen.getByRole("button", { name: "Go to next page" }));
    await waitFor(() =>
      expect(documentControlService.getLibrary).toHaveBeenLastCalledWith(
        expect.objectContaining({ page: 2 })
      )
    );
  });

  it("shows empty, library-error and reference-filter-error states", async () => {
    vi.mocked(documentControlService.getLibrary).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 10,
      totalPages: 0
    });
    renderPage();
    expect(await screen.findByText("No controlled documents match the selected filters")).toBeTruthy();
    cleanup();

    vi.mocked(documentControlService.getTypes).mockRejectedValue(new Error("types down"));
    vi.mocked(documentControlService.getLibrary).mockRejectedValue(new Error("Library unavailable"));
    renderPage();
    expect(await screen.findByText("Library unavailable")).toBeTruthy();
    expect(
      screen.getByText(/The document types and departments could not be loaded, so those filters are empty/)
    ).toBeTruthy();
  });
});
