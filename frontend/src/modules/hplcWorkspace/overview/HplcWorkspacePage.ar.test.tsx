import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { i18n } from "../../../i18n";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { HplcWorkspacePage } from "./HplcWorkspacePage";

vi.mock("../services/HplcWorkspaceService", () => ({
  HplcWorkspaceService: {
    getInstruments: vi.fn()
  }
}));

describe("HplcWorkspacePage Arabic", () => {
  beforeEach(async () => {
    localStorage.clear();
    localStorage.setItem("microlims_token", "header.payload.signature");
    localStorage.setItem("microlims_user_id", "7");
    vi.mocked(HplcWorkspaceService.getInstruments).mockResolvedValue([]);
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["common", "hplcWorkspace"]);
  });

  afterEach(async () => {
    cleanup();
    vi.clearAllMocks();
    localStorage.clear();
    await i18n.changeLanguage("en");
  });

  it("renders translated workspace title and empty state in Arabic", async () => {
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter initialEntries={["/hplc"]}>
            <HplcWorkspacePage />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    expect(await screen.findByText("مساحة عمل HPLC")).toBeTruthy();
    expect(await screen.findByText("لم يتم العثور على أجهزة")).toBeTruthy();
  });
});
