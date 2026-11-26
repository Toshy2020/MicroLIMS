import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";

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

import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { QualityCreateActions } from "./QualityCreateActions";
import { ok, serveLookups } from "./steps/stepTestUtils";

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>;
}

function renderActions(permissions: string[]) {
  localStorage.setItem("microlims_permissions", JSON.stringify(permissions));
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={["/quality"]}>
          <Routes>
            <Route
              path="*"
              element={
                <>
                  <QualityCreateActions />
                  <Where />
                </>
              }
            />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

describe("QualityCreateActions", () => {
  beforeEach(() => {
    serveLookups(http, { "/org/my-sections": () => ok([]) });
    http.post.mockImplementation(() => ok({ id: 40, version: 1, record: { id: 40, number: "CC-2026-0007" } }));
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.clearAllMocks();
  });

  it("offers nothing to a person who can neither propose nor receive", () => {
    renderActions(["Quality.View"]);

    expect(screen.queryByRole("button")).toBeNull();
  });

  it("offers proposing a change control to a person with Quality.Raise, but not receiving a complaint", () => {
    renderActions(["Quality.View", "Quality.Raise"]);

    expect(screen.getByRole("button", { name: "Propose change control" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Receive complaint" })).toBeNull();
  });

  it("offers receiving a complaint to QA", () => {
    renderActions(["Quality.View", "Quality.Approve"]);

    expect(screen.getByRole("button", { name: "Receive complaint" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Propose change control" })).toBeNull();
  });

  it("opens the new change control on its own page after it is proposed", async () => {
    const user = userEvent.setup();
    renderActions(["Quality.View", "Quality.Raise"]);

    await user.click(screen.getByRole("button", { name: "Propose change control" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^Title/), "Move incubators");
    await user.type(within(dialog).getByLabelText(/^What changes/), "Two incubators move");
    await user.type(within(dialog).getByLabelText(/^Why/), "Renovation");
    await user.click(within(dialog).getByRole("button", { name: "Propose" }));

    await waitFor(() => expect(screen.getByTestId("where").textContent).toBe("/quality/40"));
  });
});
