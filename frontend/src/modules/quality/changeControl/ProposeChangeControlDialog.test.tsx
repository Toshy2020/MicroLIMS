import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

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

import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ProposeChangeControlDialog } from "./ProposeChangeControlDialog";
import { refusal } from "../testFixtures";
import { ok, serveLookups } from "../steps/stepTestUtils";

const SEARCH = {
  query: "Incub",
  totalMatches: 1,
  executionTimeMs: 1,
  categories: [
    {
      categoryName: "Equipment",
      count: 1,
      items: [{ id: 4, entityType: "Equipment", title: "Incubator 3", subtitle: "Room 2", status: "", matchedField: "", matchedValue: "", badgeColor: "", navigationTarget: "" }]
    }
  ]
};

function renderDialog(onCreated = vi.fn().mockResolvedValue(undefined), onClose = vi.fn()) {
  render(
    <PinnedLightTheme>
      <ProposeChangeControlDialog open onClose={onClose} onCreated={onCreated} />
    </PinnedLightTheme>
  );
  return { onCreated, onClose };
}

async function fillBasics(user: ReturnType<typeof userEvent.setup>) {
  const dialog = await screen.findByRole("dialog");
  await user.type(within(dialog).getByLabelText(/^Title/), "Move incubators");
  await user.type(within(dialog).getByLabelText(/^What changes/), "Two incubators move to room 4");
  await user.type(within(dialog).getByLabelText(/^Why/), "Room 2 is being renovated");
  return dialog;
}

describe("ProposeChangeControlDialog", () => {
  beforeEach(() => {
    serveLookups(http, {
      "/org/my-sections": () => ok([{ sectionId: 1, sectionCode: "MIC", sectionName: "Microbiology", departmentId: 1, departmentCode: "QC", departmentName: "QC" }]),
      "/search/global": () => ok(SEARCH)
    });
    http.post.mockImplementation(() => ok({ id: 21, version: 1, record: { id: 21, number: "CC-2026-0003" } }));
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("proposes with the section, the owner and the affected records found with the search", async () => {
    const user = userEvent.setup();
    const { onCreated } = renderDialog();

    const dialog = await fillBasics(user);
    await user.click(within(dialog).getByRole("combobox", { name: /^Laboratory section/ }));
    await user.click(await screen.findByRole("option", { name: "Microbiology" }));
    await user.click(within(dialog).getByRole("combobox", { name: /^Owner/ }));
    await user.click(await screen.findByRole("option", { name: "Sam Head (shead)" }));
    await user.click(within(dialog).getByRole("combobox", { name: /^Kind of record/ }));
    await user.click(await screen.findByRole("option", { name: "Equipment" }));
    await user.type(within(dialog).getByLabelText("Search for the record"), "Incub");
    await user.click(await within(dialog).findByRole("button", { name: "Choose Incubator 3" }));
    expect(within(dialog).getByText("Equipment: Incubator 3")).toBeTruthy();

    await user.click(within(dialog).getByRole("button", { name: "Propose" }));

    await waitFor(() => {
      expect(http.post).toHaveBeenCalledWith("/quality/change-controls", {
        title: "Move incubators",
        description: "Two incubators move to room 4",
        reason: "Room 2 is being renovated",
        sectionId: 1,
        affected: [{ targetType: "Equipment", targetId: 4, newRecordCode: null }],
        ownerUserId: 8
      });
    });
    await waitFor(() => expect(onCreated).toHaveBeenCalledTimes(1));
    expect(onCreated.mock.calls[0][0].record.number).toBe("CC-2026-0003");
  });

  it("proposes for the whole site with no owner chosen (the server uses the proposer) and a new affected record", async () => {
    const user = userEvent.setup();
    renderDialog();

    const dialog = await fillBasics(user);
    await user.click(within(dialog).getByRole("radio", { name: "New record" }));
    await user.type(within(dialog).getByLabelText("Planned code or name"), "TD-NEW-PH");
    await user.click(within(dialog).getByRole("button", { name: "Add new record" }));
    await user.click(within(dialog).getByRole("button", { name: "Propose" }));

    await waitFor(() => {
      expect(http.post).toHaveBeenCalledWith("/quality/change-controls", {
        title: "Move incubators",
        description: "Two incubators move to room 4",
        reason: "Room 2 is being renovated",
        sectionId: null,
        affected: [{ targetType: "TestDefinition", targetId: null, newRecordCode: "TD-NEW-PH" }],
        ownerUserId: null
      });
    });
  });

  it("removes an affected record from the list before proposing", async () => {
    const user = userEvent.setup();
    renderDialog();

    const dialog = await fillBasics(user);
    await user.click(within(dialog).getByRole("radio", { name: "New record" }));
    await user.type(within(dialog).getByLabelText("Planned code or name"), "TD-NEW-PH");
    await user.click(within(dialog).getByRole("button", { name: "Add new record" }));
    await user.click(within(dialog).getByRole("button", { name: "Remove Test definition: TD-NEW-PH" }));

    expect(within(dialog).queryByText("Test definition: TD-NEW-PH")).toBeNull();
  });

  it("cannot be submitted without a title, a description and a reason", async () => {
    renderDialog();

    const dialog = await screen.findByRole("dialog");
    expect((within(dialog).getByRole("button", { name: "Propose" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("keeps the dialog open and shows the server's refusal", async () => {
    const user = userEvent.setup();
    http.post.mockRejectedValue(refusal("Select an active Section Head or QC Manager in the change control's section as the owner."));
    const { onCreated } = renderDialog();

    const dialog = await fillBasics(user);
    await user.click(within(dialog).getByRole("button", { name: "Propose" }));

    expect(await within(dialog).findByText("Select an active Section Head or QC Manager in the change control's section as the owner.")).toBeTruthy();
    expect(onCreated).not.toHaveBeenCalled();
  });
});
