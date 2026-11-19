import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { AxiosError, AxiosHeaders } from "axios";
import { isUiStateKey, resetUiStateCache, useUiState } from "./useUiState";

const service = vi.hoisted(() => ({ getAll: vi.fn(), set: vi.fn(), remove: vi.fn() }));
vi.mock("../services/uiStateService", () => ({ uiStateService: service }));

function Probe({ stateKey }: { stateKey: string }) {
  const { value, loaded, set, remove } = useUiState(stateKey);
  return (
    <div>
      <output aria-label={`value of ${stateKey}`}>{loaded ? (value ?? "none") : "loading"}</output>
      <button type="button" onClick={() => void set("done")}>set {stateKey}</button>
      <button type="button" onClick={() => void remove()}>remove {stateKey}</button>
    </div>
  );
}

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem("microlims_user_id", "7");
  resetUiStateCache();
});

afterEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  resetUiStateCache();
});

describe("useUiState", () => {
  it("reads the value from the server store", async () => {
    service.getAll.mockResolvedValue({ "tour.analyst.v1": "skipped" });
    render(<Probe stateKey="tour.analyst.v1" />);

    expect(screen.getByLabelText("value of tour.analyst.v1").textContent).toBe("loading");
    await waitFor(() => expect(screen.getByLabelText("value of tour.analyst.v1").textContent).toBe("skipped"));
  });

  it("sends one request for many components and passes an abort signal", async () => {
    service.getAll.mockResolvedValue({ "a.one": "1", "b.two": "2" });
    render(<><Probe stateKey="a.one" /><Probe stateKey="b.two" /></>);

    await waitFor(() => expect(screen.getByLabelText("value of b.two").textContent).toBe("2"));
    expect(service.getAll).toHaveBeenCalledTimes(1);
    expect(service.getAll.mock.calls[0][0]).toBeInstanceOf(AbortSignal);
  });

  it("falls back to localStorage when the service rejects", async () => {
    const user = userEvent.setup();
    service.getAll.mockRejectedValue(new Error("offline"));
    service.set.mockRejectedValue(new Error("offline"));
    service.remove.mockRejectedValue(new Error("offline"));
    localStorage.setItem("microlims.uiState.7.tour.analyst.v1", "skipped");
    render(<Probe stateKey="tour.analyst.v1" />);

    await waitFor(() => expect(screen.getByLabelText("value of tour.analyst.v1").textContent).toBe("skipped"));

    await user.click(screen.getByRole("button", { name: "set tour.analyst.v1" }));
    await waitFor(() => expect(screen.getByLabelText("value of tour.analyst.v1").textContent).toBe("done"));
    expect(localStorage.getItem("microlims.uiState.7.tour.analyst.v1")).toBe("done");

    await user.click(screen.getByRole("button", { name: "remove tour.analyst.v1" }));
    await waitFor(() => expect(screen.getByLabelText("value of tour.analyst.v1").textContent).toBe("none"));
    expect(localStorage.getItem("microlims.uiState.7.tour.analyst.v1")).toBeNull();
  });

  it("stores and removes through the service when it is reachable, leaving localStorage alone", async () => {
    const user = userEvent.setup();
    service.getAll.mockResolvedValue({});
    service.set.mockResolvedValue(undefined);
    service.remove.mockResolvedValue(undefined);
    render(<Probe stateKey="whatsnew.seen" />);
    await waitFor(() => expect(screen.getByLabelText("value of whatsnew.seen").textContent).toBe("none"));

    await user.click(screen.getByRole("button", { name: "set whatsnew.seen" }));
    await waitFor(() => expect(screen.getByLabelText("value of whatsnew.seen").textContent).toBe("done"));
    expect(service.set).toHaveBeenCalledWith("whatsnew.seen", "done");
    expect(localStorage.getItem("microlims.uiState.7.whatsnew.seen")).toBeNull();

    await user.click(screen.getByRole("button", { name: "remove whatsnew.seen" }));
    await waitFor(() => expect(screen.getByLabelText("value of whatsnew.seen").textContent).toBe("none"));
    expect(service.remove).toHaveBeenCalledWith("whatsnew.seen");
  });

  it("keeps the other server values when one write falls back to localStorage", async () => {
    const user = userEvent.setup();
    service.getAll.mockResolvedValue({ "a.one": "1", "b.two": "2" });
    service.set.mockRejectedValue(new Error("offline"));
    render(<><Probe stateKey="a.one" /><Probe stateKey="b.two" /></>);
    await waitFor(() => expect(screen.getByLabelText("value of b.two").textContent).toBe("2"));

    await user.click(screen.getByRole("button", { name: "set b.two" }));

    await waitFor(() => expect(screen.getByLabelText("value of b.two").textContent).toBe("done"));
    expect(localStorage.getItem("microlims.uiState.7.b.two")).toBe("done");
    // The server value of the other key is still shown, not lost to the fallback.
    expect(screen.getByLabelText("value of a.one").textContent).toBe("1");
  });

  it("rejects a refusal from the server instead of hiding it in localStorage", async () => {
    const user = userEvent.setup();
    service.getAll.mockResolvedValue({ "b.two": "2" });
    const refusal = new AxiosError("Bad request", "ERR_BAD_REQUEST", undefined, undefined, {
      status: 400, statusText: "Bad Request", data: { message: "The key must start with a lowercase letter" },
      headers: {}, config: { headers: new AxiosHeaders() }
    });
    service.set.mockRejectedValue(refusal);
    function SetProbe() {
      const { value, set } = useUiState("b.two");
      const [error, setError] = useState("");
      return (
        <div>
          <output aria-label="value">{value ?? "none"}</output>
          <output aria-label="error">{error}</output>
          <button type="button" onClick={() => set("done").catch((e: unknown) => setError(e === refusal ? "refused" : "other"))}>set</button>
        </div>
      );
    }
    render(<SetProbe />);
    await waitFor(() => expect(screen.getByLabelText("value").textContent).toBe("2"));

    await user.click(screen.getByRole("button", { name: "set" }));

    await waitFor(() => expect(screen.getByLabelText("error").textContent).toBe("refused"));
    expect(screen.getByLabelText("value").textContent).toBe("2");
    expect(localStorage.getItem("microlims.uiState.7.b.two")).toBeNull();
  });

  it("does not show one user's local values to another user", async () => {
    service.getAll.mockRejectedValue(new Error("offline"));
    localStorage.setItem("microlims.uiState.7.flag.x", "seven");
    localStorage.setItem("microlims.uiState.8.flag.x", "eight");
    localStorage.setItem("microlims_user_id", "8");
    render(<Probe stateKey="flag.x" />);

    await waitFor(() => expect(screen.getByLabelText("value of flag.x").textContent).toBe("eight"));
  });

  it("cancels the request when the last component unmounts", async () => {
    let captured: AbortSignal | undefined;
    service.getAll.mockImplementation((signal: AbortSignal) => {
      captured = signal;
      return new Promise(() => {});
    });
    const { unmount } = render(<Probe stateKey="a.one" />);
    await waitFor(() => expect(captured).toBeDefined());

    unmount();
    expect(captured?.aborted).toBe(true);
  });
});

describe("interface state keys", () => {
  // The same cases as the server's UserUiStatePostgresTests: a key the server
  // refuses would never be saved, so the hook refuses it up front.
  it.each(["", "a", "Tour.done", "1tour", "tour-done", "tour done", "tour_done", "a" + "b".repeat(64)])(
    "%j is refused as the server refuses it",
    (key) => {
      expect(isUiStateKey(key)).toBe(false);
    }
  );

  it.each(["nav.lastLab", "nav.tab.equipmentConfiguration", "tour.SectionHead.v1", "a" + "b".repeat(63)])(
    "%j is accepted",
    (key) => {
      expect(isUiStateKey(key)).toBe(true);
    }
  );

  it("a component that uses a refused key fails at once instead of silently never saving", () => {
    service.getAll.mockResolvedValue({});
    const quiet = vi.spyOn(console, "error").mockImplementation(() => undefined);
    expect(() => render(<Probe stateKey="nav.tab.equipment-configuration" />)).toThrow(/key rule/);
    quiet.mockRestore();
  });
});

