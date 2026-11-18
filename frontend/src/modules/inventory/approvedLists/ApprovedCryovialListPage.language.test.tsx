import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../../i18n/testUtils";
import { tableViewService } from "../../../components/lab/table/services/tableViewService";
import { CryovialService } from "../../laboratoryConfiguration/cryovials/services/CryovialService";
import { ApprovedCryovialListPage } from "./ApprovedCryovialListPage";

// Only the network boundary is replaced; the page and its table render for real.
vi.mock("../../laboratoryConfiguration/cryovials/services/CryovialService", () => ({
  CryovialService: { getAll: vi.fn(), thawVial: vi.fn() }
}));
vi.mock("../../../components/lab/table/services/tableViewService", () => ({
  tableViewService: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() }
}));

describe("ApprovedCryovialListPage in Arabic", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.mocked(CryovialService.getAll).mockResolvedValue([]);
    vi.mocked(tableViewService.list).mockResolvedValue([]);
  });

  afterEach(async () => {
    cleanup();
    localStorage.clear();
    await resetLanguage();
  });

  it("shows the heading, the filter labels and the empty message in Arabic", async () => {
    await renderInArabic(
      <AuthProvider>
        <MemoryRouter>
          <ApprovedCryovialListPage />
        </MemoryRouter>
      </AuthProvider>,
      ["common", "inventory"]
    );
    expect(screen.getByRole("heading", { name: "قائمة القوارير المجمّدة المعتمدة" })).toBeTruthy();
    expect(screen.getByRole("combobox", { name: "الكائن الدقيق" })).toBeTruthy();
    await waitFor(() => expect(screen.getByText("لا توجد قوارير مجمّدة معتمدة متاحة حاليًا في المخزون.")).toBeTruthy());
  });
});
