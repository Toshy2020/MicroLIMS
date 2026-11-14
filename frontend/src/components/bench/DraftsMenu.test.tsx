import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor, waitForElementToBeRemoved, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { toast } from "sonner";
import { AuthProvider } from "../../contexts/AuthContext";
import { useDraftOverview } from "../../hooks/useDraftOverview";
import { clientSettingsService } from "../../services/clientSettingsService";
import { draftStore } from "../../services/drafts/draftStore";
import { makeDraft, resetDraftDb } from "../../services/drafts/draftTestUtils";
import { DraftsMenu } from "./DraftsMenu";
import { renderWithTheme } from "./benchTestUtils";

vi.mock("../../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));
vi.mock("../../services/clientSettingsService", () => ({ clientSettingsService: { get: vi.fn() } }));
// Package boundary: only the toast calls are replaced; the real Toaster still mounts with the theme provider.
vi.mock("sonner", async (importOriginal) => ({
  ...(await importOriginal<typeof import("sonner")>()),
  toast: { info: vi.fn(), error: vi.fn(), success: vi.fn() }
}));

function Host() {
  const overview = useDraftOverview();
  return <DraftsMenu overview={overview} />;
}

function Path() {
  return <span data-testid="path">{useLocation().pathname}</span>;
}

function renderMenu() {
  return renderWithTheme(
    <AuthProvider>
      <MemoryRouter>
        <Host />
        <Path />
      </MemoryRouter>
    </AuthProvider>,
    { bench: true }
  );
}

const FIXED_NOW = new Date("2031-03-04T12:00:00.000Z");

beforeEach(async () => {
  localStorage.clear();
  localStorage.setItem("microlims_token", "h.e30=.s");
  localStorage.setItem("microlims_user_id", "7");
  await resetDraftDb();
  vi.mocked(clientSettingsService.get).mockResolvedValue({ benchDraftRetentionHours: 24, features: [] });
  vi.mocked(toast.info).mockClear();
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(FIXED_NOW);
});
afterEach(() => {
  vi.useRealTimers();
  cleanup();
  localStorage.clear();
});

describe("DraftsMenu", () => {
  it("lists my drafts with title and age; opens the draft's route", async () => {
    const user = userEvent.setup();
    await draftStore.put(makeDraft({ userId: 7, recordKey: "p1", title: "Plate 1 count", route: "/testing/plate-1", values: {}, updatedAtDevice: "2031-03-04T10:00:00.000Z" }));
    await draftStore.put(makeDraft({ userId: 8, recordKey: "p2", title: "Someone else's plate", route: "/testing/plate-2", values: {} }));
    renderMenu();

    await user.click(await screen.findByRole("button", { name: "Unsaved entries on this tablet, 1" }));

    const list = await screen.findByRole("dialog", { name: "Unsaved entries on this tablet" });
    expect(within(list).getByText("Plate 1 count")).toBeTruthy();
    expect(within(list).getByText(/2 h ago/)).toBeTruthy();
    expect(within(list).queryByText("Someone else's plate")).toBeNull();

    await user.click(within(list).getByRole("button", { name: "Open Plate 1 count" }));
    await waitFor(() => expect(screen.getByTestId("path").textContent).toBe("/testing/plate-1"));
  });

  it("shows a rejected draft with its reason", async () => {
    const user = userEvent.setup();
    await draftStore.put(makeDraft({ userId: 7, values: {}, state: "rejected", rejectReason: "This record was changed by someone else." }));
    renderMenu();

    await user.click(await screen.findByRole("button", { name: /Unsaved entries on this tablet/ }));

    expect(await screen.findByText(/This record was changed by someone else\./)).toBeTruthy();
  });

  it("Discard asks for confirmation with Cancel focused, and removes the draft only when confirmed", async () => {
    const user = userEvent.setup();
    const draft = makeDraft({ userId: 7, values: { v: "x" } });
    await draftStore.put(draft);
    renderMenu();

    await user.click(await screen.findByRole("button", { name: /Unsaved entries on this tablet/ }));
    await user.click(await screen.findByRole("button", { name: "Discard Plate 1 count" }));

    const dialog = await screen.findByRole("alertdialog");
    expect(within(dialog).getByRole("button", { name: "Cancel" })).toBe(document.activeElement);
    await user.click(within(dialog).getByRole("button", { name: "Cancel" }));
    await waitForElementToBeRemoved(() => screen.queryByRole("alertdialog"));
    expect(await draftStore.get(7, draft.formKey, draft.recordKey)).toBeDefined();

    await user.click(await screen.findByRole("button", { name: "Discard Plate 1 count" }));
    await user.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: "Discard" }));

    await waitFor(async () => expect(await draftStore.get(7, draft.formKey, draft.recordKey)).toBeUndefined());
  });

  it("shows \"Another user has unsaved entries on this tablet\" when countOtherUsers > 0, with no detail", async () => {
    const user = userEvent.setup();
    await draftStore.put(makeDraft({ userId: 8, title: "Secret title", values: { v: "secret" } }));
    renderMenu();

    await user.click(await screen.findByRole("button", { name: /Unsaved entries on this tablet/ }));

    expect(await screen.findByText("Another user has unsaved entries on this tablet")).toBeTruthy();
    expect(screen.queryByText("Secret title")).toBeNull();
  });

  it("expired drafts are purged at start and the user is told how many", async () => {
    await draftStore.put(makeDraft({ userId: 7, recordKey: "old1", values: {}, updatedAtDevice: "2031-03-01T00:00:00.000Z" }));
    await draftStore.put(makeDraft({ userId: 8, recordKey: "old2", values: {}, updatedAtDevice: "2031-03-02T00:00:00.000Z" }));
    await draftStore.put(makeDraft({ userId: 7, recordKey: "fresh", values: {}, updatedAtDevice: "2031-03-04T11:00:00.000Z" }));
    renderMenu();

    await waitFor(() =>
      expect(toast.info).toHaveBeenCalledWith("2 unsaved entries older than 24 hours were removed from this tablet.")
    );
    expect((await draftStore.listForUser(7)).map((d) => d.recordKey)).toEqual(["fresh"]);
    expect(await draftStore.listForUser(8)).toEqual([]);
  });

  it("uses the retention the server returns", async () => {
    vi.mocked(clientSettingsService.get).mockResolvedValue({ benchDraftRetentionHours: 2, features: [] });
    await draftStore.put(makeDraft({ userId: 7, recordKey: "three-hours", values: {}, updatedAtDevice: "2031-03-04T09:00:00.000Z" }));
    renderMenu();

    await waitFor(() =>
      expect(toast.info).toHaveBeenCalledWith("1 unsaved entry older than 2 hours was removed from this tablet.")
    );
    expect(await draftStore.listForUser(7)).toEqual([]);
  });

  it("renders nothing when there are no drafts at all", async () => {
    renderMenu();
    await waitFor(() => expect(clientSettingsService.get).toHaveBeenCalled());
    expect(screen.queryByRole("button", { name: /Unsaved entries/ })).toBeNull();
  });
});
