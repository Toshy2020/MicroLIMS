import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { SignedTimeline } from "./SignedTimeline";

describe("SignedTimeline", () => {
  afterEach(() => {
    cleanup();
  });

  it("displays signer full name, UTC time with time zone, and signature meaning", () => {
    render(
      <PinnedLightTheme>
        <SignedTimeline
          entries={[
            {
              signerFullName: "Sam Head",
              username: "shead",
              role: "Section Head",
              signedAtUtc: "2026-11-20T14:30:00Z",
              signedAtUtcText: "2026-11-20 14:30:00 UTC",
              meaning: "OosPhaseOneConcluded",
              meaningText: "Phase I concluded",
              comment: "Laboratory error confirmed"
            }
          ]}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText(/Sam Head/)).toBeTruthy();
    expect(screen.getByText(/2026-11-20 14:30:00 UTC/)).toBeTruthy();
    expect(screen.getByText("Phase I concluded")).toBeTruthy();
  });
});
