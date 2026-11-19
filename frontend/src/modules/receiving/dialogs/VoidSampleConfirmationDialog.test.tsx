import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { renderInArabic, resetLanguage } from "../../../i18n/testUtils";
import { ReceiveService } from "../services/ReceiveService";
import { VoidSampleConfirmationDialog } from "./VoidSampleConfirmationDialog";
import type { SampleRecord } from "../types/receivingTypes";

vi.mock("../services/ReceiveService", () => ({
  ReceiveService: { voidSample: vi.fn() }
}));

afterEach(async () => {
  cleanup();
  localStorage.clear();
  vi.clearAllMocks();
  await resetLanguage();
});

const sample = {
  sampleId: 7,
  referenceNumber: "FP1126005",
  displayName: "Paracetamol 500 mg",
  category: "FinishedProduct",
  batchNumber: "B1",
  controlNumber: null
} as unknown as SampleRecord;

describe("VoidSampleConfirmationDialog", () => {
  it("opens with focus on Cancel, not on the reason field", async () => {
    render(
      <PinnedLightTheme>
        <AuthProvider>
        <VoidSampleConfirmationDialog open sample={sample} onClose={vi.fn()} onSuccess={vi.fn()} />
        </AuthProvider>
      </PinnedLightTheme>
    );
    const cancel = await screen.findByRole("button", { name: "Cancel" });
    await waitFor(() => expect(document.activeElement).toBe(cancel));
  });

  it("shows both English recorded meaning and Arabic translation in Arabic, and submits voidSample", async () => {
    const user = userEvent.setup();
    vi.mocked(ReceiveService.voidSample).mockResolvedValue(sample);
    const onSuccess = vi.fn();
    const payload = btoa(JSON.stringify({ "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "SectionHead" }));
    localStorage.setItem("microlims_token", `h.${payload}.s`);
    localStorage.setItem("microlims_username", "head");
    localStorage.setItem("microlims_full_name", "Head One");

    await renderInArabic(
      <AuthProvider>
        <VoidSampleConfirmationDialog open sample={sample} onClose={vi.fn()} onSuccess={onSuccess} />
      </AuthProvider>,
      ["common", "receiving"]
    );

    const cancel = await screen.findByRole("button", { name: "إلغاء" });
    await waitFor(() => expect(document.activeElement).toBe(cancel));
    // Wait for MUI Dialog transition's onEntered focus callback to settle before typing.
    await new Promise((r) => setTimeout(r, 300));
    const reasonInput = screen.getByRole("textbox", { name: /سبب الإبطال/ }) as HTMLTextAreaElement;
    await user.click(reasonInput);
    await user.type(reasonInput, "Duplicate entry");
    await waitFor(() => expect(reasonInput.value).toBe("Duplicate entry"));
    const signAndVoidBtn = screen.getByRole("button", { name: "توقيع وإبطال" }) as HTMLButtonElement;
    await waitFor(() => expect(signAndVoidBtn.disabled).toBe(false));
    await user.click(signAndVoidBtn);

    const sigDialog = await screen.findByRole("dialog", { name: "التوقيع الإلكتروني" });
    const english = within(sigDialog).getByText("I void this sample record; it is struck from the register.");
    const arabic = within(sigDialog).getByText("أُبطل سجل العينة هذا؛ ويُشطب من السجل.");
    expect(english.getAttribute("lang")).toBe("en");
    expect(english.getAttribute("dir")).toBe("ltr");
    expect(english.compareDocumentPosition(arabic) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    await user.type(within(sigDialog).getByLabelText(/^كلمة المرور/), "pw-1");
    await user.click(within(sigDialog).getByRole("button", { name: "توقيع" }));

    await waitFor(() => expect(ReceiveService.voidSample).toHaveBeenCalledWith(7, "Duplicate entry", "pw-1"));
    expect(onSuccess).toHaveBeenCalledTimes(1);
  });
});
