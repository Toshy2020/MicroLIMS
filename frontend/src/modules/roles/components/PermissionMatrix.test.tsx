import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { PermissionMatrix } from "./PermissionMatrix";
import type { PermissionRecord, SodRules } from "../services/RoleService";

const permissions: PermissionRecord[] = [
  { code: "Users.Manage", description: "Manage users.", isEnforced: true },
  { code: "Samples.Approve", description: "Approve samples.", isEnforced: true },
  { code: "Audit.View", description: "View the audit trail.", isEnforced: true }
];

const sodRules: SodRules = {
  administrationCodes: ["Users.Manage"],
  gxpExecutionAndApprovalCodes: ["Samples.Approve"]
};

function renderMatrix(checked: string[], rules: SodRules | null = sodRules, onToggle = vi.fn()) {
  render(
    <PinnedLightTheme>
      <PermissionMatrix permissions={permissions} checkedCodes={new Set(checked)} onToggle={onToggle} sodRules={rules} />
    </PinnedLightTheme>
  );
  return onToggle;
}

const box = (code: string) => screen.getByRole("checkbox", { name: new RegExp(`^${code.replace(".", "\\.")}`) }) as HTMLInputElement;

describe("PermissionMatrix segregation of duties", () => {
  afterEach(cleanup);

  it("disables a GxP box while an administration code is checked, and names the conflicting code", async () => {
    renderMatrix(["Users.Manage"]);

    expect(box("Samples.Approve").disabled).toBe(true);
    expect(box("Audit.View").disabled).toBe(false);
    fireEvent.mouseOver(screen.getByText("Samples.Approve"));
    expect((await screen.findByRole("tooltip")).textContent).toContain("Cannot be combined with Users.Manage");
  });

  it("disables an administration box while a GxP code is checked", () => {
    renderMatrix(["Samples.Approve"]);

    expect(box("Users.Manage").disabled).toBe(true);
  });

  it("keeps both boxes of an existing conflict enabled so it can be fixed", () => {
    const onToggle = renderMatrix(["Users.Manage", "Samples.Approve"]);

    expect(box("Users.Manage").disabled).toBe(false);
    expect(box("Samples.Approve").disabled).toBe(false);
    fireEvent.click(box("Samples.Approve"));
    expect(onToggle).toHaveBeenCalledWith("Samples.Approve", false);
  });

  it("disables nothing when no rules are loaded", () => {
    renderMatrix(["Users.Manage"], null);

    expect(box("Samples.Approve").disabled).toBe(false);
  });
});
