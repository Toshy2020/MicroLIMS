import { afterEach, describe, expect, it, vi } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AuthProvider } from "../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../i18n/testUtils";
import { SignatureDialog } from "./SignatureDialog";

const ENGLISH = "I am approving this result.";
const ARABIC = "أقرّ بأنني أعتمد هذه النتيجة.";
const NOTE = "الإقرار المسجَّل هو النص الإنجليزي.";

function seedAuth() {
  const payload = btoa(JSON.stringify({ "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "SectionHead", permission: "Samples.View" }));
  localStorage.setItem("microlims_token", `h.${payload}.s`);
  localStorage.setItem("microlims_username", "head");
  localStorage.setItem("microlims_full_name", "Head One");
}

afterEach(async () => {
  localStorage.clear();
  await resetLanguage();
});

describe("SignatureDialog in Arabic", () => {
  it("shows the recorded English statement first, the Arabic translation below and the note", async () => {
    seedAuth();
    await renderInArabic(
      <AuthProvider>
        <SignatureDialog open meaningStatement={ENGLISH} meaningStatementTranslation={ARABIC} onCancel={() => {}} onConfirm={async () => {}} />
      </AuthProvider>
    );

    const dialog = screen.getByRole("dialog", { name: "التوقيع الإلكتروني" });
    const english = within(dialog).getByText(ENGLISH);
    const arabic = within(dialog).getByText(ARABIC);
    const note = within(dialog).getByText(NOTE);

    // The statement the server records stays English and left to right...
    expect(english.getAttribute("lang")).toBe("en");
    expect(english.getAttribute("dir")).toBe("ltr");
    // ...and comes before its translation, which comes before the note.
    expect(english.compareDocumentPosition(arabic) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(arabic.compareDocumentPosition(note) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    expect(within(dialog).getByText("Head One")).toBeTruthy();
    expect(within(dialog).getByRole("button", { name: "توقيع" }).hasAttribute("disabled")).toBe(true);
    expect(within(dialog).getByLabelText(/^كلمة المرور/)).toBeTruthy();
  });

  it("still shows the English statement and the note when no translation is supplied", async () => {
    seedAuth();
    await renderInArabic(
      <AuthProvider>
        <SignatureDialog open meaningStatement={ENGLISH} onCancel={() => {}} onConfirm={async () => {}} />
      </AuthProvider>
    );
    expect(screen.getByText(ENGLISH)).toBeTruthy();
    expect(screen.getByText(NOTE)).toBeTruthy();
    expect(screen.queryByText(ARABIC)).toBeNull();
  });

  it("passes the password to onConfirm and shows the Arabic failure message", async () => {
    seedAuth();
    const user = userEvent.setup();
    const onConfirm = vi.fn().mockRejectedValue(new Error("x"));
    await renderInArabic(
      <AuthProvider>
        <SignatureDialog open meaningStatement={ENGLISH} onCancel={() => {}} onConfirm={onConfirm} />
      </AuthProvider>
    );
    await user.type(screen.getByLabelText(/^كلمة المرور/), "pw-1");
    await user.click(screen.getByRole("button", { name: "توقيع" }));
    expect(onConfirm).toHaveBeenCalledWith("pw-1", undefined);
    expect(await screen.findByText("فشل التوقيع.")).toBeTruthy();
  });
});
