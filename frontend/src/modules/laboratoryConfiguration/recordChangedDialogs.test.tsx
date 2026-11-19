import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AuthProvider } from "../../contexts/AuthContext";
import { LiveProvider } from "../../contexts/LiveContext";
import { createFakeLiveConnection, type FakeLiveConnection } from "../../contexts/testing/fakeLiveConnection";
import { AcMachinePanel } from "./afterCleaning/components/AcPanels";
import { AfterCleaningConfigService } from "./afterCleaning/services/AfterCleaningConfigService";
import type { Machine } from "./afterCleaning/acConfigTypes";
import { EmDepartmentPanel } from "./environmentalMonitoring/components/EmPanels";
import { EMConfigService } from "./environmentalMonitoring/services/EMConfigService";
import type { EmDepartment } from "./environmentalMonitoring/emConfigTypes";
import { WaterDepartmentPanel } from "./water/components/WaterDepartmentPanel";
import { WaterConfigService } from "./water/services/WaterConfigService";
import type { WaterDept } from "./water/waterConfigTypes";
import { AddItemDialog } from "./items/components/AddItemDialog";
import type { Item } from "./items/services/ItemService";
import { ItemService } from "./items/services/ItemService";

// Network boundaries only: the HTTP client, the services each dialog calls, and the Test Master
// list that the test pickers read.
vi.mock("../../services/apiClient", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(() => Promise.resolve({ data: {} })) }
}));
vi.mock("./afterCleaning/services/AfterCleaningConfigService", () => ({
  AfterCleaningConfigService: { getMachines: vi.fn(), updateMachine: vi.fn() }
}));
vi.mock("./environmentalMonitoring/services/EMConfigService", () => ({
  EMConfigService: { getDepartments: vi.fn(), updateDepartment: vi.fn() }
}));
vi.mock("./water/services/WaterConfigService", () => ({
  WaterConfigService: { getWaterDepartments: vi.fn(), updateWaterDepartment: vi.fn() }
}));
vi.mock("./items/services/ItemService", () => ({
  ItemService: { getAll: vi.fn(), update: vi.fn() }
}));
vi.mock("../../services/masterDataOptions", () => ({
  masterDataOptions: { getTestDefinitions: vi.fn(() => Promise.resolve([])) }
}));

const ME = 11;
const OTHER = 99;
const BANNER_TEXT = /This record was changed by another user at .+\. Reload to see the latest\./;

async function settle() {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

async function mount(connection: FakeLiveConnection, ui: React.ReactElement) {
  localStorage.setItem("microlims_token", "h.e30=.s");
  localStorage.setItem("microlims_user_id", String(ME));
  render(
    <AuthProvider>
      <LiveProvider connection={connection}>{ui}</LiveProvider>
    </AuthProvider>
  );
  await settle();
}

function signal(connection: FakeLiveConnection, type: string, id: number, actorUserId: number) {
  act(() => connection.emitSignal({ kind: "record", key: `record:${type}:${id}`, actorUserId }));
}

beforeEach(() => {
  localStorage.clear();
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
  localStorage.clear();
});

describe("After-cleaning machine panel", () => {
  const machine: Machine = { id: 5, version: 1, name: "Line one", parts: [] };
  const fresh: Machine = { id: 5, version: 2, name: "Line one renamed", parts: [] };

  it("shows the banner for another user's change, reloads the record and saves with the new version", async () => {
    const user = userEvent.setup();
    vi.mocked(AfterCleaningConfigService.getMachines).mockResolvedValue([fresh]);
    vi.mocked(AfterCleaningConfigService.updateMachine).mockResolvedValue(fresh);
    const connection = createFakeLiveConnection();
    await mount(connection, <AcMachinePanel open machine={machine} onClose={vi.fn()} onSaved={vi.fn()} />);
    expect(screen.queryByRole("status")).toBeNull();

    signal(connection, "machine", 5, OTHER);
    expect(screen.getByRole("status").textContent).toMatch(BANNER_TEXT);

    await user.click(screen.getByRole("button", { name: "Reload" }));
    expect((await screen.findByLabelText(/Machine name/)) as HTMLInputElement).toHaveProperty("value", "Line one renamed");
    expect(screen.queryByRole("status")).toBeNull();

    await user.click(screen.getByRole("button", { name: "Save changes" }));
    expect(AfterCleaningConfigService.updateMachine).toHaveBeenCalledWith(5, "Line one renamed", 2);
  });

  it("does not show the banner for this tab's own save", async () => {
    const user = userEvent.setup();
    vi.mocked(AfterCleaningConfigService.updateMachine).mockResolvedValue(machine);
    const connection = createFakeLiveConnection();
    await mount(connection, <AcMachinePanel open machine={machine} onClose={vi.fn()} onSaved={vi.fn()} />);

    await user.click(screen.getByRole("button", { name: "Save changes" }));
    await settle();
    signal(connection, "machine", 5, ME);

    expect(screen.queryByRole("status")).toBeNull();
  });
});

describe("EM department panel", () => {
  const dept = { id: 7, version: 3, name: "Filling", class: "Grade C", testingFrequency: "", rooms: [] } as unknown as EmDepartment;
  const fresh = { ...dept, version: 4, name: "Filling hall" } as EmDepartment;

  it("shows the banner for another user's change, reloads the record and saves with the new version", async () => {
    const user = userEvent.setup();
    vi.mocked(EMConfigService.getDepartments).mockResolvedValue([fresh]);
    vi.mocked(EMConfigService.updateDepartment).mockResolvedValue(fresh);
    const connection = createFakeLiveConnection();
    await mount(connection, <EmDepartmentPanel open dept={dept} onClose={vi.fn()} onSaved={vi.fn()} />);

    signal(connection, "department", 7, OTHER);
    expect(screen.getByRole("status").textContent).toMatch(BANNER_TEXT);

    await user.click(screen.getByRole("button", { name: "Reload" }));
    expect(((await screen.findByLabelText(/^Name/)) as HTMLInputElement).value).toBe("Filling hall");

    await user.click(screen.getByRole("button", { name: "Save changes" }));
    expect(EMConfigService.updateDepartment).toHaveBeenCalledTimes(1);
    const args = vi.mocked(EMConfigService.updateDepartment).mock.calls[0];
    expect(args[0]).toBe(7);
    expect(args[1]).toBe("Filling hall");
    expect(args[4]).toBe(4);
  });

  it("does not show the banner for this tab's own save", async () => {
    const user = userEvent.setup();
    vi.mocked(EMConfigService.updateDepartment).mockResolvedValue(dept);
    const connection = createFakeLiveConnection();
    await mount(connection, <EmDepartmentPanel open dept={dept} onClose={vi.fn()} onSaved={vi.fn()} />);

    await user.click(screen.getByRole("button", { name: "Save changes" }));
    await settle();
    signal(connection, "department", 7, ME);

    expect(screen.queryByRole("status")).toBeNull();
  });

  it("asks before discarding unsaved edits on Reload", async () => {
    const user = userEvent.setup();
    const connection = createFakeLiveConnection();
    await mount(connection, <EmDepartmentPanel open dept={dept} onClose={vi.fn()} onSaved={vi.fn()} />);
    await user.type(screen.getByLabelText(/^Name/), " x");

    signal(connection, "department", 7, OTHER);
    await user.click(screen.getByRole("button", { name: "Reload" }));

    expect(await screen.findByText("Discard your unsaved changes?")).toBeTruthy();
    expect(EMConfigService.getDepartments).not.toHaveBeenCalled();
  });
});

describe("Water system panel", () => {
  const dept: WaterDept = { id: 3, version: 1, name: "Purified", samplingPoints: [] };
  const fresh: WaterDept = { ...dept, version: 2, name: "Purified water" };

  it("shows the banner for another user's change, reloads the record and saves with the new version", async () => {
    const user = userEvent.setup();
    vi.mocked(WaterConfigService.getWaterDepartments).mockResolvedValue([fresh]);
    vi.mocked(WaterConfigService.updateWaterDepartment).mockResolvedValue(fresh);
    const connection = createFakeLiveConnection();
    await mount(connection, <WaterDepartmentPanel open dept={dept} onClose={vi.fn()} onSaved={vi.fn()} />);

    signal(connection, "waterDepartment", 3, OTHER);
    expect(screen.getByRole("status").textContent).toMatch(BANNER_TEXT);

    await user.click(screen.getByRole("button", { name: "Reload" }));
    expect(((await screen.findByLabelText(/^Name/)) as HTMLInputElement).value).toBe("Purified water");

    await user.click(screen.getByRole("button", { name: "Save changes" }));
    expect(WaterConfigService.updateWaterDepartment).toHaveBeenCalledWith(3, "Purified water", 2);
  });

  it("does not show the banner for this tab's own save", async () => {
    const user = userEvent.setup();
    vi.mocked(WaterConfigService.updateWaterDepartment).mockResolvedValue(dept);
    const connection = createFakeLiveConnection();
    await mount(connection, <WaterDepartmentPanel open dept={dept} onClose={vi.fn()} onSaved={vi.fn()} />);

    await user.click(screen.getByRole("button", { name: "Save changes" }));
    await settle();
    signal(connection, "waterDepartment", 3, ME);

    expect(screen.queryByRole("status")).toBeNull();
  });
});

describe("Item dialog", () => {
  const item = {
    id: 21,
    version: 1,
    name: "Paracetamol",
    code: "PCM",
    category: "FinishedProduct",
    sopNumber: "",
    assignedTests: [{ testCode: "ASSAY", displayName: "ASSAY" }]
  } as unknown as Item;
  const fresh = { ...item, version: 2, name: "Paracetamol 500" } as Item;

  it("shows the banner for another user's change and hands the reloaded item to the page", async () => {
    const user = userEvent.setup();
    vi.mocked(ItemService.getAll).mockResolvedValue([fresh]);
    const onItemReloaded = vi.fn();
    const connection = createFakeLiveConnection();
    await mount(
      connection,
      <AddItemDialog open itemToEdit={item} onClose={vi.fn()} onSave={vi.fn()} onItemReloaded={onItemReloaded} />
    );
    expect(screen.queryByRole("status")).toBeNull();

    signal(connection, "item", 21, OTHER);
    expect(screen.getByRole("status").textContent).toMatch(BANNER_TEXT);

    await user.click(screen.getByRole("button", { name: "Reload" }));

    expect(onItemReloaded).toHaveBeenCalledWith(fresh);
    expect(screen.queryByRole("status")).toBeNull();
  });

  it("shows the form values of the reloaded item once the page passes it back", async () => {
    const connection = createFakeLiveConnection();
    localStorage.setItem("microlims_token", "h.e30=.s");
    localStorage.setItem("microlims_user_id", String(ME));
    const view = render(
      <AuthProvider>
        <LiveProvider connection={connection}>
          <AddItemDialog open itemToEdit={item} onClose={vi.fn()} onSave={vi.fn()} />
        </LiveProvider>
      </AuthProvider>
    );
    await settle();
    expect((screen.getByLabelText(/Item Name/) as HTMLInputElement).value).toBe("Paracetamol");

    view.rerender(
      <AuthProvider>
        <LiveProvider connection={connection}>
          <AddItemDialog open itemToEdit={fresh} onClose={vi.fn()} onSave={vi.fn()} />
        </LiveProvider>
      </AuthProvider>
    );
    await settle();

    expect((screen.getByLabelText(/Item Name/) as HTMLInputElement).value).toBe("Paracetamol 500");
  });
});
