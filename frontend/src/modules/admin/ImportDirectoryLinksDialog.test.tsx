import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { ImportDirectoryLinksDialog } from "./ImportDirectoryLinksDialog";
import { SsoAdminService } from "./services/SsoAdminService";

vi.mock("./services/SsoAdminService", () => ({
  SsoAdminService: { getOverview: vi.fn(), testConnection: vi.fn(), previewLinks: vi.fn(), importLinks: vi.fn() }
}));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

const MIXED = {
  rows: [
    { row: 1, username: "jdoe", status: "Ok" as const, message: "Will link to john.doe" },
    { row: 2, username: "ghost", status: "UnknownUser" as const, message: "No such user" }
  ]
};

function renderDialog(onImported = vi.fn()) {
  render(<PinnedLightTheme><ImportDirectoryLinksDialog open onClose={() => {}} onImported={onImported} /></PinnedLightTheme>);
  return onImported;
}

async function previewMixed(user: ReturnType<typeof userEvent.setup>) {
  vi.mocked(SsoAdminService.previewLinks).mockResolvedValue(MIXED);
  await user.type(screen.getByLabelText("CSV rows"), "username,companyUsername{enter}jdoe,john.doe{enter}ghost,g.host");
  await user.click(screen.getByRole("button", { name: "Preview" }));
  await screen.findByRole("table", { name: "Directory link preview" });
}

describe("ImportDirectoryLinksDialog", () => {
  it("import: pasted CSV is previewed with per-row status as text, header row skipped", async () => {
    const user = userEvent.setup();
    renderDialog();
    await previewMixed(user);

    expect(SsoAdminService.previewLinks).toHaveBeenCalledWith([
      { username: "jdoe", companyUsername: "john.doe" },
      { username: "ghost", companyUsername: "g.host" }
    ]);
    const table = screen.getByRole("table", { name: "Directory link preview" });
    expect(within(table).getByText("Ok")).toBeTruthy();
    expect(within(table).getByText("UnknownUser")).toBeTruthy();
    expect(within(table).getByText("No such user")).toBeTruthy();
  });

  it("import: an uploaded CSV file is read into the preview input", async () => {
    const user = userEvent.setup();
    renderDialog();
    const file = new File(["username,companyUsername\njdoe,john.doe\n"], "links.csv", { type: "text/csv" });
    await user.upload(screen.getByLabelText("CSV file"), file);
    await waitFor(() => expect((screen.getByLabelText("CSV rows") as HTMLTextAreaElement).value).toContain("jdoe,john.doe"));
  });

  it("import: Import is disabled while any row is invalid unless Skip invalid rows is ticked", async () => {
    const user = userEvent.setup();
    vi.mocked(SsoAdminService.importLinks).mockResolvedValue({ ...MIXED, applied: 1 });
    renderDialog();
    await previewMixed(user);

    const importButton = screen.getByRole("button", { name: "Import" }) as HTMLButtonElement;
    expect(importButton.disabled).toBe(true);
    expect(SsoAdminService.importLinks).not.toHaveBeenCalled();

    await user.click(screen.getByRole("checkbox", { name: "Skip invalid rows" }));
    expect(importButton.disabled).toBe(false);
    await user.click(importButton);
    await waitFor(() => expect(SsoAdminService.importLinks).toHaveBeenCalledTimes(1));
  });

  it("import: the result summary is announced in a status region and the page is told to reload", async () => {
    const user = userEvent.setup();
    vi.mocked(SsoAdminService.importLinks).mockResolvedValue({ ...MIXED, applied: 1 });
    const onImported = renderDialog();
    await previewMixed(user);
    await user.click(screen.getByRole("checkbox", { name: "Skip invalid rows" }));
    await user.click(screen.getByRole("button", { name: "Import" }));

    await waitFor(() => expect(screen.getByRole("status").textContent).toBe("Imported 1 directory link; 1 row skipped."));
    expect(onImported).toHaveBeenCalledTimes(1);
    // Cannot be imported twice.
    expect((screen.getByRole("button", { name: "Import" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("import: a failed preview shows the server's message", async () => {
    const user = userEvent.setup();
    vi.mocked(SsoAdminService.previewLinks).mockRejectedValue({ response: { data: { message: "Too many rows." } } });
    renderDialog();
    await user.type(screen.getByLabelText("CSV rows"), "a,b");
    await user.click(screen.getByRole("button", { name: "Preview" }));
    expect((await screen.findByRole("alert")).textContent).toBe("Too many rows.");
  });
});
