import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { HelpPanelProvider } from "./HelpPanelContext";
import { HelpMenu } from "./HelpMenu";

const versionService = vi.hoisted(() => ({ get: vi.fn() }));
vi.mock("../../services/systemVersionService", () => ({ systemVersionService: versionService }));

afterEach(() => {
  vi.clearAllMocks();
});

function renderMenu() {
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <HelpPanelProvider>
          <HelpMenu />
        </HelpPanelProvider>
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("HelpMenu", () => {
  it("has an accessible name and opens a menu with the six entries", async () => {
    const user = userEvent.setup();
    renderMenu();

    const button = screen.getByRole("button", { name: "Help" });
    expect(button.getAttribute("aria-expanded")).toBe("false");
    await user.click(button);

    const menu = await screen.findByRole("menu");
    const labels = within(menu).getAllByRole("menuitem").map((item) => item.textContent ?? "");
    expect(labels).toEqual([
      "Help for this page",
      "Full guide",
      "Keyboard shortcuts",
      "What's newNot available yet",
      "Replay tourNot available yet",
      "About"
    ]);
    expect(button.getAttribute("aria-expanded")).toBe("true");
    // Entries built in later tasks are shown as unavailable, not as working links.
    expect(within(menu).getByRole("menuitem", { name: /Full guide/ }).getAttribute("aria-disabled")).toBeNull();
    expect(within(menu).getByRole("menuitem", { name: "Keyboard shortcuts" }).getAttribute("aria-disabled")).toBeNull();
    expect(within(menu).getByRole("menuitem", { name: /Replay tour/ }).getAttribute("aria-disabled")).toBe("true");
    expect(within(menu).getByRole("menuitem", { name: "About" }).getAttribute("aria-disabled")).toBeNull();
  });

  it("About shows the application version and the server version and environment", async () => {
    const user = userEvent.setup();
    versionService.get.mockResolvedValue({ version: "9.8.7+abc1234", environment: "Staging" });
    renderMenu();

    await user.click(screen.getByRole("button", { name: "Help" }));
    await user.click(await screen.findByRole("menuitem", { name: "About" }));

    const dialog = await screen.findByRole("dialog", { name: "About MicroLIMS" });
    expect(within(dialog).getByText(__APP_VERSION__)).toBeTruthy();
    expect(await within(dialog).findByText("9.8.7+abc1234")).toBeTruthy();
    expect(within(dialog).getByText("Staging")).toBeTruthy();
    expect(versionService.get).toHaveBeenCalledTimes(1);
  });

  it("shows the failure and offers a retry when the server version cannot be loaded", async () => {
    const user = userEvent.setup();
    versionService.get.mockRejectedValueOnce(new Error("offline"));
    versionService.get.mockResolvedValueOnce({ version: "1.2.3", environment: "Production" });
    renderMenu();

    await user.click(screen.getByRole("button", { name: "Help" }));
    await user.click(await screen.findByRole("menuitem", { name: "About" }));

    const dialog = await screen.findByRole("dialog", { name: "About MicroLIMS" });
    const alert = await within(dialog).findByRole("alert");
    expect(alert.textContent).toContain("The server version could not be loaded.");
    // The application version does not depend on the server.
    expect(within(dialog).getByText(__APP_VERSION__)).toBeTruthy();

    await user.click(within(alert).getByRole("button", { name: "Retry" }));
    expect(await within(dialog).findByText("1.2.3")).toBeTruthy();
    expect(within(dialog).getByText("Production")).toBeTruthy();
    expect(within(dialog).queryByRole("alert")).toBeNull();
  });

  it("returns focus to the Help button when About is closed", async () => {
    const user = userEvent.setup();
    versionService.get.mockResolvedValue({ version: "1.0.0", environment: "Development" });
    renderMenu();

    const button = screen.getByRole("button", { name: "Help" });
    await user.click(button);
    await user.click(await screen.findByRole("menuitem", { name: "About" }));
    const dialog = await screen.findByRole("dialog", { name: "About MicroLIMS" });
    await user.click(within(dialog).getByRole("button", { name: "Close" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(button));
  });

  it("styles the dialog title with a span, not a nested heading", async () => {
    const user = userEvent.setup();
    versionService.get.mockResolvedValue({ version: "1.0.0", environment: "Development" });
    renderMenu();

    await user.click(screen.getByRole("button", { name: "Help" }));
    await user.click(await screen.findByRole("menuitem", { name: "About" }));
    const dialog = await screen.findByRole("dialog", { name: "About MicroLIMS" });
    const title = within(dialog).getByText("About MicroLIMS");
    expect(title.tagName).toBe("SPAN");
    expect(dialog.querySelectorAll("h1 h1, h1 h2, h1 h3, h2 h1, h2 h2, h2 h3, h2 h4, h2 h5, h2 h6")).toHaveLength(0);
  });

  it("ShortcutsDialog lists the keys and opens from the help menu and from '?' when no input is focused", async () => {
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <HelpPanelProvider>
            <HelpMenu />
            <input aria-label="Sample input" />
            <textarea aria-label="Sample notes" />
            <div contentEditable="true" aria-label="Editable div" />
          </HelpPanelProvider>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    // 1. Opens from the Help menu
    await user.click(screen.getByRole("button", { name: "Help" }));
    await user.click(await screen.findByRole("menuitem", { name: "Keyboard shortcuts" }));
    const dialog = await screen.findByRole("dialog", { name: "Keyboard shortcuts" });
    expect(within(dialog).getByText(/Ctrl\+D/)).toBeTruthy();
    expect(within(dialog).getByText(/Ctrl\+Enter/)).toBeTruthy();
    await user.click(within(dialog).getByRole("button", { name: "Close" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    // 2. '?' typed inside an input, textarea, or contenteditable does NOT open it
    await user.click(screen.getByLabelText("Sample input"));
    await user.keyboard("?");
    expect(screen.queryByRole("dialog", { name: "Keyboard shortcuts" })).toBeNull();

    await user.click(screen.getByLabelText("Sample notes"));
    await user.keyboard("?");
    expect(screen.queryByRole("dialog", { name: "Keyboard shortcuts" })).toBeNull();

    await user.click(screen.getByLabelText("Editable div"));
    await user.keyboard("?");
    expect(screen.queryByRole("dialog", { name: "Keyboard shortcuts" })).toBeNull();

    // 3. '?' when no input is focused opens ShortcutsDialog
    (document.activeElement as HTMLElement | null)?.blur();
    await user.keyboard("?");
    expect(await screen.findByRole("dialog", { name: "Keyboard shortcuts" })).toBeTruthy();
  });
});
