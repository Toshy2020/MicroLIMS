import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { renderInArabic, resetLanguage } from "../../../i18n/testUtils";
import { AttachmentsPanel } from "./AttachmentsPanel";
import { OosTrackingService, type OosInvestigationDocument } from "../services/OosTrackingService";

vi.mock("../services/OosTrackingService", () => ({
  OosTrackingService: {
    getDocuments: vi.fn(),
    uploadDocument: vi.fn(),
    downloadDocument: vi.fn(),
    supersedeDocument: vi.fn(),
    voidDocument: vi.fn()
  }
}));

const doc = (overrides: Partial<OosInvestigationDocument> = {}): OosInvestigationDocument => ({
  id: 1,
  oosGroupCode: "OOS1126042",
  originalFileName: "lab-report.pdf",
  fileExtension: ".pdf",
  contentType: "application/pdf",
  fileSizeBytes: 2048,
  contentSha256: "AB12",
  uploadedByUserId: 3,
  uploadedByName: "Rita Reviewer",
  uploadedAt: "2026-11-20T09:00:00Z",
  status: "Current",
  supersededByDocumentId: null,
  supersededAt: null,
  supersededByUserId: null,
  supersessionReason: null,
  voidedAt: null,
  voidedByUserId: null,
  voidReason: null,
  ...overrides
});

const pdf = (name = "evidence.pdf") => new File(["%PDF-1.4"], name, { type: "application/pdf" });

function renderPanel(canManage: boolean) {
  return render(
    <PinnedLightTheme>
      <AttachmentsPanel oosGroupCode="OOS1126042" canManage={canManage} />
    </PinnedLightTheme>
  );
}

const refusal = (message: string) =>
  Object.assign(new Error("Request failed"), { isAxiosError: true, response: { data: { message } } });

describe("OOS investigation AttachmentsPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(OosTrackingService.getDocuments).mockResolvedValue([
      doc(),
      doc({
        id: 2,
        originalFileName: "old-report.pdf",
        status: "Superseded",
        supersessionReason: "Wrong batch attached"
      }),
      doc({
        id: 3,
        originalFileName: "scan.png",
        status: "Voided",
        voidReason: "Uploaded to the wrong investigation"
      })
    ]);
  });

  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("lists every document with its status in words and the stated reasons", async () => {
    renderPanel(true);

    expect(await screen.findByText("lab-report.pdf")).toBeTruthy();
    expect(OosTrackingService.getDocuments).toHaveBeenCalledWith("OOS1126042", expect.anything());
    const rows = screen.getAllByRole("row").slice(1);
    expect(within(rows[0]).getByText("Current")).toBeTruthy();
    expect(within(rows[1]).getByText("Superseded")).toBeTruthy();
    expect(within(rows[1]).getByText("Superseded: Wrong batch attached")).toBeTruthy();
    expect(within(rows[2]).getByText("Voided")).toBeTruthy();
    expect(within(rows[2]).getByText("Voided: Uploaded to the wrong investigation")).toBeTruthy();
  });

  it("is read-only without Oos.Manage and says why", async () => {
    renderPanel(false);

    expect(await screen.findByText("lab-report.pdf")).toBeTruthy();
    expect(screen.queryByLabelText(/^Upload file\. Accepted formats/)).toBeNull();
    expect(screen.queryByRole("button", { name: /^Supersede/ })).toBeNull();
    expect(screen.queryByRole("button", { name: /^Void/ })).toBeNull();
    expect(screen.getByRole("button", { name: "Download lab-report.pdf" })).toBeTruthy();
    expect(screen.getByText(/needs the Oos\.Manage permission/)).toBeTruthy();
  });

  it("shows an empty state when nothing is attached", async () => {
    vi.mocked(OosTrackingService.getDocuments).mockResolvedValue([]);
    renderPanel(true);

    expect(await screen.findByText("No documents are attached to this investigation yet.")).toBeTruthy();
  });

  it("shows a load failure with the server's reason and retries", async () => {
    const user = userEvent.setup();
    vi.mocked(OosTrackingService.getDocuments).mockRejectedValueOnce(refusal("You may not read this investigation."));
    renderPanel(true);

    expect((await screen.findByRole("alert")).textContent).toContain("You may not read this investigation.");
    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByText("lab-report.pdf")).toBeTruthy();
  });

  it("uploads a file, then reloads the list", async () => {
    const user = userEvent.setup();
    vi.mocked(OosTrackingService.uploadDocument).mockResolvedValue(doc({ id: 9, originalFileName: "evidence.pdf" }));
    renderPanel(true);
    await screen.findByText("lab-report.pdf");

    const file = pdf();
    await user.upload(screen.getByLabelText(/^Upload file\. Accepted formats/), file);
    await user.click(screen.getByRole("button", { name: "Upload" }));

    await waitFor(() => expect(OosTrackingService.uploadDocument).toHaveBeenCalledWith("OOS1126042", file));
    await waitFor(() => expect(OosTrackingService.getDocuments).toHaveBeenCalledTimes(2));
  });

  it("shows the server's allowlist or size refusal and keeps the list", async () => {
    const user = userEvent.setup();
    vi.mocked(OosTrackingService.uploadDocument).mockRejectedValue(
      refusal("The file is larger than the 25 MB limit.")
    );
    renderPanel(true);
    await screen.findByText("lab-report.pdf");

    await user.upload(screen.getByLabelText(/^Upload file\. Accepted formats/), pdf("huge.pdf"));
    await user.click(screen.getByRole("button", { name: "Upload" }));

    expect((await screen.findByRole("alert")).textContent).toContain("larger than the 25 MB limit");
    expect(screen.getByText("lab-report.pdf")).toBeTruthy();
  });

  it("downloads a document by id and group", async () => {
    const user = userEvent.setup();
    const blob = new Blob(["x"]);
    vi.mocked(OosTrackingService.downloadDocument).mockResolvedValue(blob);
    const createUrl = vi.fn(() => "blob:test");
    Object.defineProperty(window.URL, "createObjectURL", { configurable: true, value: createUrl });
    Object.defineProperty(window.URL, "revokeObjectURL", { configurable: true, value: vi.fn() });
    renderPanel(true);
    await screen.findByText("lab-report.pdf");

    await user.click(screen.getByRole("button", { name: "Download lab-report.pdf" }));

    await waitFor(() => expect(OosTrackingService.downloadDocument).toHaveBeenCalledWith(1, "OOS1126042"));
    await waitFor(() => expect(createUrl).toHaveBeenCalledWith(blob));
  });

  it("supersedes a current document only with a replacement file and a reason", async () => {
    const user = userEvent.setup();
    vi.mocked(OosTrackingService.supersedeDocument).mockResolvedValue(doc({ id: 10 }));
    renderPanel(true);
    await screen.findByText("lab-report.pdf");

    expect(screen.queryByRole("button", { name: "Supersede old-report.pdf" })).toBeNull();
    await user.click(screen.getByRole("button", { name: "Supersede lab-report.pdf" }));
    const dialog = await screen.findByRole("dialog");
    const submit = within(dialog).getByRole("button", { name: "Supersede" });
    expect(submit.hasAttribute("disabled")).toBe(true);

    const replacement = pdf("replacement.pdf");
    await user.upload(within(dialog).getByLabelText(/^Upload file\. Accepted formats/), replacement);
    expect(submit.hasAttribute("disabled")).toBe(true);
    await user.type(within(dialog).getByLabelText(/Reason for supersession/), "Corrected batch");
    expect(submit.hasAttribute("disabled")).toBe(false);
    await user.click(submit);

    await waitFor(() =>
      expect(OosTrackingService.supersedeDocument).toHaveBeenCalledWith(1, "OOS1126042", replacement, "Corrected batch")
    );
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });

  it("voids a document only with a reason, and not one already voided", async () => {
    const user = userEvent.setup();
    vi.mocked(OosTrackingService.voidDocument).mockResolvedValue(doc({ status: "Voided" }));
    renderPanel(true);
    await screen.findByText("lab-report.pdf");

    expect(screen.queryByRole("button", { name: "Void scan.png" })).toBeNull();
    await user.click(screen.getByRole("button", { name: "Void lab-report.pdf" }));
    const dialog = await screen.findByRole("dialog");
    const submit = within(dialog).getByRole("button", { name: "Void document" });
    expect(submit.hasAttribute("disabled")).toBe(true);

    await user.type(within(dialog).getByLabelText(/Reason for voiding/), "Wrong investigation");
    await user.click(submit);

    await waitFor(() =>
      expect(OosTrackingService.voidDocument).toHaveBeenCalledWith(1, "OOS1126042", "Wrong investigation")
    );
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });

  it("keeps the void dialog open and shows the server's refusal", async () => {
    const user = userEvent.setup();
    vi.mocked(OosTrackingService.voidDocument).mockRejectedValue(refusal("This document is already voided."));
    renderPanel(true);
    await screen.findByText("lab-report.pdf");

    await user.click(screen.getByRole("button", { name: "Void lab-report.pdf" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/Reason for voiding/), "Duplicate");
    await user.click(within(dialog).getByRole("button", { name: "Void document" }));

    expect(await within(dialog).findByText("This document is already voided.")).toBeTruthy();
  });

  it("renders in Arabic", async () => {
    await renderInArabic(<AttachmentsPanel oosGroupCode="OOS1126042" canManage />, ["common", "oosTracking"]);

    expect(await screen.findByText("lab-report.pdf")).toBeTruthy();
    expect(screen.getByRole("heading", { name: "المرفقات" })).toBeTruthy();
  });
});
