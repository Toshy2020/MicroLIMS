import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { AuthProvider } from "../../contexts/AuthContext";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { SampleLifecycleBadge } from "./SampleLifecycleBadge";

afterEach(cleanup);

describe("SampleLifecycleBadge", () => {
  it.each([
    ["Received", "Received"],
    ["Voided", "Voided"],
    ["Cancelled", "Cancelled"],
    ["InTesting", "Under Testing"],
    ["Approved", "Approved"]
  ])("shows a visible label for %s", (status, label) => {
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <SampleLifecycleBadge status={status} />
        </AuthProvider>
      </PinnedLightTheme>
    );
    expect(screen.getByText(label)).toBeTruthy();
  });
});
