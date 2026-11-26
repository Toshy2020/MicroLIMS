import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { SignedTimeline, type SignedTimelineEntry } from "./SignedTimeline";

const entry = (overrides: Partial<SignedTimelineEntry> = {}): SignedTimelineEntry => ({
  signerFullName: "Sam Head",
  role: "Section Head",
  signedAtUtc: "2026-11-20T14:30:00Z",
  signedAtUtcText: "2026-11-20 14:30:00 UTC",
  meaning: "OosPhaseOneConcluded",
  meaningText: "Phase I concluded",
  comment: "Laboratory error confirmed",
  ...overrides
});

describe("SignedTimeline", () => {
  afterEach(() => {
    cleanup();
  });

  it("displays signer full name, UTC time with time zone, and signature meaning", () => {
    render(
      <PinnedLightTheme>
        <SignedTimeline entries={[entry()]} />
      </PinnedLightTheme>
    );

    expect(screen.getByText(/Sam Head/)).toBeTruthy();
    expect(screen.getByText(/2026-11-20 14:30:00 UTC/)).toBeTruthy();
    expect(screen.getByText("Phase I concluded")).toBeTruthy();
    expect(screen.getByText("Laboratory error confirmed")).toBeTruthy();
  });

  it("lists every signature as its own item, in the order given", () => {
    render(
      <PinnedLightTheme>
        <SignedTimeline
          entries={[
            entry({ meaningText: "Deviation assessed", signerFullName: "Ann Analyst" }),
            entry({ meaningText: "Deviation approved", signerFullName: "Quinn QA", signedAtUtcText: "2026-11-21 09:00:00 UTC" })
          ]}
        />
      </PinnedLightTheme>
    );

    const items = within(screen.getByRole("list")).getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(within(items[0]).getByText("Deviation assessed")).toBeTruthy();
    expect(within(items[1]).getByText(/Quinn QA/)).toBeTruthy();
    expect(within(items[1]).getByText(/2026-11-21 09:00:00 UTC/)).toBeTruthy();
  });

  it("shows the given title and empty text when nothing is signed", () => {
    render(
      <PinnedLightTheme>
        <SignedTimeline entries={[]} title="Quality signatures" emptyText="Nobody has signed this record." />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Quality signatures")).toBeTruthy();
    expect(screen.getByText("Nobody has signed this record.")).toBeTruthy();
    expect(screen.queryByRole("list")).toBeNull();
  });

  it("falls back to the common title and empty text", () => {
    render(
      <PinnedLightTheme>
        <SignedTimeline entries={[]} />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Signed Timeline")).toBeTruthy();
    expect(screen.getByText("No electronic signatures recorded yet.")).toBeTruthy();
  });
});
