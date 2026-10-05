import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { ReplicateRowInputCard, isSelectableLot } from "./ReplicateRowInputCard";
import type { LotOption } from "../types";

afterEach(cleanup);

const lot = (materialId: number, purity: number | null): LotOption => ({
  materialId, batchNumber: `B${materialId}`, expiryDate: null, quantityRemaining: 5, unit: "g", usable: true, reason: null, purity
} as unknown as LotOption);

describe("ReplicateRowInputCard standard lot picker", () => {
  it("lists a lot without purity as disabled with the explanation", () => {
    render(
      <ReplicateRowInputCard
        row={{ standardMaterialId: null, standardWeightMg: "", referencePreparationId: null, referenceVolumeMl: "", titrantVolumeMl: "", blankMl: "" }}
        index={0} isPrimary blankRequired={false}
        standardLots={[lot(1, 99.5), lot(2, null)]} referenceOptions={[]} onChange={() => {}}
      />
    );
    fireEvent.mouseDown(screen.getByRole("combobox"));
    const options = within(screen.getByRole("listbox")).getAllByRole("option");
    expect(options[0].getAttribute("aria-disabled")).not.toBe("true");
    expect(options[1].getAttribute("aria-disabled")).toBe("true");
    expect(options[1].textContent).toContain("No purity - enter it in Materials Stock");
  });

  it("defaults to the first lot that has purity, skipping lots without it", () => {
    expect([lot(2, null), lot(1, 99.5)].find(isSelectableLot)?.materialId).toBe(1);
  });
});
