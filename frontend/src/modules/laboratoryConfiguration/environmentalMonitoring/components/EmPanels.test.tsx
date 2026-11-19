import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { i18n } from "../../../../i18n";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { EmDepartmentPanel } from "./EmPanels";

afterEach(cleanup);

const renderPanel = () =>
  render(
    <PinnedLightTheme>
      <EmDepartmentPanel open dept={null} onClose={vi.fn()} onSaved={vi.fn()} />
    </PinnedLightTheme>
  );

describe("EmDepartmentPanel language", () => {
  it("shows the English title by default", async () => {
    renderPanel();
    expect((await screen.findAllByText("Add department")).length).toBeGreaterThan(0);
  });

  it("shows the Arabic title and field label when Arabic is active", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["labConfig"]);
    try {
      renderPanel();
      expect(await screen.findAllByText("إضافة قسم")).toBeTruthy();
      expect(screen.getByLabelText(/^الفئة/)).toBeTruthy();
    } finally {
      await i18n.changeLanguage("en");
    }
  });
});
