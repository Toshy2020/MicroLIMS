import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { LiveProvider } from "../../../contexts/LiveContext";
import { createFakeLiveConnection, type FakeLiveConnection } from "../../../contexts/testing/fakeLiveConnection";
import { MaterialMasterPage } from "./MaterialMasterPage";
import { MaterialMasterService, type MaterialMasterEntry } from "./services/MaterialMasterService";

// Network boundaries only.
vi.mock("../../../services/apiClient", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(() => Promise.resolve({ data: {} })) }
}));
vi.mock("./services/MaterialMasterService", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./services/MaterialMasterService")>()),
  MaterialMasterService: { getAll: vi.fn(), getById: vi.fn(), update: vi.fn(), setActive: vi.fn(), create: vi.fn() }
}));
vi.mock("./services/MaterialCategoryService", () => ({
  MaterialCategoryService: { getAll: vi.fn(() => Promise.resolve([])) }
}));
vi.mock("../../../services/laboratorySectionService", () => ({
  getMySections: vi.fn(() => Promise.resolve([])),
  laboratorySectionService: { getSections: vi.fn(() => Promise.resolve([])) }
}));

const ME = 11;
const BANNER_TEXT = /This record was changed by another user at .+\. Reload to see the latest\./;

const entry: MaterialMasterEntry = {
  id: 31,
  version: 1,
  sectionId: 1,
  canEdit: true,
  code: "RGT-001",
  name: "Sodium hydroxide",
  category: "Reagent",
  baseUnit: "Gram",
  isActive: true
};
const fresh: MaterialMasterEntry = { ...entry, version: 2, name: "Sodium hydroxide 1 M" };

async function settle() {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

async function openEditDialog(connection: FakeLiveConnection) {
  const user = userEvent.setup();
  localStorage.setItem("microlims_token", "h.e30=.s");
  localStorage.setItem("microlims_user_id", String(ME));
  render(
    <PinnedLightTheme>
      <MemoryRouter>
        <AuthProvider>
          <LiveProvider connection={connection}>
            <MaterialMasterPage />
          </LiveProvider>
        </AuthProvider>
      </MemoryRouter>
    </PinnedLightTheme>
  );
  await user.click(await screen.findByText("RGT-001"));
  await screen.findByLabelText(/^Name/);
  return user;
}

function signal(connection: FakeLiveConnection, actorUserId: number) {
  act(() => connection.emitSignal({ kind: "record", key: "record:materialMasterEntry:31", actorUserId }));
}

beforeEach(() => {
  localStorage.clear();
  vi.clearAllMocks();
  vi.mocked(MaterialMasterService.getAll).mockResolvedValue([entry]);
});

afterEach(() => {
  cleanup();
  localStorage.clear();
});

describe("Material master edit dialog changed-by-someone-else banner", () => {
  it("shows the banner for another user's change, reloads the record and saves with the new version", async () => {
    vi.mocked(MaterialMasterService.getById).mockResolvedValue(fresh);
    vi.mocked(MaterialMasterService.update).mockResolvedValue(fresh);
    const connection = createFakeLiveConnection();
    const user = await openEditDialog(connection);
    expect(screen.queryByRole("status")).toBeNull();

    signal(connection, 99);
    expect(screen.getByRole("status").textContent).toMatch(BANNER_TEXT);

    await user.click(screen.getByRole("button", { name: "Reload" }));
    expect(MaterialMasterService.getById).toHaveBeenCalledWith(31);
    await settle();
    expect((screen.getByLabelText(/^Name/) as HTMLInputElement).value).toBe("Sodium hydroxide 1 M");

    await user.click(screen.getByRole("button", { name: "Save Changes" }));
    await settle();
    expect(MaterialMasterService.update).toHaveBeenCalledTimes(1);
    const [id, payload, version] = vi.mocked(MaterialMasterService.update).mock.calls[0];
    expect(id).toBe(31);
    expect(payload.name).toBe("Sodium hydroxide 1 M");
    expect(version).toBe(2);
  });

  it("does not show the banner for this tab's own save", async () => {
    vi.mocked(MaterialMasterService.update).mockResolvedValue(entry);
    const connection = createFakeLiveConnection();
    const user = await openEditDialog(connection);
    // The own-save signal arrives while the dialog is still open (the page reloads it after a save).
    vi.mocked(MaterialMasterService.update).mockImplementation(async () => {
      signal(connection, ME);
      return entry;
    });

    await user.click(screen.getByRole("button", { name: "Save Changes" }));
    await settle();

    expect(MaterialMasterService.update).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole("status")).toBeNull();
  });
});
