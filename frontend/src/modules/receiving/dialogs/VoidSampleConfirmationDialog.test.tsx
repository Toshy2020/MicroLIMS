import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { VoidSampleConfirmationDialog } from "./VoidSampleConfirmationDialog";
import type { SampleRecord } from "../types/receivingTypes";

afterEach(cleanup);

const sample = {
  sampleId: 7,
  referenceNumber: "FP1126005",
  displayName: "Paracetamol 500 mg",
  category: "FinishedProduct",
  batchNumber: "B1",
  controlNumber: null
} as unknown as SampleRecord;

describe("VoidSampleConfirmationDialog", () => {
  it("opens with focus on Cancel, not on the reason field", async () => {
    render(
      <PinnedLightTheme>
        <AuthProvider>
        <VoidSampleConfirmationDialog open sample={sample} onClose={vi.fn()} onSuccess={vi.fn()} />
        </AuthProvider>
      </PinnedLightTheme>
    );
    const cancel = await screen.findByRole("button", { name: "Cancel" });
    await waitFor(() => expect(document.activeElement).toBe(cancel));
  });
});
