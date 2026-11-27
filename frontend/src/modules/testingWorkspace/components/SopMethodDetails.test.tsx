import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { SopMethodDetails, SopMethodLine } from "./SopMethodDetails";

afterEach(cleanup);

describe("SopMethodDetails", () => {
  it("shows 'Not recorded (before {date})' in the laboratory time zone for pre-capture orders", () => {
    render(
      <PinnedLightTheme>
        <SopMethodDetails
          sopCapture={null}
          sopCaptureStartDateUtc="2026-11-26T23:30:00Z"
          timeZone="Africa/Cairo"
        />
      </PinnedLightTheme>
    );
    expect(screen.getByText("Not recorded (before 27 Nov 2026)")).toBeTruthy();
  });

  it("shows the UTC date when timeZone is UTC", () => {
    render(
      <PinnedLightTheme>
        <SopMethodDetails
          sopCapture={null}
          sopCaptureStartDateUtc="2026-11-26T23:30:00Z"
          timeZone="UTC"
        />
      </PinnedLightTheme>
    );
    expect(screen.getByText("Not recorded (before 26 Nov 2026)")).toBeTruthy();
  });

  it("keeps existing wording for unresolved capture statuses", () => {
    render(
      <PinnedLightTheme>
        <SopMethodDetails
          sopCapture={{
            status: "NoEffectiveRevision",
            documentMasterId: null,
            documentCode: null,
            title: null,
            revisionId: null,
            revisionNumber: null,
            effectiveDateUtc: null,
            capturedAtUtc: "2026-11-27T08:00:00Z"
          }}
          sopCaptureStartDateUtc="2026-11-26T23:30:00Z"
          timeZone="UTC"
        />
      </PinnedLightTheme>
    );
    expect(screen.getByText("SOP not recorded: No effective revision")).toBeTruthy();
  });

  it("renders pre-capture method line on Certificate of Analysis in the laboratory time zone", () => {
    render(
      <PinnedLightTheme>
        <SopMethodLine
          document={{
            documentCode: "",
            title: "",
            revisionNumber: "",
            effectiveDateUtc: null,
            preCaptureStartDateUtc: "2026-11-26T23:30:00Z"
          }}
          timeZone="Africa/Cairo"
        />
      </PinnedLightTheme>
    );
    expect(screen.getByText("Not recorded (before 27 Nov 2026)")).toBeTruthy();
  });
});
