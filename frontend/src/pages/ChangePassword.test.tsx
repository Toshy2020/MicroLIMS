import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider, type SignInMethod } from "../contexts/AuthContext";
import { ChangePasswordPage } from "./ChangePassword";

vi.mock("../modules/authentication/services/authenticationService", () => ({
  authenticationService: { changePassword: vi.fn(), me: vi.fn() }
}));

afterEach(() => {
  cleanup();
  localStorage.clear();
});

function renderPage(signInMethod: SignInMethod = "Local") {
  localStorage.setItem("microlims_sign_in_method", signInMethod);
  render(
    <AuthProvider>
      <MemoryRouter>
        <ChangePasswordPage />
      </MemoryRouter>
    </AuthProvider>
  );
  return signInMethod === "Directory"
    ? screen.getByLabelText(/^New Signing Password/)
    : screen.getByLabelText(/^New Password/);
}

describe("ChangePasswordPage policy checklist", () => {
  it("starts neutral rather than showing unchecked radio-like circles", () => {
    renderPage();
    expect(screen.getAllByTitle("Not met yet")).toHaveLength(5);
    expect(screen.queryByTitle("Not met")).toBeNull();
  });

  it("ticks rules as they are met while typing, without flagging the rest", () => {
    const input = renderPage();
    fireEvent.change(input, { target: { value: "abcdefgh" } });
    expect(screen.getAllByTitle("Met")).toHaveLength(2);
    expect(screen.getAllByTitle("Not met yet")).toHaveLength(3);
  });

  it("marks unmet rules as failed once the field is left", () => {
    const input = renderPage();
    fireEvent.change(input, { target: { value: "abcdefgh" } });
    fireEvent.blur(input);
    expect(screen.getAllByTitle("Not met")).toHaveLength(3);
  });

  it("uses signing password wording for a directory user", () => {
    renderPage("Directory");
    expect(screen.getByRole("heading", { name: "Change Signing Password" })).toBeTruthy();
    expect(screen.getByLabelText(/^Current Signing Password/)).toBeTruthy();
    expect(screen.getByLabelText(/^New Signing Password/)).toBeTruthy();
    expect(screen.getByLabelText(/^Confirm New Signing Password/)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Change Signing Password" })).toBeTruthy();
  });
});
