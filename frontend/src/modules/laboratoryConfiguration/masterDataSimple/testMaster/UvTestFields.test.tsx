import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { UvTestFields } from "./UvTestFields";
import { MaterialMasterService } from "../services/MaterialMasterService";

vi.mock("../services/MaterialMasterService", () => ({
  MaterialMasterService: {
    getAll: vi.fn().mockResolvedValue([
      { id: 7, code: "RS-7", name: "Riboflavin RS", sectionId: 1 },
      { id: 8, code: "RS-8", name: "Other lab RS", sectionId: 2 }
    ])
  }
}));

describe("UvTestFields", () => {
  afterEach(cleanup);

  it("SpecificAbsorbanceAssay shows specific absorbance and total volume, and no standard volume", () => {
    render(
      <PinnedLightTheme>
        <UvTestFields
          mode="SpecificAbsorbanceAssay"
          onModeChange={vi.fn()}
          specificAbsorbance="328"
          onSpecificAbsorbanceChange={vi.fn()}
          totalVolumeMl="5000"
          onTotalVolumeMlChange={vi.fn()}
          standardVolumeMl=""
          onStandardVolumeMlChange={vi.fn()}
          standardEntryId=""
          onStandardEntryIdChange={vi.fn()}
          sectionId={1}
          replicateCount="1"
          onReplicateCountChange={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByLabelText(/Calculation mode/i)).toBeTruthy();
    expect(screen.getByLabelText(/Specific absorbance/i)).toBeTruthy();
    expect(screen.getByLabelText(/Total volume/i)).toBeTruthy();
    expect(screen.getByLabelText(/Replicate count/i)).toBeTruthy();

    expect(screen.queryByLabelText(/Standard volume/i)).toBeNull();
  });

  it("SpecificAbsorbanceValue shows total volume, and no specific absorbance or standard volume", () => {
    render(
      <PinnedLightTheme>
        <UvTestFields
          mode="SpecificAbsorbanceValue"
          onModeChange={vi.fn()}
          specificAbsorbance=""
          onSpecificAbsorbanceChange={vi.fn()}
          totalVolumeMl="250000"
          onTotalVolumeMlChange={vi.fn()}
          standardVolumeMl=""
          onStandardVolumeMlChange={vi.fn()}
          standardEntryId=""
          onStandardEntryIdChange={vi.fn()}
          sectionId={1}
          replicateCount="1"
          onReplicateCountChange={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByLabelText(/Total volume/i)).toBeTruthy();
    expect(screen.getByLabelText(/Replicate count/i)).toBeTruthy();

    expect(screen.queryByLabelText(/Specific absorbance/i)).toBeNull();
    expect(screen.queryByLabelText(/Standard volume/i)).toBeNull();
  });

  it("AbsorbanceLimit shows replicate count only, and no volume or specific absorbance", () => {
    render(
      <PinnedLightTheme>
        <UvTestFields
          mode="AbsorbanceLimit"
          onModeChange={vi.fn()}
          specificAbsorbance=""
          onSpecificAbsorbanceChange={vi.fn()}
          totalVolumeMl=""
          onTotalVolumeMlChange={vi.fn()}
          standardVolumeMl=""
          onStandardVolumeMlChange={vi.fn()}
          standardEntryId=""
          onStandardEntryIdChange={vi.fn()}
          sectionId={1}
          replicateCount="1"
          onReplicateCountChange={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByLabelText(/Calculation mode/i)).toBeTruthy();
    expect(screen.getByLabelText(/Replicate count/i)).toBeTruthy();

    expect(screen.queryByLabelText(/Specific absorbance/i)).toBeNull();
    expect(screen.queryByLabelText(/Total volume/i)).toBeNull();
    expect(screen.queryByLabelText(/Standard volume/i)).toBeNull();
  });

  it("StandardComparison shows total volume and standard volume, and no specific absorbance", () => {
    render(
      <PinnedLightTheme>
        <UvTestFields
          mode="StandardComparison"
          onModeChange={vi.fn()}
          specificAbsorbance=""
          onSpecificAbsorbanceChange={vi.fn()}
          totalVolumeMl="100"
          onTotalVolumeMlChange={vi.fn()}
          standardVolumeMl="100"
          onStandardVolumeMlChange={vi.fn()}
          standardEntryId=""
          onStandardEntryIdChange={vi.fn()}
          sectionId={1}
          replicateCount="1"
          onReplicateCountChange={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByLabelText(/Calculation mode/i)).toBeTruthy();
    expect(screen.getByLabelText(/Total volume/i)).toBeTruthy();
    expect(screen.getByLabelText(/Standard volume/i)).toBeTruthy();
    expect(screen.getByLabelText(/Replicate count/i)).toBeTruthy();

    expect(screen.queryByLabelText(/Specific absorbance/i)).toBeNull();
  });

  it("shows the standard material select only for StandardComparison, listing its section's entries, and sends the choice", async () => {
    const onEntry = vi.fn();
    const props = {
      onModeChange: vi.fn(), specificAbsorbance: "", onSpecificAbsorbanceChange: vi.fn(), totalVolumeMl: "100",
      onTotalVolumeMlChange: vi.fn(), standardVolumeMl: "100", onStandardVolumeMlChange: vi.fn(), replicateCount: "1",
      onReplicateCountChange: vi.fn(), standardEntryId: "" as const, onStandardEntryIdChange: onEntry, sectionId: 1
    };
    const { rerender } = render(
      <PinnedLightTheme>
        <UvTestFields {...props} mode="SpecificAbsorbanceAssay" />
      </PinnedLightTheme>
    );
    expect(screen.queryByLabelText(/Standard material/i)).toBeNull();

    rerender(
      <PinnedLightTheme>
        <UvTestFields {...props} mode="StandardComparison" />
      </PinnedLightTheme>
    );
    await waitFor(() => expect(MaterialMasterService.getAll).toHaveBeenCalledWith("ReferenceStandard", true));
    fireEvent.mouseDown(screen.getByLabelText(/Standard material/i));
    const list = within(await screen.findByRole("listbox"));
    expect(list.queryByText(/Other lab RS/)).toBeNull();
    fireEvent.click(list.getByText(/Riboflavin RS/));
    expect(onEntry).toHaveBeenCalledWith(7);
  });
});
