import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { i18n } from "../../../i18n";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import { IcpWorkspacePage } from "./IcpWorkspacePage";

vi.mock("../services/IcpWorkspaceService", () => ({
  IcpWorkspaceService: {
    getInstruments: vi.fn()
  }
}));

describe("IcpWorkspacePage Arabic", () => {
  beforeEach(async () => {
    localStorage.clear();
    localStorage.setItem("microlims_token", "header.payload.signature");
    localStorage.setItem("microlims_user_id", "7");
    vi.mocked(IcpWorkspaceService.getInstruments).mockResolvedValue([]);
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["common", "icpWorkspace"]);
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
          <MemoryRouter initialEntries={["/icp"]}>
            <IcpWorkspacePage />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    expect(await screen.findByText("مساحة عمل ICP-OES")).toBeTruthy();
    expect(await screen.findByText("لم يتم العثور على أجهزة ICP")).toBeTruthy();
  });
});
