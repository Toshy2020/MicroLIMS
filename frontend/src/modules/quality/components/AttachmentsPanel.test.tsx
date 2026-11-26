import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn()
}));
vi.mock("../../../services/apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/apiClient")>();
  return { ...actual, apiClient: http };
});

import { AttachmentsPanel } from "./AttachmentsPanel";
import { attachment, networkFailure, refusal } from "../testFixtures";
import type { QualityAttachmentDto } from "../../../services/qualityService";

function renderPanel(options: { attachments?: QualityAttachmentDto[]; canEdit?: boolean; canRemove?: boolean; onChanged?: () => Promise<void> } = {}) {
  const onChanged = options.onChanged ?? vi.fn().mockResolvedValue(undefined);
  render(
    <PinnedLightTheme>
      <AttachmentsPanel
        recordId={5}
        recordVersion={6}
        attachments={options.attachments ?? [attachment()]}
        canEdit={options.canEdit ?? true}
        canRemove={options.canRemove ?? true}
        onChanged={onChanged}
      />
    </PinnedLightTheme>
  );
  return { onChanged };
}

const pdf = () => new File(["%PDF-1.7"], "deviation-photo.pdf", { type: "application/pdf" });

describe("AttachmentsPanel", () => {
  let anchorClick: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    anchorClick = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
    http.post.mockResolvedValue({ data: { data: attachment() } });
    http.get.mockResolvedValue({ data: new Blob(["x"]) });
    URL.createObjectURL = vi.fn(() => "blob:quality");
    URL.revokeObjectURL = vi.fn();
  });

  afterEach(() => {
    cleanup();
    anchorClick.mockRestore();
    vi.clearAllMocks();
  });

  it("lists each attachment with its size, uploader and upload time with the time zone", () => {
    renderPanel({
      attachments: [
        attachment({
          fileName: "temperature-log.pdf",
          size: 2048,
          uploadedByName: "Ann Analyst",
          uploadedAtUtc: "2026-11-21T09:00:00Z"
        })
      ]
    });

    const item = screen.getByRole("listitem");
    expect(within(item).getByText("temperature-log.pdf")).toBeTruthy();
    expect(within(item).getByText(/2\.0 KB · uploaded by Ann Analyst · .*21 Nov 2026.*\d{2}:\d{2}\s+\S+/)).toBeTruthy();
  });

  it("shows a voided attachment as voided with its voider and reason, and cannot be voided again", () => {
    renderPanel({
      attachments: [
        attachment({
          id: 22,
          fileName: "wrong.pdf",
          isVoided: true,
          voidedAtUtc: "2026-11-22T10:00:00Z",
          voidedByUserId: 8,
          voidedByName: "Sam Head",
          voidReason: "Wrong batch"
        })
      ]
    });

    expect(screen.getByText("Voided")).toBeTruthy();
    expect(screen.getByText(/Voided by Sam Head · .*22 Nov 2026.*: Wrong batch/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Void wrong.pdf" })).toBeNull();
    expect(screen.getByRole("button", { name: "Download wrong.pdf" })).toBeTruthy();
  });

  it("uploads the chosen file with the record version, then reloads", async () => {
    const user = userEvent.setup();
    const { onChanged } = renderPanel({ attachments: [] });
    const upload = screen.getByRole("button", { name: "Upload" }) as HTMLButtonElement;
    expect(upload.disabled).toBe(true);

    const file = pdf();
    await user.upload(screen.getByLabelText(/^Upload file\. Accepted formats/), file);
    expect(screen.getByText("deviation-photo.pdf")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Upload" }));

    await waitFor(() => expect(http.post).toHaveBeenCalledTimes(1));
    const [url, body, config] = http.post.mock.calls[0] as [string, FormData, unknown];
    expect(url).toBe("/quality/5/attachments");
    expect((body.get("file") as File).name).toBe("deviation-photo.pdf");
    expect(config).toEqual({ headers: { "If-Match": '"6"' } });
    await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    expect(screen.queryByText("deviation-photo.pdf")).toBeNull();
  });

  it("shows the server's refusal of a file and keeps the file so another can be chosen", async () => {
    const user = userEvent.setup();
    http.post.mockRejectedValue(refusal("This file type is not allowed."));
    const { onChanged } = renderPanel({ attachments: [] });

    await user.upload(screen.getByLabelText(/^Upload file\. Accepted formats/), pdf());
    await user.click(screen.getByRole("button", { name: "Upload" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("This file type is not allowed.");
    expect(screen.getByText("deviation-photo.pdf")).toBeTruthy();
    expect(onChanged).not.toHaveBeenCalled();
  });

  it("explains a failed connection in words", async () => {
    const user = userEvent.setup();
    http.post.mockRejectedValue(networkFailure());
    renderPanel({ attachments: [] });

    await user.upload(screen.getByLabelText(/^Upload file\. Accepted formats/), pdf());
    await user.click(screen.getByRole("button", { name: "Upload" }));

    expect((await screen.findByRole("alert")).textContent).toContain("Could not reach the MicroLIMS server");
  });

  it("voids an attachment only with a reason, sending the record version", async () => {
    const user = userEvent.setup();
    const { onChanged } = renderPanel();

    await user.click(screen.getByRole("button", { name: "Void temperature-log.pdf" }));
    const dialog = await screen.findByRole("dialog");
    const submit = within(dialog).getByRole("button", { name: "Void attachment" }) as HTMLButtonElement;
    expect(submit.disabled).toBe(true);

    await user.type(within(dialog).getByLabelText(/^Reason for voiding/), "Uploaded to the wrong record");
    await user.click(submit);

    await waitFor(() => {
      expect(http.post).toHaveBeenCalledWith(
        "/quality/5/attachments/21/void",
        { reason: "Uploaded to the wrong record" },
        { headers: { "If-Match": '"6"' } }
      );
    });
    await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
  });

  it("keeps the void dialog open with the server's message when voiding is refused", async () => {
    const user = userEvent.setup();
    http.post.mockRejectedValue(refusal("This attachment is already voided."));
    renderPanel();

    await user.click(screen.getByRole("button", { name: "Void temperature-log.pdf" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^Reason for voiding/), "Duplicate");
    await user.click(within(dialog).getByRole("button", { name: "Void attachment" }));

    expect(await within(dialog).findByText("This attachment is already voided.")).toBeTruthy();
    expect(screen.getByRole("dialog")).toBeTruthy();
  });

  it("downloads an attachment through the API", async () => {
    const user = userEvent.setup();
    renderPanel();

    await user.click(screen.getByRole("button", { name: "Download temperature-log.pdf" }));

    await waitFor(() => {
      expect(http.get).toHaveBeenCalledWith("/quality/5/attachments/21", { responseType: "blob" });
    });
    await waitFor(() => expect(anchorClick).toHaveBeenCalledTimes(1));
  });

  it("tells the user when a download fails", async () => {
    const user = userEvent.setup();
    http.get.mockRejectedValue(refusal("You may not open this attachment."));
    renderPanel();

    await user.click(screen.getByRole("button", { name: "Download temperature-log.pdf" }));

    expect((await screen.findByRole("alert")).textContent).toContain("You may not open this attachment.");
  });

  it("is read-only without edit permission: files can be downloaded but not added or voided", () => {
    renderPanel({ canEdit: false, canRemove: false });

    expect(screen.queryByRole("button", { name: "Upload" })).toBeNull();
    expect(screen.queryByLabelText(/^Upload file\. Accepted formats/)).toBeNull();
    expect(screen.queryByRole("button", { name: /^Void / })).toBeNull();
    expect(screen.getByRole("button", { name: "Download temperature-log.pdf" })).toBeTruthy();
  });

  it("offers Void only to someone who may void: a person who can add files but not void sees no Void control", () => {
    renderPanel({ canEdit: true, canRemove: false });

    expect(screen.getByRole("button", { name: "Upload" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /^Void / })).toBeNull();
    expect(screen.getByRole("button", { name: "Download temperature-log.pdf" })).toBeTruthy();
  });

  it("offers Void to someone who may void", () => {
    renderPanel({ canRemove: true });

    expect(screen.getByRole("button", { name: "Void temperature-log.pdf" })).toBeTruthy();
  });

  it("shows the empty state", () => {
    renderPanel({ attachments: [] });

    expect(screen.getByText("No attachments yet.")).toBeTruthy();
  });
});
