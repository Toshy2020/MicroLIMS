import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { useTheme } from "@mui/material";
import { BenchModeProvider } from "../theme/BenchModeContext";
import { ThemeModeProvider } from "../theme/ThemeModeContext";
import { AuthProvider } from "../contexts/AuthContext";
import { Header } from "./Header";
import { apiClient } from "../services/apiClient";

vi.mock("../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));

afterEach(() => {
  cleanup();
  localStorage.clear();
});

// A real consumer of the active theme, so the test proves the switch reaches
// what components actually read.
function ThemeBenchFlag() {
  const theme = useTheme();
  return <span data-testid="theme-bench">{String(theme.custom.bench)}</span>;
}

function LocationProbe() {
  return <span data-testid="path">{useLocation().pathname}</span>;
}

function renderHeader(unsavedDraftCount?: number) {
  localStorage.setItem("microlims_token", "h.e30=.s");
  localStorage.setItem("microlims_username", "s.lab");
  localStorage.setItem("microlims_full_name", "Sam Lab");
  return render(
    <BenchModeProvider>
      <ThemeModeProvider>
        <AuthProvider>
          <MemoryRouter>
            <Header labCodes={[]} unsavedDraftCount={unsavedDraftCount} />
            <LocationProbe />
            <ThemeBenchFlag />
          </MemoryRouter>
        </AuthProvider>
      </ThemeModeProvider>
    </BenchModeProvider>
  );
}

describe("Header bench mode switch", () => {
  it("shows a labelled Bench mode switch that changes theme.custom.bench and is remembered", async () => {
    const user = userEvent.setup();
    renderHeader();
    const toggle = screen.getByRole("switch", { name: "Bench mode" });
    expect((toggle as HTMLInputElement).checked).toBe(false);
    expect(screen.getByTestId("theme-bench").textContent).toBe("false");

    await user.click(toggle);

    expect((screen.getByRole("switch", { name: "Bench mode" }) as HTMLInputElement).checked).toBe(true);
    expect(screen.getByTestId("theme-bench").textContent).toBe("true");
    expect(localStorage.getItem("microlims_bench_mode")).toBe("on");

    await user.click(screen.getByRole("switch", { name: "Bench mode" }));
    expect(screen.getByTestId("theme-bench").textContent).toBe("false");
  });

  it("starts on when the device preference is on", () => {
    localStorage.setItem("microlims_bench_mode", "on");
    renderHeader();
    expect((screen.getByRole("switch", { name: "Bench mode" }) as HTMLInputElement).checked).toBe(true);
    expect(screen.getByTestId("theme-bench").textContent).toBe("true");
  });
});

describe("Header Switch user", () => {
  it("is hidden outside bench mode", () => {
    renderHeader();
    expect(screen.queryByRole("button", { name: "Switch user" })).toBeNull();
  });

  it("is shown in bench mode, signs out through the logout endpoint and goes to /login", async () => {
    localStorage.setItem("microlims_bench_mode", "on");
    const user = userEvent.setup();
    renderHeader();
    localStorage.setItem("microlims_refresh_token", "r1");
    vi.mocked(apiClient.post).mockResolvedValue({ data: {} });
    await user.click(screen.getByRole("button", { name: "Switch user" }));

    expect(apiClient.post).toHaveBeenCalledWith("/auth/logout", { refreshToken: "r1" }, expect.anything());
    expect(localStorage.getItem("microlims_token")).toBeNull();
    expect(screen.getByTestId("path").textContent).toBe("/login");
  });

  it("with drafts asks first, focuses Cancel, and Cancel keeps the session", async () => {
    localStorage.setItem("microlims_bench_mode", "on");
    const user = userEvent.setup();
    renderHeader(2);
    vi.mocked(apiClient.post).mockClear();
    await user.click(screen.getByRole("button", { name: "Switch user" }));

    expect(await screen.findByText(/You have 2 unsaved entries/)).toBeTruthy();
    expect(screen.getByText(/not saved to MicroLIMS/)).toBeTruthy();
    expect(apiClient.post).not.toHaveBeenCalled();
    const cancel = screen.getByRole("button", { name: "Cancel" });
    expect(document.activeElement).toBe(cancel);

    await user.click(cancel);
    expect(localStorage.getItem("microlims_token")).not.toBeNull();
    expect(screen.getByTestId("path").textContent).toBe("/");
    expect(apiClient.post).not.toHaveBeenCalled();
  });

  it("with drafts, confirming signs out and goes to /login", async () => {
    localStorage.setItem("microlims_bench_mode", "on");
    const user = userEvent.setup();
    renderHeader(1);
    vi.mocked(apiClient.post).mockResolvedValue({ data: {} });
    await user.click(screen.getByRole("button", { name: "Switch user" }));
    const dialog = await screen.findByRole("alertdialog");
    expect(within(dialog).getByText(/You have 1 unsaved entry/)).toBeTruthy();
    await user.click(within(dialog).getByRole("button", { name: "Switch user" }));
    expect(localStorage.getItem("microlims_token")).toBeNull();
    expect(screen.getByTestId("path").textContent).toBe("/login");
  });
});
