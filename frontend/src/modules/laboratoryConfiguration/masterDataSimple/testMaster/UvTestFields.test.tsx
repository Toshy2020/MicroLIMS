import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { UvTestFields } from "./UvTestFields";

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
});
