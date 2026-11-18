import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { i18n } from "../../i18n";
import { BenchActionBar } from "./BenchActionBar";
import { DensityToggle } from "../lab/table/DensityToggle";

beforeEach(async () => {
  await i18n.loadNamespaces(["common"]);
  await i18n.changeLanguage("ar");
});

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage("en");
});

describe("bench and table controls in Arabic", () => {
  it("renders the bench action bar in Arabic and still fires the callbacks", async () => {
    const user = userEvent.setup();
    const onSave = vi.fn();
    render(
      <PinnedLightTheme>
        <BenchActionBar onCancel={() => {}} onSave={onSave} onSaveAndNext={() => {}} saving={false} />
      </PinnedLightTheme>
    );
    expect(screen.getByRole("button", { name: "إلغاء" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "حفظ والتالي" })).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "حفظ" }));
    expect(onSave).toHaveBeenCalledTimes(1);
  });

  it("renders the density toggle labels in Arabic", () => {
    render(
      <PinnedLightTheme>
        <DensityToggle density="comfortable" onChange={() => {}} benchMode={false} />
      </PinnedLightTheme>
    );
    expect(screen.getByRole("group", { name: "كثافة الصفوف" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "كثافة مضغوطة" })).toBeTruthy();
  });
});
