import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { i18n } from "../i18n";
import { ShortcutsDialog } from "./entry/ShortcutsDialog";
import { FilterBar } from "./lab/FilterBar";
import { LoadFailuresAlert } from "./LoadErrorAlert";

beforeEach(async () => {
  await i18n.loadNamespaces(["common"]);
  await i18n.changeLanguage("ar");
});

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage("en");
});

function Bar() {
  const [search, setSearch] = useState("");
  return <FilterBar search={search} onSearch={setSearch} resultCount={3} onRefresh={() => {}} />;
}

describe("shared dialogs and banners in Arabic", () => {
  it("renders the shortcuts dialog in Arabic", () => {
    render(
      <PinnedLightTheme>
        <ShortcutsDialog open onClose={() => {}} />
      </PinnedLightTheme>
    );
    expect(screen.getByText("اختصارات لوحة المفاتيح")).toBeTruthy();
    expect(screen.getByText("نسخ القيمة من الخلية التي فوقها")).toBeTruthy();
    expect(screen.getByRole("button", { name: "إغلاق" })).toBeTruthy();
  });

  it("renders the filter bar in Arabic and clears the search", async () => {
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <Bar />
      </PinnedLightTheme>
    );
    expect(screen.getByRole("button", { name: "تحديث" })).toBeTruthy();
    expect(screen.getByText("3 نتائج")).toBeTruthy();
    await user.type(screen.getByRole("textbox", { name: "بحث" }), "abc");
    await user.click(screen.getByRole("button", { name: "مسح البحث" }));
    expect((screen.getByRole("textbox", { name: "بحث" }) as HTMLInputElement).value).toBe("");
  });

  it("uses the Arabic dual form for two failed lists", () => {
    render(
      <PinnedLightTheme>
        <LoadFailuresAlert failed={["أ", "ب"]} />
      </PinnedLightTheme>
    );
    expect(screen.getByText(/تظهر القائمتان فارغتين/)).toBeTruthy();
  });
});
