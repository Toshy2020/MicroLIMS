import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderWithTheme } from "../../components/bench/benchTestUtils";
import { resetDraftDb, signInForTest } from "../../services/drafts/draftTestUtils";
import { LocationResultGridDialog } from "./LocationResultGridDialog";

const service = vi.hoisted(() => ({
  getLocations: vi.fn(),
  recordBatchResults: vi.fn()
}));
vi.mock("./services/TestWorkflowService", () => ({ TestWorkflowService: service }));
vi.mock("../../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));

const location = (id: number, name: string) => ({
  id,
  locationType: "SettlingPlate",
  locationName: name,
  gradeClassification: null,
  alertLimit: "1",
  actionLimit: "3",
  specLimit: "5",
  cfuResult: null,
  calculatedResult: null,
  reportedResult: null,
  rawReadings: null,
  status: null,
  enteredAt: null
});

const LOCATIONS = [location(101, "Filling Room A"), location(102, "Capping Zone B"), location(103, "Packing Hall C")];

function renderDialog() {
  return renderWithTheme(
    <AuthProvider>
      <MemoryRouter>
        <LocationResultGridDialog
          open
          testOrderId={55}
          testCode="EM-AIR"
          displayName="Batch EM-01"
          minReadyAt={null}
          onClose={vi.fn()}
          onSubmitted={vi.fn()}
        />
      </MemoryRouter>
    </AuthProvider>,
    { bench: false }
  );
}

async function openAndPaste(user: ReturnType<typeof userEvent.setup>, text: string, locations = LOCATIONS) {
  service.getLocations.mockResolvedValue(locations);
  renderDialog();
  const first = await screen.findByLabelText(`${locations[0].locationName} reading 1`);
  await user.click(first);
  await user.paste(text);
}

const reading = (name: string) => screen.getByLabelText(`${name} reading 1`) as HTMLInputElement;
const accept = () => screen.getByRole("button", { name: "Accept" }) as HTMLButtonElement;
const preview = () => screen.findByRole("table", { name: /paste preview/i });

beforeEach(async () => {
  localStorage.clear();
  await resetDraftDb();
  signInForTest(7);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  localStorage.clear();
});

describe("LocationResultGridDialog paste", () => {
  it("a shuffled block lands every count on the row with the same location name", async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "Packing Hall C\t9\nFilling Room A\t3\nCapping Zone B\t6\n");
    await preview();
    await user.click(accept());

    expect(reading("Filling Room A").value).toBe("3");
    expect(reading("Capping Zone B").value).toBe("6");
    expect(reading("Packing Hall C").value).toBe("9");
  });

  it("warns how many entered readings the paste replaces, without blocking, and replaces them on Accept", async () => {
    const user = userEvent.setup();
    service.getLocations.mockResolvedValue(LOCATIONS);
    renderDialog();
    const first = await screen.findByLabelText("Filling Room A reading 1");
    await user.type(first, "5");
    await user.click(screen.getByRole("button", { name: "Add plate reading for Filling Room A" }));
    await user.type(screen.getByLabelText("Filling Room A reading 2"), "7");

    await user.click(reading("Capping Zone B"));
    await user.paste("Filling Room A\t3\nCapping Zone B\t6\n");
    await preview();

    expect(screen.getByRole("status").textContent).toBe(
      "This will replace 2 readings already entered in 1 row. Check them before accepting."
    );
    const replaced = screen.getByRole("list", { name: /readings that will be replaced/i });
    expect(within(replaced).getByText("Filling Room A (2 readings)")).toBeTruthy();
    expect(accept().disabled).toBe(false);

    await user.click(accept());
    expect(reading("Filling Room A").value).toBe("3");
    expect(screen.queryByLabelText("Filling Room A reading 2")).toBeNull();
    expect(reading("Capping Zone B").value).toBe("6");
  });

  it("shows no replace warning when the pasted rows are still empty", async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "Filling Room A\t3\n");
    await preview();
    expect(screen.queryByText(/This will replace/)).toBeNull();
    expect(screen.queryByRole("list", { name: /readings that will be replaced/i })).toBeNull();
  });

  it("matches names after trimming and ignoring case", async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "  capping zone b \t4\nFILLING ROOM A\t2\n");
    await preview();
    await user.click(accept());

    expect(reading("Capping Zone B").value).toBe("4");
    expect(reading("Filling Room A").value).toBe("2");
    expect(reading("Packing Hall C").value).toBe("");
  });

  it("an omitted location stays empty and is listed as left empty without blocking", async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "Packing Hall C\t9\nFilling Room A\t3\n");
    await preview();

    const leftEmpty = screen.getByRole("list", { name: /left empty/i });
    expect(within(leftEmpty).getByText("Capping Zone B")).toBeTruthy();
    expect(accept().disabled).toBe(false);

    await user.click(accept());
    expect(reading("Capping Zone B").value).toBe("");
    expect(reading("Packing Hall C").value).toBe("9");
  });

  it("an unknown name blocks Accept and is named in the problems", async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "Filling Room A\t3\nBoiler House\t5\n");
    const table = await preview();

    expect(accept().disabled).toBe(true);
    expect(within(table).getByText(/unknown location/i)).toBeTruthy();
    expect(screen.getByRole("alert").textContent).toMatch(/1 problem/i);
    expect(reading("Filling Room A").value).toBe("");
  });

  it("the same name twice in the paste blocks Accept", async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "Filling Room A\t3\nfilling room a\t4\n");
    const table = await preview();

    expect(accept().disabled).toBe(true);
    expect(within(table).getAllByText(/more than once/i).length).toBe(2);
  });

  it("duplicate names in the grid refuse the paste with the explanation", async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "Filling Room A\t3\n", [location(101, "Filling Room A"), location(102, " filling room a")]);
    await screen.findByRole("alert");

    expect(screen.getByRole("alert").textContent).toContain(
      "Location names are not unique in this session; enter counts by hand"
    );
    expect(accept().disabled).toBe(true);
  });

  it("a single column is refused with an explanation", async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "Filling Room A\nCapping Zone B\n");
    await screen.findByRole("alert");

    expect(screen.getByRole("alert").textContent).toMatch(/two columns/i);
    expect(accept().disabled).toBe(true);
  });

  it('flags "12.5" and "-1" as count format errors and blocks Accept', async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "Filling Room A\t12.5\nCapping Zone B\t-1\nPacking Hall C\t7\n");
    const table = await preview();

    expect(within(table).getAllByText(/whole number/i).length).toBe(2);
    expect(screen.getByRole("alert").textContent).toMatch(/2 problems/i);
    expect(accept().disabled).toBe(true);
  });

  it("refuses a paste of 501 rows", async () => {
    const user = userEvent.setup();
    const text = Array.from({ length: 501 }, () => "Filling Room A\t1").join("\n");
    await openAndPaste(user, text);
    await screen.findByRole("alert");

    expect(screen.getByRole("alert").textContent).toMatch(/501 rows/);
    expect(screen.getByRole("alert").textContent).toMatch(/500/);
    expect(accept().disabled).toBe(true);
  });

  it("Cancel and Escape leave the grid untouched", async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "Filling Room A\t3\nCapping Zone B\t6\n");
    await preview();
    await user.click(screen.getByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());
    expect(reading("Filling Room A").value).toBe("");
    expect(reading("Capping Zone B").value).toBe("");

    await user.click(reading("Filling Room A"));
    await user.paste("Filling Room A\t3\nCapping Zone B\t6\n");
    await preview();
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());
    expect(reading("Filling Room A").value).toBe("");
  });

  it("after Accept the values are unsaved, and Save sends them through the normal service call", async () => {
    const user = userEvent.setup();
    service.recordBatchResults.mockResolvedValue({ ok: true });
    await openAndPaste(user, "Packing Hall C\t9\nFilling Room A\t3\nCapping Zone B\t6\n");
    await preview();
    await user.click(accept());

    expect(service.recordBatchResults).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "Submit Results" }));
    await waitFor(() => expect(service.recordBatchResults).toHaveBeenCalledTimes(1));
    expect(service.recordBatchResults).toHaveBeenCalledWith(55, [
      { sampleLocationId: 101, readings: [3] },
      { sampleLocationId: 102, readings: [6] },
      { sampleLocationId: 103, readings: [9] }
    ]);
  });

  it("the preview is a real table with headers and row numbers that can be operated by keyboard", async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "Packing Hall C\t9\nFilling Room A\t3\n");
    const table = await preview();

    expect(within(table).getByRole("columnheader", { name: "Location" })).toBeTruthy();
    expect(within(table).getByRole("columnheader", { name: "Count" })).toBeTruthy();
    const rowHeaders = within(table).getAllByRole("rowheader").map((h) => h.textContent);
    expect(rowHeaders).toEqual(["1", "2"]);

    // Keyboard only: reach Accept with Tab and press Enter.
    const acceptButton = accept();
    for (let i = 0; i < 10 && document.activeElement !== acceptButton; i++) await user.tab();
    expect(document.activeElement).toBe(acceptButton);
    await user.keyboard("{Enter}");
    await waitFor(() => expect(reading("Packing Hall C").value).toBe("9"));
    expect(reading("Filling Room A").value).toBe("3");
  });

  it("a single value pasted into one field is a normal paste and opens no preview", async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "4");
    expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull();
    expect(reading("Filling Room A").value).toBe("4");
  });
});
