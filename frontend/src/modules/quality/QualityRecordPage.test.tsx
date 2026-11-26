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

import { QualityRecordPage } from "./QualityRecordPage";
import { registerQualityTypeActions, type QualityTypeActionsProps } from "./typeActions";
import { actionDetail, attachment, comment, extension, link, recordDetail, refusal, timelineEntry } from "./testFixtures";
import type {
  QualityCommentDto,
  QualityExtensionDto,
  QualityLinkDto,
  QualityRecordDetailDto
} from "../../services/qualityService";

const ok = <T,>(data: T) => Promise.resolve({ data: { success: true, data } });

interface Backend {
  detail?: () => Promise<unknown>;
  links?: () => Promise<unknown>;
  comments?: () => Promise<unknown>;
  extensions?: () => Promise<unknown>;
}

function serve(backend: Backend = {}) {
  http.get.mockImplementation((url: string) => {
    if (url === "/quality/records/1") return (backend.detail ?? (() => ok(recordDetail())))();
    if (url === "/quality/1/links") return (backend.links ?? (() => ok<QualityLinkDto[]>([link()])))();
    if (url === "/quality/1/comments") return (backend.comments ?? (() => ok<QualityCommentDto[]>([comment()])))();
    if (url === "/quality/1/extensions") return (backend.extensions ?? (() => ok<QualityExtensionDto[]>([])))();
    if (url === "/quality/records/1/pdf") return Promise.resolve({ data: new Blob(["%PDF"]) });
    return Promise.reject(new Error(`unexpected GET ${url}`));
  });
}

function setAuth(permissions: string[], userId = 8) {
  localStorage.setItem("microlims_username", "shead");
  localStorage.setItem("microlims_role", "SectionHead");
  localStorage.setItem("microlims_full_name", "Sam Head");
  localStorage.setItem("microlims_user_id", String(userId));
  localStorage.setItem("microlims_permissions", JSON.stringify(permissions));
}

function renderPage() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={["/quality/1"]}>
          <Routes>
            <Route path="/quality/:id" element={<QualityRecordPage />} />
            <Route path="/quality" element={<p>Register page</p>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const VIEW = ["Quality.View"];
const RAISE = ["Quality.View", "Quality.Raise"];
const APPROVE = ["Quality.View", "Quality.Raise", "Quality.Approve"];

describe("QualityRecordPage", () => {
  beforeEach(() => {
    serve();
    http.post.mockResolvedValue({ data: { data: {} } });
    URL.createObjectURL = vi.fn(() => "blob:quality");
    URL.revokeObjectURL = vi.fn();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.clearAllMocks();
  });

  it("shows a loading state while the record is read", () => {
    http.get.mockImplementation(() => new Promise(() => {}));
    setAuth(VIEW);
    renderPage();

    expect(screen.getByLabelText("Loading the quality record")).toBeTruthy();
  });

  it("shows the record header: number, title, status, type, classification, owner and due date", async () => {
    setAuth(VIEW);
    renderPage();

    expect(await screen.findByRole("heading", { level: 1, name: /DEV-2026-0001/ })).toBeTruthy();
    expect(screen.getByText("Incubator temperature drift")).toBeTruthy();
    const header = screen.getByRole("region", { name: "Record summary" });
    expect(within(header).getByText("Deviation")).toBeTruthy();
    expect(within(header).getByText("Investigation")).toBeTruthy();
    expect(within(header).getByText("Major")).toBeTruthy();
    expect(within(header).getByText("Sam Head")).toBeTruthy();
    expect(within(header).getByText("2026-12-01")).toBeTruthy();
    expect(screen.getByText("Incubator 3 held 37.8 C for six hours.")).toBeTruthy();
  });

  it("shows the path stepper with the current step announced", async () => {
    setAuth(VIEW);
    renderPage();

    await screen.findByRole("heading", { level: 1 });
    expect(screen.getByText("Current step: Investigation (step 2 of 3)")).toBeTruthy();
    expect(document.querySelector("[aria-current='step']")?.textContent).toContain("Investigation");
  });

  it("shows every signature with the signer's full name, the UTC time with its zone and the meaning", async () => {
    serve({
      detail: () =>
        ok(
          recordDetail({
            timeline: [
              timelineEntry({ stepId: 1, meaningText: "Deviation assessed", signerFullName: "Sam Head", signedAtUtcText: "2026-11-21 10:15:00 UTC" }),
              timelineEntry({
                stepId: 2,
                kind: "DeviationLabConclusion",
                meaningText: "Laboratory conclusion signed",
                signerFullName: "Ann Analyst",
                role: "Analyst",
                signedAtUtcText: "2026-11-24 15:40:12 UTC",
                comment: "Probe failed its check."
              })
            ]
          })
        )
    });
    setAuth(VIEW);
    renderPage();

    const timeline = await screen.findByRole("region", { name: "Signed Timeline" });
    const entries = within(timeline).getAllByRole("listitem");
    expect(entries).toHaveLength(2);
    expect(within(entries[0]).getByText("Deviation assessed")).toBeTruthy();
    expect(within(entries[0]).getByText(/Sam Head \(Section Head\) · 2026-11-21 10:15:00 UTC/)).toBeTruthy();
    expect(within(entries[1]).getByText("Laboratory conclusion signed")).toBeTruthy();
    expect(within(entries[1]).getByText(/Ann Analyst \(Analyst\) · 2026-11-24 15:40:12 UTC/)).toBeTruthy();
    expect(within(entries[1]).getByText("Probe failed its check.")).toBeTruthy();
  });

  it("tells a user who may only view that the page is read-only and offers no way to change anything", async () => {
    serve({
      detail: () => ok(recordDetail({ attachments: [attachment()] })),
      extensions: () => ok([extension()])
    });
    setAuth(VIEW);
    renderPage();

    expect(await screen.findByText(/Read-only view/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Add link" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Upload" })).toBeNull();
    expect(screen.queryByLabelText("Add a comment")).toBeNull();
    expect(screen.queryByRole("button", { name: /^Decide/ })).toBeNull();
    expect(screen.getByRole("button", { name: /Download temperature-log.pdf/ })).toBeTruthy();
    expect(screen.getByRole("button", { name: /Export PDF/ })).toBeTruthy();
  });

  it("lets a user with Quality.Raise link, upload and comment, and shows no read-only notice", async () => {
    setAuth(RAISE);
    renderPage();

    expect(await screen.findByRole("button", { name: "Add link" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Upload" })).toBeTruthy();
    expect(screen.getByLabelText("Add a comment")).toBeTruthy();
    expect(screen.queryByText(/Read-only view/)).toBeNull();
  });

  it("offers the owner a request for more time and an approver the decision on a pending request", async () => {
    serve({ extensions: () => ok([extension({ requestedDueDate: "2026-12-20" })]) });
    setAuth(APPROVE, 8);
    renderPage();

    expect(await screen.findByRole("button", { name: "Request extension" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Decide the extension request to 2026-12-20" })).toBeTruthy();
  });

  it("does not offer a request for more time to someone who does not own the record", async () => {
    setAuth(APPROVE, 99);
    renderPage();

    await screen.findByRole("heading", { level: 1 });
    expect(screen.queryByRole("button", { name: "Request extension" })).toBeNull();
  });

  it("opens the extension request for an action the signed-in person is responsible for", async () => {
    const user = userEvent.setup();
    serve({
      detail: () =>
        ok(recordDetail({ type: "Capa", deviation: null, actions: [actionDetail({ id: 11, description: "Recalibrate the incubator probe", responsibleUserId: 8 })] }))
    });
    setAuth(RAISE, 8);
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Request an extension for action: Recalibrate the incubator probe" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("For action: Recalibrate the incubator probe")).toBeTruthy();
  });

  it("shows a partial-outage banner when one panel's request fails and keeps the rest of the page", async () => {
    serve({ comments: () => Promise.reject(refusal("Comments are unavailable.")) });
    setAuth(RAISE);
    renderPage();

    expect(await screen.findByText("Some data could not be loaded: comments.")).toBeTruthy();
    expect(screen.getByText("Comments could not be loaded.")).toBeTruthy();
    expect(screen.getByText("Amoxicillin 500mg")).toBeTruthy();
    expect(screen.getByRole("heading", { level: 1, name: /DEV-2026-0001/ })).toBeTruthy();
  });

  it("retries only the panel that failed", async () => {
    const user = userEvent.setup();
    let commentCalls = 0;
    serve({
      comments: () => {
        commentCalls += 1;
        return commentCalls === 1 ? Promise.reject(refusal("Comments are unavailable.")) : ok([comment({ text: "Back again." })]);
      }
    });
    setAuth(VIEW);
    renderPage();

    await screen.findByText("Some data could not be loaded: comments.");
    http.get.mockClear();
    serve({ comments: () => ok([comment({ text: "Back again." })]) });
    await user.click(screen.getByRole("button", { name: "Retry" }));

    expect(await screen.findByText("Back again.")).toBeTruthy();
    expect(screen.queryByText(/Some data could not be loaded/)).toBeNull();
    const urls = http.get.mock.calls.map((call) => call[0]);
    expect(urls).toEqual(["/quality/1/comments"]);
  });

  it("shows the server's message and a way back when the record cannot be read", async () => {
    serve({ detail: () => Promise.reject(refusal("The quality record could not be found.")) });
    setAuth(VIEW);
    renderPage();

    expect((await screen.findByRole("alert")).textContent).toContain("The quality record could not be found.");
    expect(screen.getByRole("link", { name: "Back to Quality Events" }).getAttribute("href")).toBe("/quality");
  });

  it("marks an overdue record in words and shows why a cancelled record was cancelled", async () => {
    serve({ detail: () => ok(recordDetail({ isOverdue: true, isCancelled: true, cancelReason: "Raised twice by mistake." })) });
    setAuth(VIEW);
    renderPage();

    const header = await screen.findByRole("region", { name: "Record summary" });
    expect(within(header).getByText("Overdue")).toBeTruthy();
    expect(within(header).getByText("Cancelled")).toBeTruthy();
    expect(screen.getByText("This record was cancelled. Reason: Raised twice by mistake.")).toBeTruthy();
  });

  it("renders the step forms registered for the record's type in the actions slot", async () => {
    const received: QualityTypeActionsProps[] = [];
    const unregister = registerQualityTypeActions("Deviation", (props) => {
      received.push(props);
      return <button type="button">Assess deviation</button>;
    });
    setAuth(["Quality.View", "Quality.Investigate"], 8);
    renderPage();

    const slot = await screen.findByRole("region", { name: "Actions for this record" });
    expect(within(slot).getByRole("button", { name: "Assess deviation" })).toBeTruthy();
    const props = received[received.length - 1];
    expect(props.record.id).toBe(1);
    expect(props.record.version).toBe(3);
    expect(props.permissions).toEqual({ raise: false, investigate: true, labConclude: false, approve: false });
    expect(props.currentUserId).toBe(8);
    unregister();
  });

  it("shows no actions slot when no step forms are registered for the type", async () => {
    setAuth(VIEW);
    renderPage();

    await screen.findByRole("heading", { level: 1 });
    expect(screen.queryByRole("region", { name: "Actions for this record" })).toBeNull();
  });

  it("reloads the record with its new version after a change and sends that version next time", async () => {
    const user = userEvent.setup();
    let version = 3;
    serve({ detail: () => ok(recordDetail({ version })) });
    http.post.mockImplementation(() => {
      version = 4;
      return ok(comment());
    });
    setAuth(RAISE);
    renderPage();

    await user.type(await screen.findByLabelText("Add a comment"), "First note.");
    await user.click(screen.getByRole("button", { name: "Post comment" }));
    await waitFor(() => expect(http.post).toHaveBeenCalledTimes(1));
    expect(http.post).toHaveBeenLastCalledWith("/quality/1/comments", { text: "First note." }, { headers: { "If-Match": '"3"' } });

    await waitFor(() => {
      const detailCalls = http.get.mock.calls.filter((call) => call[0] === "/quality/records/1");
      expect(detailCalls.length).toBe(2);
    });
    await user.type(screen.getByLabelText("Add a comment"), "Second note.");
    await user.click(screen.getByRole("button", { name: "Post comment" }));
    await waitFor(() => expect(http.post).toHaveBeenCalledTimes(2));
    expect(http.post).toHaveBeenLastCalledWith("/quality/1/comments", { text: "Second note." }, { headers: { "If-Match": '"4"' } });
  });

  it("downloads the record as a PDF", async () => {
    const user = userEvent.setup();
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
    setAuth(VIEW);
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Export PDF" }));

    await waitFor(() => expect(http.get).toHaveBeenCalledWith("/quality/records/1/pdf", { responseType: "blob" }));
    await waitFor(() => expect(click).toHaveBeenCalledTimes(1));
    click.mockRestore();
  });

  it("cancels the requests when the page is left", async () => {
    setAuth(VIEW);
    const { unmount } = renderPage();
    await screen.findByRole("heading", { level: 1 });

    const signals = http.get.mock.calls.map((call) => (call[1] as { signal?: AbortSignal }).signal);
    expect(signals.length).toBeGreaterThanOrEqual(4);
    expect(signals.every((s) => s instanceof AbortSignal && !s.aborted)).toBe(true);
    unmount();
    expect(signals.every((s) => s?.aborted)).toBe(true);
  });
});

describe("QualityRecordPage detail types", () => {
  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.clearAllMocks();
  });

  it("shows the type's facts for a CAPA with its source deviation", async () => {
    const capa: QualityRecordDetailDto = recordDetail({
      type: "Capa",
      number: "CAPA-2026-0003",
      deviation: null,
      capa: {
        status: "PlanApproved",
        sourceRecordId: 1,
        sourceRecordNumber: "DEV-2026-0001",
        effectivenessCriteria: "No drift for 90 days",
        effectivenessMethod: null,
        effectivenessPlannedDate: null,
        effectivenessResult: null,
        effectivenessFindings: null,
        successorCapaId: null,
        successorCapaNumber: null
      }
    });
    serve({ detail: () => ok(capa) });
    setAuth(VIEW);
    renderPage();

    expect(await screen.findByRole("link", { name: "DEV-2026-0001" })).toBeTruthy();
    expect(screen.getByText("No drift for 90 days")).toBeTruthy();
  });
});

describe("QualityRecordPage in Arabic", () => {
  afterEach(async () => {
    cleanup();
    localStorage.clear();
    vi.clearAllMocks();
    await resetLanguage();
  });

  it("shows the panels and the path announcement in Arabic and keeps the record number readable", async () => {
    setAuth(RAISE);
    serve();
    await renderInArabic(
      <AuthProvider>
        <MemoryRouter initialEntries={["/quality/1"]}>
          <Routes>
            <Route path="/quality/:id" element={<QualityRecordPage />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
      ["common", "quality"]
    );

    expect(await screen.findByRole("heading", { level: 1, name: /DEV-2026-0001/ })).toBeTruthy();
    expect(screen.getByRole("group", { name: "مسار السجل" })).toBeTruthy();
    expect(screen.getByText("الخطوة الحالية: Investigation (الخطوة 2 من 3)")).toBeTruthy();
    expect(screen.getByRole("button", { name: "إضافة رابط" })).toBeTruthy();
  });
});
