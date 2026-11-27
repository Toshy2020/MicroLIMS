import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn()
}));
vi.mock("../../../services/apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/apiClient")>();
  return { ...actual, apiClient: http };
});

import { AuthProvider } from "../../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../../i18n/testUtils";
import { DeviationActions } from "../deviation/DeviationActions";
import { recordDetail } from "../testFixtures";
import { QA, ok, seedSigner, serveLookups } from "./stepTestUtils";
import type { QualityDeviationDetailDto } from "../../../services/qualityService";

describe("quality step forms in Arabic", () => {
  afterEach(async () => {
    cleanup();
    localStorage.clear();
    vi.clearAllMocks();
    await resetLanguage();
  });

  it("shows the step in Arabic and signs with the server's English statement plus an Arabic reading aid", async () => {
    const user = userEvent.setup();
    serveLookups(http);
    http.post.mockImplementation(() => ok({}));
    seedSigner();
    const base = recordDetail().deviation as QualityDeviationDetailDto;
    const record = recordDetail({ status: "Raised", deviation: { ...base, status: "Raised" } });

    await renderInArabic(
      <AuthProvider>
        <MemoryRouter>
          <DeviationActions record={record} permissions={QA} currentUserId={9} onChanged={vi.fn().mockResolvedValue(undefined)} />
        </MemoryRouter>
      </AuthProvider>,
      ["common", "quality"]
    );

    expect(await screen.findByRole("heading", { name: "تقييم الانحراف" })).toBeTruthy();
    await user.click(screen.getByRole("combobox", { name: /^التصنيف/ }));
    await user.click(await screen.findByRole("option", { name: "Major" }));
    await user.click(screen.getByRole("combobox", { name: /^المالك/ }));
    await user.click(await screen.findByRole("option", { name: "Sam Head (shead)" }));
    await user.click(screen.getByRole("button", { name: "قبول وتوقيع" }));

    const dialog = await screen.findByRole("dialog");
    await waitFor(() => expect(within(dialog).getByText("Deviation assessed")).toBeTruthy());
    expect(within(dialog).getByText("تم تقييم الانحراف")).toBeTruthy();
  });
});
