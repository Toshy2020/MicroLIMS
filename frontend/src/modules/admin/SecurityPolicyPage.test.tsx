import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { SecurityPolicyPage } from "./SecurityPolicyPage";
import { SecurityPolicyService } from "./services/SecurityPolicyService";

vi.mock("./services/SecurityPolicyService", () => ({ SecurityPolicyService: { get: vi.fn() } }));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

describe("SecurityPolicyPage", () => {
  it("shows each group with its values and sources, and no way to edit them", async () => {
    vi.mocked(SecurityPolicyService.get).mockResolvedValue({
      isReadOnly: true,
      groups: [
        { key: "lockout", title: "Account lockout", settings: [
          { key: "lockout.maxFailedAttempts", label: "Failed logins before lockout", value: "5 attempts", source: "Code constant: AuthenticationService" }
        ] }
      ]
    });

    render(<PinnedLightTheme><SecurityPolicyPage /></PinnedLightTheme>);

    expect(await screen.findByText("Account lockout")).toBeTruthy();
    expect(screen.getByText("Failed logins before lockout")).toBeTruthy();
    expect(screen.getByText("5 attempts")).toBeTruthy();
    expect(screen.getByText("Code constant: AuthenticationService")).toBeTruthy();
    expect(screen.queryAllByRole("button")).toHaveLength(0);
    expect(screen.queryAllByRole("textbox")).toHaveLength(0);
  });

  it("says so when the policy cannot be loaded", async () => {
    vi.mocked(SecurityPolicyService.get).mockRejectedValue(new Error("403"));
    render(<PinnedLightTheme><SecurityPolicyPage /></PinnedLightTheme>);
    expect(await screen.findByText(/Could not load the security policy/)).toBeTruthy();
  });
});
