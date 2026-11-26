import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { MasterDataWorkbookPage } from "./MasterDataWorkbookPage";
import type { WorkbookReport } from "./types";

// Network boundaries only: apiClient (the real workbook service runs on it) and the change-control lookups.
const http = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() }));
vi.mock("../../../services/apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/apiClient")>();
  return { ...actual, apiClient: http };
});
const lookup = vi.hoisted(() => ({ listApproved: vi.fn(), getSectionSetting: vi.fn() }));
vi.mock("../../../services/changeControlLookupService", () => ({ changeControlLookupService: lookup }));

const clean: WorkbookReport = { sheets: [{ sheet: "Specifications", created: 2, unchanged: 0, errors: 0 }], errors: [], hasErrors: false };
const gateRefused: WorkbookReport = {
  sheets: [{ sheet: "Specifications", created: 0, unchanged: 0, errors: 1 }],
  errors: [{ sheet: "Specifications", row: 5, column: null, message: "This laboratory requires an approved change control for this change." }],
  hasErrors: true
};

function seedAuth(permissions: string[]) {
  const claims = btoa(
    JSON.stringify({
      "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "SectionHead",
      permission: permissions
    })
  );
  localStorage.setItem("microlims_token", `h.${claims}.s`);
  localStorage.setItem("microlims_username", "head");
  localStorage.setItem("microlims_full_name", "Head One");
}

function mount() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <MasterDataWorkbookPage />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const ok = (data: unknown) => Promise.resolve({ data: { data } });

async function chooseFileAndCheck(user: ReturnType<typeof userEvent.setup>) {
  const input = document.querySelector('input[type="file"]') as HTMLInputElement;
  await user.upload(input, new File(["x"], "specs.xlsx"));
  await user.click(screen.getByRole("button", { name: "Check file" }));
  await waitFor(() => expect((screen.getByRole("button", { name: /Load and sign/ }) as HTMLButtonElement).disabled).toBe(false));
}

async function signAndLoad(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole("button", { name: /Load and sign/ }));
  const dialog = await screen.findByRole("dialog");
  await user.type(within(dialog).getByLabelText(/^Password/), "pw-1");
  await user.click(within(dialog).getByRole("button", { name: "Sign" }));
}

beforeEach(() => {
  localStorage.clear();
  seedAuth(["Quality.View", "MasterData.Manage"]);
  http.get.mockImplementation(() => ok([]));
  lookup.listApproved.mockResolvedValue([
    { id: 1, number: "CC-2026-0004", type: "ChangeControl", title: "Load the new specifications", status: "ApprovedForImplementation", sectionId: 1, sectionName: "FP", version: 1 }
  ]);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("MasterDataWorkbookPage change control number", () => {
  it("sends the entered number (trimmed, upper-case) as X-Change-Control on the import request", async () => {
    const user = userEvent.setup();
    http.post.mockImplementation((url: string) =>
      url.endsWith("/validate") ? ok(clean) : ok({ committed: true, report: clean, import: null })
    );
    mount();
    await chooseFileAndCheck(user);

    await user.type(screen.getByRole("combobox", { name: "Approved change control" }), "  cc-2026-0004 ");
    await signAndLoad(user);

    await waitFor(() => expect(http.post.mock.calls.some(([url]) => String(url).endsWith("/import"))).toBe(true));
    const importCall = http.post.mock.calls.find(([url]) => String(url).endsWith("/import"));
    expect(importCall?.[2]).toEqual({ headers: { "X-Change-Control": "CC-2026-0004" } });
    const validateCall = http.post.mock.calls.find(([url]) => String(url).endsWith("/validate"));
    expect(validateCall?.[2]).toBeUndefined();
  });

  it("sends no header when the field is left empty", async () => {
    const user = userEvent.setup();
    http.post.mockImplementation((url: string) =>
      url.endsWith("/validate") ? ok(clean) : ok({ committed: true, report: clean, import: null })
    );
    mount();
    await chooseFileAndCheck(user);

    await signAndLoad(user);

    await waitFor(() => expect(http.post.mock.calls.some(([url]) => String(url).endsWith("/import"))).toBe(true));
    const importCall = http.post.mock.calls.find(([url]) => String(url).endsWith("/import"));
    expect(importCall?.[2]).toBeUndefined();
  });

  it("blocks Load and sign while the number is not valid for the header, and says why", async () => {
    const user = userEvent.setup();
    http.post.mockImplementation(() => ok(clean));
    mount();
    await chooseFileAndCheck(user);

    await user.type(screen.getByRole("combobox", { name: "Approved change control" }), "not valid!");

    expect((screen.getByRole("button", { name: /Load and sign/ }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText(/capital letters, digits and hyphens/)).toBeTruthy();
  });

  it("offers the approved change controls to a user who can view quality records", async () => {
    const user = userEvent.setup();
    mount();

    await user.click(screen.getByRole("combobox", { name: "Approved change control" }));

    expect(await screen.findByRole("option", { name: /CC-2026-0004/ })).toBeTruthy();
    expect(lookup.listApproved).toHaveBeenCalledWith(null, expect.any(AbortSignal));
  });

  it("does not ask for the list without Quality.View; the number is simply typed", async () => {
    localStorage.clear();
    seedAuth(["MasterData.Manage"]);
    mount();

    expect(screen.getByRole("combobox", { name: "Approved change control" })).toBeTruthy();
    expect(lookup.listApproved).not.toHaveBeenCalled();
  });

  it("keeps rows the gate refused as row errors with the server text, plus a hint, and does not import again by itself", async () => {
    const user = userEvent.setup();
    http.post.mockImplementation((url: string) =>
      url.endsWith("/validate") ? ok(clean) : ok({ committed: false, report: gateRefused, import: null })
    );
    mount();
    await chooseFileAndCheck(user);

    await signAndLoad(user);

    expect(await screen.findByText("This laboratory requires an approved change control for this change.")).toBeTruthy();
    expect(screen.getByText(/enter its number in the change control field/)).toBeTruthy();
    expect(http.post.mock.calls.filter(([url]) => String(url).endsWith("/import"))).toHaveLength(1);
  });
});
