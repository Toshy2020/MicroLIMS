import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";

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

import { ExtensionDialog, type ExtensionDialogMode } from "./ExtensionDialog";
import { extension, refusal } from "../testFixtures";

function seedSigner() {
  const payload = btoa(
    JSON.stringify({
      "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Qa",
      permission: "Quality.Approve"
    })
  );
  localStorage.setItem("microlims_token", `h.${payload}.s`);
  localStorage.setItem("microlims_username", "qa1");
  localStorage.setItem("microlims_full_name", "Quinn QA");
}

function renderDialog(mode: ExtensionDialogMode, onDone = vi.fn().mockResolvedValue(undefined), onClose = vi.fn()) {
  seedSigner();
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <ExtensionDialog mode={mode} recordId={5} recordNumber="CAPA-2026-0003" recordVersion={12} onClose={onClose} onDone={onDone} />
      </AuthProvider>
    </PinnedLightTheme>
  );
  return { onDone, onClose };
}

describe("ExtensionDialog request", () => {
  beforeEach(() => {
    http.post.mockResolvedValue({ data: { data: extension() } });
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.clearAllMocks();
  });

  it("requests an extension of the record's due date with a reason, sending the record version", async () => {
    const user = userEvent.setup();
    const { onDone } = renderDialog({ kind: "request", actionId: null, actionDescription: null, currentDueDate: "2026-12-01" });

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("Current due date: 2026-12-01")).toBeTruthy();
    const submit = within(dialog).getByRole("button", { name: "Send request" }) as HTMLButtonElement;
    expect(submit.disabled).toBe(true);

    await user.type(within(dialog).getByLabelText(/^New due date/), "2026-12-20");
    await user.type(within(dialog).getByLabelText(/^Reason/), "Supplier delivers the probe late");
    await user.click(submit);

    await waitFor(() => {
      expect(http.post).toHaveBeenCalledWith(
        "/quality/5/extensions",
        { actionId: null, requestedDueDate: "2026-12-20", reason: "Supplier delivers the probe late" },
        { headers: { "If-Match": '"12"' } }
      );
    });
    await waitFor(() => expect(onDone).toHaveBeenCalledTimes(1));
  });

  it("requests an extension of one action and names it", async () => {
    const user = userEvent.setup();
    renderDialog({ kind: "request", actionId: 11, actionDescription: "Recalibrate the incubator probe", currentDueDate: "2026-12-05" });

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("For action: Recalibrate the incubator probe")).toBeTruthy();
    await user.type(within(dialog).getByLabelText(/^New due date/), "2026-12-12");
    await user.type(within(dialog).getByLabelText(/^Reason/), "Waiting for the certificate");
    await user.click(within(dialog).getByRole("button", { name: "Send request" }));

    await waitFor(() => {
      expect(http.post).toHaveBeenCalledWith(
        "/quality/5/extensions",
        { actionId: 11, requestedDueDate: "2026-12-12", reason: "Waiting for the certificate" },
        { headers: { "If-Match": '"12"' } }
      );
    });
  });

  it("keeps the dialog open with the server's refusal beside the action", async () => {
    const user = userEvent.setup();
    http.post.mockRejectedValue(refusal("The new due date must be later than the current one."));
    const { onDone } = renderDialog({ kind: "request", actionId: null, actionDescription: null, currentDueDate: "2026-12-01" });

    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^New due date/), "2026-11-01");
    await user.type(within(dialog).getByLabelText(/^Reason/), "Because");
    await user.click(within(dialog).getByRole("button", { name: "Send request" }));

    expect(await within(dialog).findByText("The new due date must be later than the current one.")).toBeTruthy();
    expect(onDone).not.toHaveBeenCalled();
  });
});

describe("ExtensionDialog decision", () => {
  beforeEach(() => {
    http.post.mockResolvedValue({ data: { data: extension({ decision: "Approved" }) } });
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.clearAllMocks();
  });

  const pending = extension({ id: 51, requestedDueDate: "2026-12-20", reason: "Supplier delivers the probe late" });

  it("shows the request and asks for a signature that states the meaning and names the signer", async () => {
    const user = userEvent.setup();
    renderDialog({ kind: "decide", extension: pending });

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("Requested due date: 2026-12-20")).toBeTruthy();
    expect(within(dialog).getByText("Reason given: Supplier delivers the probe late")).toBeTruthy();

    await user.click(within(dialog).getByRole("radio", { name: "Approve the extension" }));
    await user.click(within(dialog).getByRole("button", { name: "Sign and decide" }));

    const signature = await screen.findByRole("dialog", { name: /sign/i });
    expect(within(signature).getByText("Quinn QA")).toBeTruthy();
    expect(within(signature).getByText("Due-date extension decided")).toBeTruthy();
    expect(http.post).not.toHaveBeenCalled();
  });

  it("cannot be signed before a decision is chosen", async () => {
    renderDialog({ kind: "decide", extension: pending });

    const dialog = await screen.findByRole("dialog");
    expect((within(dialog).getByRole("button", { name: "Sign and decide" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("approves with the password and the record version once signed", async () => {
    const user = userEvent.setup();
    const { onDone } = renderDialog({ kind: "decide", extension: pending });

    await user.click(await screen.findByRole("radio", { name: "Approve the extension" }));
    await user.click(screen.getByRole("button", { name: "Sign and decide" }));
    await user.type(await screen.findByLabelText(/^Password/), "correct-password");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      expect(http.post).toHaveBeenCalledWith(
        "/quality/extensions/51/decision",
        { approve: true, comment: null, password: "correct-password" },
        { headers: { "If-Match": '"12"' } }
      );
    });
    await waitFor(() => expect(onDone).toHaveBeenCalledTimes(1));
  });

  it("refuses with a comment", async () => {
    const user = userEvent.setup();
    renderDialog({ kind: "decide", extension: pending });

    await user.click(await screen.findByRole("radio", { name: "Refuse the extension" }));
    await user.type(screen.getByLabelText("Comment (optional)"), "The plan allows no more delay.");
    await user.click(screen.getByRole("button", { name: "Sign and decide" }));
    await user.type(await screen.findByLabelText(/^Password/), "correct-password");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      expect(http.post).toHaveBeenCalledWith(
        "/quality/extensions/51/decision",
        { approve: false, comment: "The plan allows no more delay.", password: "correct-password" },
        { headers: { "If-Match": '"12"' } }
      );
    });
  });

  it("keeps the signature dialog open and shows the server's message after a wrong password", async () => {
    const user = userEvent.setup();
    http.post.mockRejectedValue(refusal("The password is incorrect."));
    const { onDone } = renderDialog({ kind: "decide", extension: pending });

    await user.click(await screen.findByRole("radio", { name: "Approve the extension" }));
    await user.click(screen.getByRole("button", { name: "Sign and decide" }));
    const password = await screen.findByLabelText(/^Password/);
    await user.type(password, "wrong");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    expect(await screen.findByText("The password is incorrect.")).toBeTruthy();
    expect((screen.getByLabelText(/^Password/) as HTMLInputElement).value).toBe("");
    expect(onDone).not.toHaveBeenCalled();
  });
});
