import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { renderInArabic, resetLanguage } from "../../../i18n/testUtils";
import { RecoveryCodesPanel } from "./RecoveryCodesPanel";
import { PasswordPolicyChecklist } from "./PasswordPolicyChecklist";

afterEach(async () => {
  await resetLanguage();
});

describe("authentication components in Arabic", () => {
  it("shows the recovery codes panel in Arabic without translating the codes", async () => {
    await renderInArabic(<RecoveryCodesPanel codes={["AAAA-1111"]} onDone={vi.fn()} />, ["account"]);
    expect(screen.getByRole("heading", { name: "احفظ رموز الاسترداد" })).toBeTruthy();
    expect(screen.getByText("AAAA-1111")).toBeTruthy();
    expect(screen.getByRole("button", { name: "متابعة" })).toBeTruthy();
  });

  it("shows the password policy in Arabic", async () => {
    await renderInArabic(<PasswordPolicyChecklist password="" blurred={false} />, ["account"]);
    expect(screen.getByText("يجب أن تحتوي كلمة المرور على:")).toBeTruthy();
    expect(screen.getByRole("list", { name: "متطلبات سياسة كلمة المرور" })).toBeTruthy();
  });
});
