import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { AuthProvider } from "../../../contexts/AuthContext";
import { LanguageProvider } from "../../../i18n/LanguageProvider";
import { renderInArabic, resetLanguage } from "../../../i18n/testUtils";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { SignOffDialog } from "./SignOffDialog";

vi.mock("../services/WorkingStandardService", () => ({
  WorkingStandardService: {
    review: vi.fn(),
    approve: vi.fn(),
    rejectAtReview: vi.fn(),
    rejectAtApproval: vi.fn(),
    returnQualification: vi.fn()
  }
}));

const EN_APPROVE_MEANING =
  "I approve this working standard qualification and release the standard for analytical testing use.";
const AR_APPROVE_MEANING =
  "أعتمد تأهيل المادة القياسية العاملة هذا وأفرج عن المادة القياسية للاستخدام في الاختبارات التحليلية.";

describe("SignOffDialog meaning statement", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  afterEach(async () => {
    cleanup();
    localStorage.clear();
    await resetLanguage();
  });

  it("shows only the recorded English statement when the interface is English", async () => {
    render(
      <PinnedLightTheme>
        <LanguageProvider>
          <AuthProvider>
            <SignOffDialog
              open
              actionType="approve"
              qualificationId={1}
              qualificationCode="WSQ-01"
              onClose={vi.fn()}
              onSuccess={vi.fn()}
            />
          </AuthProvider>
        </LanguageProvider>
      </PinnedLightTheme>
    );

    expect(await screen.findByText(EN_APPROVE_MEANING)).toBeTruthy();
    expect(screen.queryByText(AR_APPROVE_MEANING)).toBeNull();
  });

  it("shows the recorded English statement (LTR) and the Arabic translation below it when the interface is Arabic", async () => {
    await renderInArabic(
      <AuthProvider>
        <SignOffDialog
          open
          actionType="approve"
          qualificationId={1}
          qualificationCode="WSQ-01"
          onClose={vi.fn()}
          onSuccess={vi.fn()}
        />
      </AuthProvider>,
      ["common", "workingStandards"]
    );

    const englishEl = await screen.findByText(EN_APPROVE_MEANING);
    expect(englishEl.getAttribute("lang")).toBe("en");
    expect(englishEl.getAttribute("dir")).toBe("ltr");
    expect(screen.getByText(AR_APPROVE_MEANING)).toBeTruthy();
  });
});
