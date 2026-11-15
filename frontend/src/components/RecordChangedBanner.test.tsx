import { useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AuthProvider } from "../contexts/AuthContext";
import { LiveProvider } from "../contexts/LiveContext";
import { createFakeLiveConnection, type FakeLiveConnection } from "../contexts/testing/fakeLiveConnection";
import { noteOwnSave } from "../hooks/useRecordChanged";
import { RecordChangedBanner } from "./RecordChangedBanner";

// Network boundary only: AuthProvider posts on logout; nothing here signs out.
vi.mock("../services/apiClient", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(() => Promise.resolve({ data: {} })) }
}));

const ME = 11;
const BANNER_TEXT = /This record was changed by another user at .+\. Reload to see the latest\./;

async function settle() {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

function Harness({ id, dirty = false, onReload }: { id: number; dirty?: boolean; onReload: () => void }) {
  const [note, setNote] = useState("");
  return (
    <>
      <label>
        Notes
        <input value={note} onChange={(e) => setNote(e.target.value)} />
      </label>
      <RecordChangedBanner type="sample" id={id} dirty={dirty} onReload={onReload} />
    </>
  );
}

async function mount(connection: FakeLiveConnection, ui: React.ReactElement) {
  localStorage.setItem("microlims_token", "h.e30=.s");
  localStorage.setItem("microlims_user_id", String(ME));
  const view = render(
    <AuthProvider>
      <LiveProvider connection={connection}>{ui}</LiveProvider>
    </AuthProvider>
  );
  await settle();
  return view;
}

function signal(connection: FakeLiveConnection, id: number, actorUserId: number | null, keyPrefix = "") {
  act(() => connection.emitSignal({ kind: "record", key: `${keyPrefix}sample:${id}`, actorUserId }));
}

beforeEach(() => {
  localStorage.clear();
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.restoreAllMocks();
});

describe("RecordChangedBanner", () => {
  it("banner appears on a signal from another user", async () => {
    const connection = createFakeLiveConnection();
    await mount(connection, <Harness id={101} onReload={vi.fn()} />);
    expect(screen.queryByRole("status")).toBeNull();

    signal(connection, 101, 99);

    expect(screen.getByRole("status").textContent).toMatch(BANNER_TEXT);
  });

  it("ignores signals for other records and other kinds", async () => {
    const connection = createFakeLiveConnection();
    await mount(connection, <Harness id={102} onReload={vi.fn()} />);

    signal(connection, 999, 99);
    act(() => connection.emitSignal({ kind: "counters", key: "sample:102", actorUserId: 99 }));

    expect(screen.queryByRole("status")).toBeNull();
  });

  it("not for this tab's own save", async () => {
    const connection = createFakeLiveConnection();
    await mount(connection, <Harness id={103} onReload={vi.fn()} />);

    noteOwnSave("sample", 103);
    signal(connection, 103, ME);

    expect(screen.queryByRole("status")).toBeNull();
  });

  it("withdraws the banner when the save response arrives after its own signal", async () => {
    const connection = createFakeLiveConnection();
    await mount(connection, <Harness id={104} onReload={vi.fn()} />);

    signal(connection, 104, ME);
    expect(screen.getByRole("status")).toBeTruthy();

    act(() => noteOwnSave("sample", 104));

    expect(screen.queryByRole("status")).toBeNull();
  });

  it("shown for the same user's other tab", async () => {
    const connection = createFakeLiveConnection();
    await mount(connection, <Harness id={105} onReload={vi.fn()} />);

    // This tab never saved record 105: the same user's signal came from another tab.
    signal(connection, 105, ME, "record:");

    expect(screen.getByRole("status").textContent).toMatch(BANNER_TEXT);
  });

  it("shown for the same user when the own save was more than 5 seconds ago", async () => {
    const connection = createFakeLiveConnection();
    const start = Date.now();
    await mount(connection, <Harness id={106} onReload={vi.fn()} />);

    noteOwnSave("sample", 106);
    vi.spyOn(Date, "now").mockReturnValue(start + 6000);
    signal(connection, 106, ME);

    expect(screen.getByRole("status")).toBeTruthy();
  });

  it("Reload with a dirty form asks to discard unsaved changes with Cancel focused", async () => {
    const user = userEvent.setup();
    const onReload = vi.fn();
    const connection = createFakeLiveConnection();
    await mount(connection, <Harness id={107} dirty onReload={onReload} />);
    signal(connection, 107, 99);

    await user.click(screen.getByRole("button", { name: "Reload" }));

    expect(await screen.findByText("Discard your unsaved changes?")).toBeTruthy();
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Cancel" }));
    expect(onReload).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Cancel" }));
    expect(onReload).not.toHaveBeenCalled();
    expect(await screen.findByRole("status")).toBeTruthy();

    await user.click(await screen.findByRole("button", { name: "Reload" }));
    await user.click(await screen.findByRole("button", { name: "Discard and reload" }));
    expect(onReload).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole("status")).toBeNull();
  });

  it("Reload with a clean form reloads without asking", async () => {
    const user = userEvent.setup();
    const onReload = vi.fn();
    const connection = createFakeLiveConnection();
    await mount(connection, <Harness id={108} onReload={onReload} />);
    signal(connection, 108, 99);

    await user.click(screen.getByRole("button", { name: "Reload" }));

    expect(onReload).toHaveBeenCalledTimes(1);
    expect(screen.queryByText("Discard your unsaved changes?")).toBeNull();
  });

  it("the page never reloads by itself and form values are untouched while the banner shows", async () => {
    const user = userEvent.setup();
    const onReload = vi.fn();
    const connection = createFakeLiveConnection();
    await mount(connection, <Harness id={109} dirty onReload={onReload} />);
    await user.type(screen.getByLabelText("Notes"), "my draft");

    signal(connection, 109, 99);
    signal(connection, 109, 98);
    await settle();

    expect(screen.getByRole("status")).toBeTruthy();
    expect((screen.getByLabelText("Notes") as HTMLInputElement).value).toBe("my draft");
    expect(onReload).not.toHaveBeenCalled();
  });

  it("banner has role=status and a Reload button reachable by keyboard", async () => {
    const user = userEvent.setup();
    const onReload = vi.fn();
    const connection = createFakeLiveConnection();
    await mount(connection, <Harness id={110} onReload={onReload} />);
    signal(connection, 110, 99);

    expect(screen.getByRole("status")).toBeTruthy();
    await user.tab(); // Notes input
    await user.tab(); // Reload
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Reload" }));
    await user.keyboard("{Enter}");

    expect(onReload).toHaveBeenCalledTimes(1);
  });

  it("watches while mounted and unwatches on unmount", async () => {
    const connection = createFakeLiveConnection();
    const user = userEvent.setup();
    function Toggle() {
      const [shown, setShown] = useState(true);
      return (
        <>
          <button type="button" onClick={() => setShown(false)}>Close screen</button>
          {shown && <Harness id={111} onReload={vi.fn()} />}
        </>
      );
    }
    await mount(connection, <Toggle />);
    expect(connection.invoke).toHaveBeenCalledWith("WatchRecord", "sample", 111);

    await user.click(screen.getByRole("button", { name: "Close screen" }));

    expect(connection.invoke).toHaveBeenCalledWith("UnwatchRecord", "sample", 111);
  });
});
