import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { Header } from "./Header";

const auth = {
  username: "s.lab",
  fullName: "Sam Lab",
  jobTitle: null as string | null,
  role: "Analyst",
  roleName: null as string | null,
  logout: vi.fn()
};
vi.mock("../contexts/AuthContext", () => ({ useAuth: () => auth }));
vi.mock("../theme/ThemeModeContext", () => ({ useThemeMode: () => ({ mode: "light", toggleMode: vi.fn() }) }));
vi.mock("../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));
vi.mock("./search/GlobalSearchDialog", () => ({ GlobalSearchDialog: () => null }));

afterEach(() => { cleanup(); });

function openAccountMenu(labCodes: string[] = []) {
  render(<PinnedLightTheme><MemoryRouter><Header labCodes={labCodes} /></MemoryRouter></PinnedLightTheme>);
  fireEvent.click(screen.getByRole("button", { name: /^Account menu for/ }));
}

describe("Header job title", () => {
  it("shows the role name when the user has no job title", () => {
    auth.jobTitle = null;
    auth.roleName = "Senior Microbiologist";
    openAccountMenu(["MICRO"]);
    // Once as the title, once in the Role line of the account menu.
    expect(screen.getAllByText("Senior Microbiologist")).toHaveLength(2);
    expect(screen.queryByText("Analyst")).toBeNull();
  });

  it("keeps the laboratory prefix for a built-in role name", () => {
    auth.jobTitle = null;
    auth.roleName = "Analyst";
    openAccountMenu(["MICRO"]);
    expect(screen.getByText("Microbiology Analyst")).toBeTruthy();
    expect(screen.getByText("Analyst")).toBeTruthy();
  });

  it("prefers the job title an administrator set", () => {
    auth.jobTitle = "Incubation Lead";
    auth.roleName = "Senior Microbiologist";
    openAccountMenu();
    expect(screen.getByText("Incubation Lead")).toBeTruthy();
    // The Role line still names the role.
    expect(screen.getAllByText("Senior Microbiologist")).toHaveLength(1);
  });
});
