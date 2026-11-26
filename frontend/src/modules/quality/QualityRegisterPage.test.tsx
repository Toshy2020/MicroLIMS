import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn()
}));
vi.mock("../../services/apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../services/apiClient")>();
  return { ...actual, apiClient: http };
});

import { QualityRegisterPage } from "./QualityRegisterPage";
import { listItem, refusal } from "./testFixtures";
import type { QualityRecordListItemDto } from "../../services/qualityService";

const pageOf = (items: QualityRecordListItemDto[], totalCount = items.length, page = 1) => ({
  data: { success: true, data: { items, totalCount, page, pageSize: 25 } }
});

interface Backend {
  records?: (params: Record<string, unknown>) => Promise<unknown>;
  classifications?: () => Promise<unknown>;
  sections?: () => Promise<unknown>;
}

function serve(backend: Backend = {}) {
  http.get.mockImplementation((url: string, config?: { params?: Record<string, unknown> }) => {
    if (url === "/quality/records") {
      return (backend.records ?? (() => Promise.resolve(pageOf([listItem()]))))(config?.params ?? {});
    }
    if (url === "/quality/config/classifications") {
      return (
        backend.classifications ??
        (() =>
          Promise.resolve({
            data: {
              data: [
                { id: 1, name: "Minor", order: 1, isActive: true, version: 1 },
                { id: 2, name: "Major", order: 2, isActive: true, version: 1 }
              ]
            }
          }))
      )();
    }
    if (url === "/org/my-sections") {
      return (
        backend.sections ??
        (() =>
          Promise.resolve({
            data: {
              data: [
                { sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology", departmentId: 1, departmentCode: "QC", departmentName: "Quality Control" },
                { sectionId: 2, sectionCode: "FP", sectionName: "Finished Product", departmentId: 1, departmentCode: "QC", departmentName: "Quality Control" }
              ]
            }
          }))
      )();
    }
    return Promise.reject(new Error(`unexpected GET ${url}`));
  });
}

function setAuth(userId = 8) {
  localStorage.setItem("microlims_username", "shead");
  localStorage.setItem("microlims_role", "SectionHead");
  localStorage.setItem("microlims_full_name", "Sam Head");
  localStorage.setItem("microlims_user_id", String(userId));
  localStorage.setItem("microlims_permissions", JSON.stringify(["Quality.View"]));
}

function renderPage() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={["/quality"]}>
          <Routes>
            <Route path="/quality" element={<QualityRegisterPage />} />
            <Route path="/quality/:id" element={<p>Record page opened</p>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const recordCalls = () => http.get.mock.calls.filter((call) => call[0] === "/quality/records");
const lastRecordParams = () => recordCalls()[recordCalls().length - 1][1].params as Record<string, unknown>;

describe("QualityRegisterPage", () => {
  beforeEach(() => {
    setAuth();
    serve();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.clearAllMocks();
  });

  it("has a tab for every record type and starts on all records, read from the server", async () => {
    renderPage();

    const tabs = await screen.findByRole("tablist", { name: "Record type" });
    for (const name of ["All", "Deviations", "CAPAs", "Change controls", "Complaints"]) {
      expect(within(tabs).getByRole("tab", { name })).toBeTruthy();
    }
    expect(within(tabs).getByRole("tab", { name: "All" }).getAttribute("aria-selected")).toBe("true");
    await waitFor(() => expect(recordCalls()).toHaveLength(1));
    expect(lastRecordParams()).toEqual({ page: 1, pageSize: 25 });
    expect(recordCalls()[0][1].signal).toBeInstanceOf(AbortSignal);
  });

  it("asks the server for one type when its tab is chosen, from the first page", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText("DEV-2026-0001");

    await user.click(screen.getByRole("tab", { name: "CAPAs" }));
    await waitFor(() => expect(lastRecordParams().type).toBe("Capa"));
    expect(lastRecordParams().page).toBe(1);

    await user.click(screen.getByRole("tab", { name: "Complaints" }));
    await waitFor(() => expect(lastRecordParams().type).toBe("Complaint"));

    await user.click(screen.getByRole("tab", { name: "All" }));
    await waitFor(() => expect(lastRecordParams().type).toBeUndefined());
  });

  it("cancels the request of the tab that was left", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText("DEV-2026-0001");
    const firstSignal = recordCalls()[0][1].signal as AbortSignal;

    await user.click(screen.getByRole("tab", { name: "Deviations" }));

    await waitFor(() => expect(firstSignal.aborted).toBe(true));
  });

  it("offers the statuses of the chosen type and sends the chosen one", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText("DEV-2026-0001");
    await user.click(screen.getByRole("tab", { name: "CAPAs" }));

    await user.click(screen.getByRole("combobox", { name: "Status" }));
    const options = screen.getAllByRole("option").map((o) => o.textContent);
    expect(options).toEqual(["All statuses", "Plan", "Plan Approved", "Completion Verified", "Closed", "Cancelled"]);
    await user.click(screen.getByRole("option", { name: "Plan Approved" }));

    await waitFor(() => expect(lastRecordParams().status).toBe("PlanApproved"));
    expect(lastRecordParams().type).toBe("Capa");

    await user.click(screen.getByRole("tab", { name: "Deviations" }));
    await waitFor(() => expect(lastRecordParams().type).toBe("Deviation"));
    expect(lastRecordParams().status).toBeUndefined();
  });

  it("filters by classification and section", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText("DEV-2026-0001");

    await user.click(await screen.findByRole("combobox", { name: "Classification" }));
    await user.click(await screen.findByRole("option", { name: "Major" }));
    await waitFor(() => expect(lastRecordParams().classificationId).toBe(2));

    await user.click(screen.getByRole("combobox", { name: "Section" }));
    await user.click(await screen.findByRole("option", { name: "Finished Product" }));
    await waitFor(() => expect(lastRecordParams().sectionId).toBe(2));
    expect(lastRecordParams().classificationId).toBe(2);
  });

  it("filters by owner (my records), overdue and the raised-date range", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText("DEV-2026-0001");

    await user.click(screen.getByRole("checkbox", { name: "Owned by me" }));
    await waitFor(() => expect(lastRecordParams().ownerUserId).toBe(8));

    await user.click(screen.getByRole("checkbox", { name: "Overdue only" }));
    await waitFor(() => expect(lastRecordParams().overdue).toBe(true));

    await user.type(screen.getByLabelText("Raised from"), "2026-11-01");
    await waitFor(() => expect(lastRecordParams().from).toBe("2026-11-01T00:00:00.000Z"));
    await user.type(screen.getByLabelText("Raised to"), "2026-11-30");
    await waitFor(() => expect(lastRecordParams().to).toBe("2026-11-30T23:59:59.999Z"));
  });

  it("clears every filter at once", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText("DEV-2026-0001");
    await user.click(screen.getByRole("checkbox", { name: "Overdue only" }));
    await waitFor(() => expect(lastRecordParams().overdue).toBe(true));

    await user.click(screen.getByRole("button", { name: "Clear filters" }));

    await waitFor(() => expect(lastRecordParams().overdue).toBeUndefined());
    expect((screen.getByRole("checkbox", { name: "Overdue only" }) as HTMLInputElement).checked).toBe(false);
    expect(screen.queryByRole("button", { name: "Clear filters" })).toBeNull();
  });

  it("pages on the server: the next page is requested, not sliced on the client", async () => {
    const user = userEvent.setup();
    serve({
      records: (params) =>
        Promise.resolve(pageOf([listItem({ id: Number(params.page) * 100, number: `DEV-2026-${String(params.page).padStart(4, "0")}` })], 60, Number(params.page)))
    });
    renderPage();
    await screen.findByText("DEV-2026-0001");

    await user.click(screen.getByRole("button", { name: /next page/i }));

    await waitFor(() => expect(lastRecordParams().page).toBe(2));
    expect(await screen.findByText("DEV-2026-0002")).toBeTruthy();
  });

  it("lists each record with its type, status, classification, section, owner and due date, and says when it is overdue", async () => {
    serve({
      records: () =>
        Promise.resolve(
          pageOf([
            listItem({ id: 1, number: "DEV-2026-0001", status: "Investigation", isOverdue: true }),
            listItem({ id: 2, number: "CAPA-2026-0003", type: "Capa", title: "Replace the probe", status: "PlanApproved", classificationName: null, ownerName: null, dueDate: null })
          ])
        )
    });
    renderPage();

    const first = (await screen.findByText("DEV-2026-0001")).closest("tr") as HTMLElement;
    expect(within(first).getByText("Deviation")).toBeTruthy();
    expect(within(first).getByText("Incubator temperature drift")).toBeTruthy();
    expect(within(first).getByText("Investigation")).toBeTruthy();
    expect(within(first).getByText("Major")).toBeTruthy();
    expect(within(first).getByText("Microbiology")).toBeTruthy();
    expect(within(first).getByText("Sam Head")).toBeTruthy();
    expect(within(first).getByText("2026-12-01")).toBeTruthy();
    expect(within(first).getByText("Overdue")).toBeTruthy();

    const second = screen.getByText("CAPA-2026-0003").closest("tr") as HTMLElement;
    expect(within(second).getByText("Plan Approved")).toBeTruthy();
    expect(within(second).queryByText("Overdue")).toBeNull();
  });

  it("shows a cancelled record as cancelled whatever its last status was", async () => {
    serve({ records: () => Promise.resolve(pageOf([listItem({ status: "Investigation", isCancelled: true })])) });
    renderPage();

    const row = (await screen.findByText("DEV-2026-0001")).closest("tr") as HTMLElement;
    expect(within(row).getByText("Cancelled")).toBeTruthy();
    expect(within(row).queryByText("Investigation")).toBeNull();
  });

  it("opens the record when its row is chosen", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByText("DEV-2026-0001"));

    expect(await screen.findByText("Record page opened")).toBeTruthy();
  });

  it("announces how many records are shown", async () => {
    serve({ records: () => Promise.resolve(pageOf([listItem(), listItem({ id: 2, number: "DEV-2026-0002" })], 2)) });
    renderPage();

    expect(await screen.findByText("Showing 2 of 2 records.")).toBeTruthy();
  });

  it("shows a loading state while the first page is read", () => {
    serve({ records: () => new Promise(() => {}) });
    renderPage();

    expect(document.querySelectorAll(".MuiSkeleton-root").length).toBeGreaterThan(0);
  });

  it("shows an empty state that names the cause", async () => {
    serve({ records: () => Promise.resolve(pageOf([])) });
    renderPage();

    expect(await screen.findByText("No quality records found")).toBeTruthy();
    expect(screen.getByText("No record matches the selected tab and filters.")).toBeTruthy();
  });

  it("shows the server's message when the register cannot be read, and reads it again on Retry", async () => {
    const user = userEvent.setup();
    let calls = 0;
    serve({
      records: () => {
        calls += 1;
        return calls === 1 ? Promise.reject(refusal("You may not view quality records.")) : Promise.resolve(pageOf([listItem()]));
      }
    });
    renderPage();

    expect((await screen.findByRole("alert")).textContent).toContain("You may not view quality records.");
    await user.click(screen.getByRole("button", { name: "Retry" }));

    expect(await screen.findByText("DEV-2026-0001")).toBeTruthy();
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("keeps the register usable and says so when the filter options cannot be loaded", async () => {
    serve({ classifications: () => Promise.reject(refusal("Classifications are unavailable.")) });
    renderPage();

    expect(await screen.findByText("DEV-2026-0001")).toBeTruthy();
    expect(await screen.findByText("Some data could not be loaded: classifications.")).toBeTruthy();
    expect(screen.getByRole("tab", { name: "CAPAs" })).toBeTruthy();
  });
});

describe("QualityRegisterPage in Arabic", () => {
  afterEach(async () => {
    cleanup();
    localStorage.clear();
    vi.clearAllMocks();
    await resetLanguage();
  });

  it("shows the title, tabs, filters and the status in Arabic", async () => {
    setAuth();
    serve();
    await renderInArabic(
      <AuthProvider>
        <MemoryRouter initialEntries={["/quality"]}>
          <QualityRegisterPage />
        </MemoryRouter>
      </AuthProvider>,
      ["common", "quality"]
    );

    expect(await screen.findByRole("heading", { level: 1, name: "أحداث الجودة" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "الانحرافات" })).toBeTruthy();
    expect(screen.getByRole("checkbox", { name: "المتأخرة فقط" })).toBeTruthy();
    expect(await screen.findByText("DEV-2026-0001")).toBeTruthy();
  });
});
