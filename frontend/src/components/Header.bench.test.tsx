import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { useTheme } from "@mui/material";
import { BenchModeProvider } from "../theme/BenchModeContext";
import { ThemeModeProvider } from "../theme/ThemeModeContext";
import { AuthProvider } from "../contexts/AuthContext";
import { Header } from "./Header";

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

function renderHeader() {
  localStorage.setItem("microlims_token", "h.e30=.s");
  localStorage.setItem("microlims_username", "s.lab");
  localStorage.setItem("microlims_full_name", "Sam Lab");
  return render(
    <BenchModeProvider>
      <ThemeModeProvider>
        <AuthProvider>
          <MemoryRouter>
            <Header labCodes={[]} />
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
