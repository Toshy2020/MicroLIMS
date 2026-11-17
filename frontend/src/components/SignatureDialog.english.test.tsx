import { afterEach, describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { AuthProvider } from "../contexts/AuthContext";
import { SignatureDialog } from "./SignatureDialog";

afterEach(() => localStorage.clear());

describe("SignatureDialog in English", () => {
  it("shows no translation and no note, whatever translation a screen supplies", () => {
    const payload = btoa(JSON.stringify({ "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "SectionHead" }));
    localStorage.setItem("microlims_token", `h.${payload}.s`);
    localStorage.setItem("microlims_username", "head");
    render(
      <AuthProvider>
        <SignatureDialog open meaningStatement="I approve." meaningStatementTranslation="أوافق." onCancel={() => {}} onConfirm={async () => {}} />
      </AuthProvider>
    );
    expect(screen.getByText("I approve.")).toBeTruthy();
    expect(screen.queryByText("أوافق.")).toBeNull();
    expect(screen.queryByText("The English statement is recorded.")).toBeNull();
  });
});
