import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { ChangeControlPrompt } from "./ChangeControlPrompt";
import type { ApprovedChangeControl } from "../services/changeControlLookupService";

// Network boundary only: the lookup service. The prompt itself is real.
const lookup = vi.hoisted(() => ({ listApproved: vi.fn(), getSectionSetting: vi.fn() }));
vi.mock("../services/changeControlLookupService", () => ({ changeControlLookupService: lookup }));

function change(id: number, number: string, title: string): ApprovedChangeControl {
  return { id, number, type: "ChangeControl", title, status: "ApprovedForImplementation", sectionId: 3, sectionName: "Micro", version: 1 };
}

function mount(props: Partial<React.ComponentProps<typeof ChangeControlPrompt>> = {}) {
  const onSubmit = vi.fn();
  const onCancel = vi.fn();
  render(
    <PinnedLightTheme>
      <ChangeControlPrompt open sectionId={3} refusal={null} initialNumber="" onSubmit={onSubmit} onCancel={onCancel} {...props} />
    </PinnedLightTheme>
  );
  return { onSubmit, onCancel };
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("ChangeControlPrompt", () => {
  it("asks the server for the approved change controls of that laboratory only", async () => {
    lookup.listApproved.mockResolvedValue([]);
    mount({ sectionId: 3 });
    await waitFor(() => expect(lookup.listApproved).toHaveBeenCalledWith(3, expect.any(AbortSignal)));
  });

  it("lists the approved change controls and submits the chosen number", async () => {
    const user = userEvent.setup();
    lookup.listApproved.mockResolvedValue([change(1, "CC-2026-0001", "Raise the limit"), change(2, "CC-2026-0002", "Rename the test")]);
    const { onSubmit } = mount({ refusal: "Needs a change control." });

    const field = await screen.findByRole("combobox", { name: "Approved change control" });
    await user.click(field);
    await user.click(await screen.findByRole("option", { name: /CC-2026-0002/ }));
    await user.click(screen.getByRole("button", { name: /Retry with this change control/ }));

    expect(onSubmit).toHaveBeenCalledWith("CC-2026-0002");
  });

  it("filters the list by what is typed (number or title)", async () => {
    const user = userEvent.setup();
    lookup.listApproved.mockResolvedValue([change(1, "CC-2026-0001", "Raise the limit"), change(2, "CC-2026-0002", "Rename the test")]);
    mount();

    const field = await screen.findByRole("combobox", { name: "Approved change control" });
    await user.type(field, "rename");

    expect(await screen.findByRole("option", { name: /CC-2026-0002/ })).toBeTruthy();
    expect(screen.queryByRole("option", { name: /CC-2026-0001/ })).toBeNull();
  });

  it("shows the refusal text in an alert and offers the retry label after a refusal", async () => {
    lookup.listApproved.mockResolvedValue([]);
    mount({ refusal: "Change control CC-9 does not list this record." });

    expect((await screen.findByRole("alert")).textContent).toContain("Change control CC-9 does not list this record.");
    expect(screen.getByRole("button", { name: "Retry with this change control" })).toBeTruthy();
  });

  it("uses the save label and no alert before the first save", async () => {
    lookup.listApproved.mockResolvedValue([]);
    mount({ refusal: null });

    expect(await screen.findByRole("button", { name: "Save with this change control" })).toBeTruthy();
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("says so when no change control is approved, and still accepts a typed number", async () => {
    const user = userEvent.setup();
    lookup.listApproved.mockResolvedValue([]);
    const { onSubmit } = mount({ refusal: "x" });

    expect(await screen.findByText(/No change control is approved for implementation/)).toBeTruthy();
    await user.type(screen.getByRole("combobox", { name: "Approved change control" }), "cc-7");
    await user.click(screen.getByRole("button", { name: /Retry with this change control/ }));

    expect(onSubmit).toHaveBeenCalledWith("CC-7");
  });

  it("falls back to typing the number when the list cannot be loaded (no view permission)", async () => {
    lookup.listApproved.mockRejectedValue({ response: { status: 403 } });
    mount({ refusal: "x" });

    expect(await screen.findByText(/could not be loaded. Type the change control number/)).toBeTruthy();
    expect((screen.getByRole("combobox", { name: "Approved change control" }) as HTMLInputElement).disabled).toBe(false);
  });

  it("keeps the submit button disabled until the number is valid, and explains why", async () => {
    const user = userEvent.setup();
    lookup.listApproved.mockResolvedValue([]);
    mount({ refusal: "x" });
    const submit = screen.getByRole("button", { name: /Retry with this change control/ });
    expect((submit as HTMLButtonElement).disabled).toBe(true);

    await user.type(screen.getByRole("combobox", { name: "Approved change control" }), "no spaces!");

    expect((submit as HTMLButtonElement).disabled).toBe(true);
    expect(await screen.findByText(/capital letters, digits and hyphens/)).toBeTruthy();
  });

  it("starts with the number that was tried last", async () => {
    lookup.listApproved.mockResolvedValue([]);
    mount({ refusal: "x", initialNumber: "CC-2026-0002" });

    expect(await screen.findByDisplayValue("CC-2026-0002")).toBeTruthy();
  });

  it("cancels from the button and from Escape, and puts focus in the field", async () => {
    const user = userEvent.setup();
    lookup.listApproved.mockResolvedValue([]);
    const { onCancel } = mount({ refusal: "x" });
    const dialog = await screen.findByRole("dialog", { name: "Change control needed" });
    await waitFor(() => expect(document.activeElement).toBe(within(dialog).getByRole("combobox")));

    await user.keyboard("{Escape}");
    expect(onCancel).toHaveBeenCalledTimes(1);
    await user.click(within(dialog).getByRole("button", { name: "Cancel" }));
    expect(onCancel).toHaveBeenCalledTimes(2);
  });
});
