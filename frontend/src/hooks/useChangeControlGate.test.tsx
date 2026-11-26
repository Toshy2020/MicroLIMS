import { useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { useChangeControlGate } from "./useChangeControlGate";
import { masterDataOptions } from "../services/masterDataOptions";
import type { ApprovedChangeControl } from "../services/changeControlLookupService";

// Network boundaries only: apiClient (the save requests) and the change-control lookup service.
const http = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() }));
vi.mock("../services/apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../services/apiClient")>();
  return { ...actual, apiClient: http };
});
const lookup = vi.hoisted(() => ({ listApproved: vi.fn(), getSectionSetting: vi.fn() }));
vi.mock("../services/changeControlLookupService", () => ({ changeControlLookupService: lookup }));

const REFUSAL_TEXT = "This laboratory requires an approved change control for this change.";
const NOT_LISTED_TEXT = "Change control CC-2026-0002 does not list this record.";

function refusal(code: string, message: string) {
  return { response: { status: 403, data: { success: false, message, errorDetails: [{ code, message }] } } };
}

function change(number: string, title: string): ApprovedChangeControl {
  return { id: 1, number, type: "ChangeControl", title, status: "ApprovedForImplementation", sectionId: 3, sectionName: "Micro", version: 1 };
}

const PAYLOAD = { code: "TAMC", displayName: "Total count" };

function Harness({ sectionId = 3, recordKey = "test-20" }: { sectionId?: number | null; recordKey?: string }) {
  const gate = useChangeControlGate({ sectionId, recordKey });
  const [outcome, setOutcome] = useState("");
  const save = () =>
    gate
      .run((cc) => masterDataOptions.updateTestDefinition(20, PAYLOAD, undefined, undefined, 7, "Corrected name", cc))
      .then(
        () => setOutcome("saved"),
        (e: Error) => setOutcome(`failed: ${e.message}`)
      );
  return (
    <PinnedLightTheme>
      <button type="button" onClick={() => void save()}>Save test</button>
      <p role="status">{outcome}</p>
      {gate.prompt}
    </PinnedLightTheme>
  );
}

beforeEach(() => {
  lookup.getSectionSetting.mockResolvedValue({ sectionId: 3, changeControlRequired: false, version: 1 });
  lookup.listApproved.mockResolvedValue([change("CC-2026-0001", "Raise the limit"), change("CC-2026-0002", "Rename the test")]);
  http.put.mockResolvedValue({ data: { data: {} } });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

async function chooseAndSubmit(user: ReturnType<typeof userEvent.setup>, number: string, button: RegExp) {
  const dialog = await screen.findByRole("dialog");
  const field = within(dialog).getByRole("combobox", { name: "Approved change control" });
  await user.clear(field);
  await user.type(field, number);
  await user.click(within(dialog).getByRole("button", { name: button }));
}

describe("useChangeControlGate", () => {
  it("opens the prompt with the server refusal text when the save is refused by the gate", async () => {
    const user = userEvent.setup();
    http.put.mockRejectedValueOnce(refusal("ChangeControl.GateRequired", REFUSAL_TEXT));
    render(<Harness />);

    await user.click(screen.getByRole("button", { name: "Save test" }));

    const dialog = await screen.findByRole("dialog", { name: "Change control needed" });
    expect(within(dialog).getByRole("alert").textContent).toContain(REFUSAL_TEXT);
    expect(http.put).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("status", { hidden: true }).textContent).toBe("");
  });

  it("retry repeats the same request with the header, the unchanged body and the same version", async () => {
    const user = userEvent.setup();
    http.put.mockRejectedValueOnce(refusal("ChangeControl.GateRequired", REFUSAL_TEXT));
    render(<Harness />);
    await user.click(screen.getByRole("button", { name: "Save test" }));

    await chooseAndSubmit(user, "CC-2026-0001", /Retry with this change control/);

    await waitFor(() => expect(screen.getByRole("status").textContent).toContain("saved"));
    expect(http.put).toHaveBeenCalledTimes(2);
    const [firstUrl, firstBody, firstConfig] = http.put.mock.calls[0];
    const [secondUrl, secondBody, secondConfig] = http.put.mock.calls[1];
    expect(firstConfig.headers["X-Change-Control"]).toBeUndefined();
    expect(firstConfig.headers["If-Match"]).toBe('"7"');
    expect(secondUrl).toBe(firstUrl);
    expect(secondBody).toEqual(firstBody);
    expect(secondConfig.headers).toEqual({ ...firstConfig.headers, "X-Change-Control": "CC-2026-0001" });
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("shows 'does not list this record' and does not repeat the save by itself", async () => {
    const user = userEvent.setup();
    http.put
      .mockRejectedValueOnce(refusal("ChangeControl.GateRequired", REFUSAL_TEXT))
      .mockRejectedValueOnce(refusal("ChangeControl.GateRecordNotListed", NOT_LISTED_TEXT));
    render(<Harness />);
    await user.click(screen.getByRole("button", { name: "Save test" }));
    await chooseAndSubmit(user, "CC-2026-0002", /Retry with this change control/);

    await waitFor(() => expect(screen.getByRole("alert").textContent).toContain(NOT_LISTED_TEXT));
    expect(http.put).toHaveBeenCalledTimes(2);
    expect(screen.getByRole("status", { hidden: true }).textContent).toBe("");
    // Still two requests a moment later: nothing is sent until the person chooses again.
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(http.put).toHaveBeenCalledTimes(2);
  });

  it("recognises the refusal by its message code, not by its text", async () => {
    const user = userEvent.setup();
    http.put.mockRejectedValueOnce(refusal("Test.SomethingElse", REFUSAL_TEXT));
    render(<Harness />);

    await user.click(screen.getByRole("button", { name: "Save test" }));

    await waitFor(() => expect(screen.getByRole("status").textContent).toContain("failed"));
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("shows nothing and sends no header when the laboratory gate is off", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole("button", { name: "Save test" }));

    await waitFor(() => expect(screen.getByRole("status").textContent).toContain("saved"));
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(http.put).toHaveBeenCalledTimes(1);
    expect(http.put.mock.calls[0][2].headers["X-Change-Control"]).toBeUndefined();
    expect(lookup.getSectionSetting).toHaveBeenCalledWith(3, expect.anything());
  });

  it("shows nothing before the save when the setting cannot be read", async () => {
    const user = userEvent.setup();
    lookup.getSectionSetting.mockRejectedValue({ response: { status: 403 } });
    render(<Harness />);

    await user.click(screen.getByRole("button", { name: "Save test" }));

    await waitFor(() => expect(screen.getByRole("status").textContent).toContain("saved"));
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("asks before the first save when the setting is on, and sends nothing until a number is chosen", async () => {
    const user = userEvent.setup();
    lookup.getSectionSetting.mockResolvedValue({ sectionId: 3, changeControlRequired: true, version: 2 });
    render(<Harness />);

    await user.click(screen.getByRole("button", { name: "Save test" }));

    const dialog = await screen.findByRole("dialog", { name: "Change control needed" });
    expect(within(dialog).getByText(/requires an approved change control for this change/)).toBeTruthy();
    expect(http.put).not.toHaveBeenCalled();

    await chooseAndSubmit(user, "CC-2026-0001", /Save with this change control/);

    await waitFor(() => expect(screen.getByRole("status").textContent).toContain("saved"));
    expect(http.put).toHaveBeenCalledTimes(1);
    expect(http.put.mock.calls[0][2].headers).toEqual({
      "If-Match": '"7"',
      "X-Change-Reason": "Corrected%20name",
      "X-Change-Control": "CC-2026-0001"
    });
  });

  it("does not send the save when the person cancels the prompt", async () => {
    const user = userEvent.setup();
    lookup.getSectionSetting.mockResolvedValue({ sectionId: 3, changeControlRequired: true, version: 2 });
    render(<Harness />);
    await user.click(screen.getByRole("button", { name: "Save test" }));
    const dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.getByRole("status").textContent).toContain("failed: Not saved: no change control was chosen."));
    expect(http.put).not.toHaveBeenCalled();
  });

  it("closes the prompt on Escape and treats it as a cancel", async () => {
    const user = userEvent.setup();
    http.put.mockRejectedValueOnce(refusal("ChangeControl.GateRequired", REFUSAL_TEXT));
    render(<Harness />);
    await user.click(screen.getByRole("button", { name: "Save test" }));
    await screen.findByRole("dialog");

    await user.keyboard("{Escape}");

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(screen.getByRole("status").textContent).toContain("failed: Not saved: no change control was chosen.");
    expect(http.put).toHaveBeenCalledTimes(1);
  });

  it("remembers the number for the next save of the same record and does not ask again", async () => {
    const user = userEvent.setup();
    http.put.mockRejectedValueOnce(refusal("ChangeControl.GateRequired", REFUSAL_TEXT));
    render(<Harness />);
    await user.click(screen.getByRole("button", { name: "Save test" }));
    await chooseAndSubmit(user, "CC-2026-0001", /Retry with this change control/);
    await waitFor(() => expect(screen.getByRole("status").textContent).toContain("saved"));

    await user.click(screen.getByRole("button", { name: "Save test" }));

    await waitFor(() => expect(http.put).toHaveBeenCalledTimes(3));
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(http.put.mock.calls[2][2].headers["X-Change-Control"]).toBe("CC-2026-0001");
  });

  it("forgets the number when another record is edited", async () => {
    const user = userEvent.setup();
    http.put.mockRejectedValueOnce(refusal("ChangeControl.GateRequired", REFUSAL_TEXT));
    const view = render(<Harness recordKey="test-20" />);
    await user.click(screen.getByRole("button", { name: "Save test" }));
    await chooseAndSubmit(user, "CC-2026-0001", /Retry with this change control/);
    await waitFor(() => expect(screen.getByRole("status").textContent).toContain("saved"));

    view.rerender(<Harness recordKey="test-21" />);
    http.put.mockRejectedValueOnce(refusal("ChangeControl.GateRequired", REFUSAL_TEXT));
    await user.click(screen.getByRole("button", { name: "Save test" }));

    await screen.findByRole("dialog");
    expect(http.put).toHaveBeenCalledTimes(3);
    expect(http.put.mock.calls[2][2].headers["X-Change-Control"]).toBeUndefined();
  });

  it("does not remember a number across editing sessions (a new mount starts empty)", async () => {
    const user = userEvent.setup();
    http.put.mockRejectedValueOnce(refusal("ChangeControl.GateRequired", REFUSAL_TEXT));
    const first = render(<Harness />);
    await user.click(screen.getByRole("button", { name: "Save test" }));
    await chooseAndSubmit(user, "CC-2026-0001", /Retry with this change control/);
    await waitFor(() => expect(screen.getByRole("status").textContent).toContain("saved"));
    first.unmount();

    http.put.mockRejectedValueOnce(refusal("ChangeControl.GateRequired", REFUSAL_TEXT));
    render(<Harness />);
    await user.click(screen.getByRole("button", { name: "Save test" }));

    await screen.findByRole("dialog");
    expect(http.put.mock.calls.at(-1)?.[2].headers["X-Change-Control"]).toBeUndefined();
  });
});
