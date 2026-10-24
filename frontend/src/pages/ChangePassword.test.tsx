import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { ChangePasswordPage } from "./ChangePassword";

vi.mock("../contexts/AuthContext", () => ({ useAuth: () => ({ refresh: vi.fn() }) }));
vi.mock("../modules/authentication/services/authenticationService", () => ({
  authenticationService: { changePassword: vi.fn() }
}));

afterEach(cleanup);

function renderPage() {
  render(<MemoryRouter><ChangePasswordPage /></MemoryRouter>);
  return screen.getByLabelText(/^New Password/);
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
});
