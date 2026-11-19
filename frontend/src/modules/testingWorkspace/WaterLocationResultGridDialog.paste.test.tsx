import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderWithTheme } from "../../components/bench/benchTestUtils";
import { resetDraftDb, signInForTest } from "../../services/drafts/draftTestUtils";
import { WaterLocationResultGridDialog } from "./WaterLocationResultGridDialog";

const service = vi.hoisted(() => ({
  getLocations: vi.fn(),
  recordWaterBatchReadings: vi.fn()
}));
vi.mock("./services/TestWorkflowService", () => ({ TestWorkflowService: service }));
vi.mock("../../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));

const point = (id: number, name: string) => ({
  id,
  locationType: "SamplingPoint",
  locationName: name,
  alertLimit: "100",
  actionLimit: "200",
  specLimit: "500",
  calculatedResult: null,
  rawReadings: null,
  status: null
});

const POINTS = [point(201, "Tap 1"), point(202, "Tank Outlet"), point(203, "Return Loop")];

function renderDialog() {
  return renderWithTheme(
    <AuthProvider>
      <MemoryRouter>
        <WaterLocationResultGridDialog
          open
          testOrderId={66}
          testCode="WATER-TAMC"
          displayName="Water batch"
          minReadyAt={null}
          onClose={vi.fn()}
          onSubmitted={vi.fn()}
        />
      </MemoryRouter>
    </AuthProvider>,
    { bench: false }
  );
}

async function openAndPaste(user: ReturnType<typeof userEvent.setup>, text: string) {
  service.getLocations.mockResolvedValue(POINTS);
  renderDialog();
  await user.click(await screen.findByLabelText("Tap 1 reading 1"));
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

describe("WaterLocationResultGridDialog paste", () => {
  it("a shuffled block lands every count on the sampling point with the same name", async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "Return Loop\t30\nTap 1\t10\n");
    await preview();

    const leftEmpty = screen.getByRole("list", { name: /left empty/i });
    expect(within(leftEmpty).getByText("Tank Outlet")).toBeTruthy();

    await user.click(accept());
    expect(reading("Tap 1").value).toBe("10");
    expect(reading("Tank Outlet").value).toBe("");
    expect(reading("Return Loop").value).toBe("30");
  });

  it("an unknown name blocks Accept and nothing is written", async () => {
    const user = userEvent.setup();
    await openAndPaste(user, "Tap 1\t10\nTap 9\t5\n");
    const table = await preview();

    expect(accept().disabled).toBe(true);
    expect(within(table).getByText(/unknown location/i)).toBeTruthy();
    expect(reading("Tap 1").value).toBe("");
  });

  it("after Accept nothing is saved until the normal Submit, which uses the water service call", async () => {
    const user = userEvent.setup();
    service.recordWaterBatchReadings.mockResolvedValue({ ok: true });
    await openAndPaste(user, "Return Loop\t30\nTap 1\t10\nTank Outlet\t20\n");
    await preview();
    await user.click(accept());
    expect(service.recordWaterBatchReadings).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Submit Results" }));
    await waitFor(() => expect(service.recordWaterBatchReadings).toHaveBeenCalledTimes(1));
    expect(service.recordWaterBatchReadings).toHaveBeenCalledWith(66, [
      { sampleLocationId: 201, readings: [10] },
      { sampleLocationId: 202, readings: [20] },
      { sampleLocationId: 203, readings: [30] }
    ]);
  });
});
