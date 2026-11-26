import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { TypeDetails } from "./TypeDetails";
import { recordDetail } from "../testFixtures";
import type { QualityRecordDetailDto } from "../../../services/qualityService";

function renderDetails(record: QualityRecordDetailDto) {
  render(
    <PinnedLightTheme>
      <MemoryRouter>
        <TypeDetails record={record} />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

const term = (name: string) => screen.getByText(name, { selector: "dt" });
const definition = (name: string) => term(name).nextElementSibling as HTMLElement;

describe("TypeDetails", () => {
  afterEach(() => {
    cleanup();
  });

  it("shows a deviation's facts, with the time it occurred in its time zone and unfilled fields marked", () => {
    renderDetails(recordDetail());

    expect(definition("Location").textContent).toBe("Incubator room 2");
    expect(definition("Occurred").textContent).toMatch(/19 Nov 2026.*\d{2}:\d{2}\s+\S+/);
    expect(definition("Immediate action").textContent).toBe("Moved the plates to incubator 4.");
    expect(definition("Root cause category").textContent).toBe("Not recorded yet");
    expect(definition("CAPA required").textContent).toBe("Not recorded yet");
  });

  it("shows whether a CAPA is required once decided", () => {
    const base = recordDetail();
    renderDetails({ ...base, deviation: { ...base.deviation!, capaRequired: true, rootCauseCategoryName: "Equipment failure" } });

    expect(definition("CAPA required").textContent).toBe("Yes");
    expect(definition("Root cause category").textContent).toBe("Equipment failure");
  });

  it("shows a CAPA's source record as a link and its effectiveness plan", () => {
    renderDetails(
      recordDetail({
        type: "Capa",
        number: "CAPA-2026-0003",
        deviation: null,
        capa: {
          status: "PlanApproved",
          sourceRecordId: 1,
          sourceRecordNumber: "DEV-2026-0001",
          effectivenessCriteria: "No drift for 90 days",
          effectivenessMethod: "Review the temperature log",
          effectivenessPlannedDate: "2027-03-01",
          effectivenessResult: null,
          effectivenessFindings: null,
          successorCapaId: null,
          successorCapaNumber: null
        }
      })
    );

    const source = within(definition("Source record")).getByRole("link", { name: "DEV-2026-0001" });
    expect(source.getAttribute("href")).toBe("/quality/1");
    expect(definition("Effectiveness check planned for").textContent).toBe("2027-03-01");
    expect(definition("Effectiveness result").textContent).toBe("Not recorded yet");
  });

  it("shows a change control's reason and a complaint's source, item and outcome", () => {
    renderDetails(
      recordDetail({
        type: "ChangeControl",
        deviation: null,
        changeControl: {
          status: "Proposed",
          reason: "Replace the incubator",
          impactSummary: null,
          rejectionReason: null,
          postChangeReview: null,
          unchangedJustification: null
        }
      })
    );
    expect(definition("Reason for the change").textContent).toBe("Replace the incubator");
    cleanup();

    renderDetails(
      recordDetail({
        type: "Complaint",
        deviation: null,
        complaint: {
          status: "Concluded",
          source: "Hospital pharmacy",
          receivedOn: "2026-11-02",
          externalReference: null,
          itemId: 4,
          itemName: "Amoxicillin 500mg",
          lotText: "B-77",
          complaintText: "Capsules were discoloured.",
          whatToTest: null,
          findings: null,
          outcome: "NotJustified",
          conclusionSummary: null
        }
      })
    );
    expect(definition("Source").textContent).toBe("Hospital pharmacy");
    expect(definition("Item").textContent).toBe("Amoxicillin 500mg");
    expect(definition("Outcome").textContent).toBe("Not justified");
    expect(definition("Received on").textContent).toBe("2026-11-02");
  });

  it("renders nothing when the record carries no type detail", () => {
    renderDetails(recordDetail({ deviation: null }));

    expect(screen.queryByText("Details")).toBeNull();
  });
});
