import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { i18n } from "../../../../i18n";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { HplcMethodDialog } from "./HplcMethodDialog";

vi.mock("../../../../services/masterDataOptions", () => ({
  masterDataOptions: { getItems: vi.fn().mockResolvedValue([]) }
}));

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage("en");
});

function renderDialog() {
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <HplcMethodDialog
          open
          editingId={null}
          solutions={[]}
          materialMasters={[]}
          mySections={[]}
          finishedProducts={[]}
          onClose={() => undefined}
          onSuccess={() => undefined}
        />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("HplcMethodDialog language", () => {
  it("keeps the English title and actions", async () => {
    renderDialog();
    expect(await screen.findByText("Add HPLC Method Master")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Create HPLC Method" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Cancel" })).toBeTruthy();
  });

  it("shows the Arabic title, actions and validation messages in Arabic", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["common", "masterData"]);
    renderDialog();

    expect(await screen.findByText("إضافة سجل رئيسي لطريقة HPLC")).toBeTruthy();
    expect(screen.getByRole("button", { name: "إلغاء" })).toBeTruthy();

    await userEvent.click(screen.getByRole("button", { name: "إنشاء طريقة HPLC" }));
    expect(await screen.findByText("اسم الطريقة مطلوب.", { exact: false })).toBeTruthy();
  });
});
