import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { LoginPage } from "./Login";

const login = vi.fn();
vi.mock("../contexts/AuthContext", () => ({ useAuth: () => ({ login }) }));
vi.mock("../modules/authentication/services/authenticationService", () => ({
  authenticationService: { login: vi.fn(), me: vi.fn(), requestPasswordReset: vi.fn() }
}));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

function renderPage() {
  render(<PinnedLightTheme><MemoryRouter><LoginPage /></MemoryRouter></PinnedLightTheme>);
}

describe("LoginPage", () => {
  it("shows what the system is beside the sign-in form, with no remember option", () => {
    renderPage();
    expect(screen.getByRole("heading", { level: 1, name: "Sign in" })).toBeTruthy();
    expect(screen.getByText("Pharmaceutical Quality Control")).toBeTruthy();
    expect(screen.getByText("FDA 21 CFR Part 11")).toBeTruthy();
    expect(screen.getByText("EU GMP Annex 11")).toBeTruthy();
    expect(screen.getByText("Authorised users only. Every sign-in attempt is recorded.")).toBeTruthy();
    expect(screen.queryByRole("checkbox")).toBeNull();
  });

  it("signs in with the entered credentials", async () => {
    vi.mocked(authenticationService.login).mockResolvedValue({ token: "t", refreshToken: "r", role: "Analyst", permissions: [], mustChangePassword: false } as never);
    vi.mocked(authenticationService.me).mockResolvedValue({ fullName: "A Hassan", userId: 7 } as never);
    renderPage();

    fireEvent.change(screen.getByLabelText(/^Username/), { target: { value: "a.hassan" } });
    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: "Secret#1" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign In" }));

    await waitFor(() => expect(login).toHaveBeenCalled());
    expect(authenticationService.login).toHaveBeenCalledWith("a.hassan", "Secret#1");
  });

  it("offers both recovery routes on the help screen", () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Need help signing in?" }));

    expect(screen.getByRole("heading", { level: 1, name: "Need help signing in?" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Send Reset Link" })).toBeTruthy();
    expect(screen.getByRole("link", { name: "Have a recovery code from your administrator?" })).toBeTruthy();
  });
});
