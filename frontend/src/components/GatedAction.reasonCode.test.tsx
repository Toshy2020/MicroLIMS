import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import { Button } from "@mui/material";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { actionAvailabilityService } from "../services/actionAvailabilityService";
import { useActionAvailability } from "../hooks/useActionAvailability";
import { i18n } from "../i18n";
import { GatedAction } from "./GatedAction";

// Network boundary only.
vi.mock("../services/actionAvailabilityService", () => ({
  actionAvailabilityService: { get: vi.fn() }
}));

const getAvailability = vi.mocked(actionAvailabilityService.get);

function Harness() {
  const availability = useActionAvailability("test-order", 5);
  return (
    <GatedAction action="test.release" availability={availability}>
      <Button>Release test</Button>
    </GatedAction>
  );
}

function mount() {
  return render(
    <PinnedLightTheme>
      <Harness />
    </PinnedLightTheme>
  );
}

const ENGLISH = "Already taken by Dana.";
const SERVER_ENGLISH = "The test is already taken by Dana.";

beforeEach(async () => {
  getAvailability.mockReset();
  await i18n.changeLanguage("en");
});
afterEach(async () => {
  cleanup();
  await i18n.changeLanguage("en");
});

describe("gated action reasons in the interface language", () => {
  it("Arabic and a coded refusal: the Arabic template with its parameters", async () => {
    await i18n.changeLanguage("ar");
    getAvailability.mockResolvedValue([
      {
        action: "test.release",
        allowed: false,
        reason: SERVER_ENGLISH,
        reasonCode: "Test.AlreadyTaken",
        reasonParams: { name: "Dana" }
      }
    ]);
    mount();
    expect(await screen.findByText("تم أخذ الاختبار بالفعل بواسطة Dana.")).toBeTruthy();
    expect(screen.queryByText(SERVER_ENGLISH)).toBeNull();
  });

  it("English and a coded refusal: the English template", async () => {
    getAvailability.mockResolvedValue([
      {
        action: "test.release",
        allowed: false,
        reason: "server wording",
        reasonCode: "Test.AlreadyTaken",
        reasonParams: { name: "Dana" }
      }
    ]);
    mount();
    expect(await screen.findByText(ENGLISH)).toBeTruthy();
  });

  it("a code without a template: the server's English reason", async () => {
    await i18n.changeLanguage("ar");
    getAvailability.mockResolvedValue([
      { action: "test.release", allowed: false, reason: "Something new happened.", reasonCode: "Nope.NoTemplate" }
    ]);
    mount();
    expect(await screen.findByText("Something new happened.")).toBeTruthy();
  });

  it("an uncoded refusal in Arabic: the English reason unchanged", async () => {
    await i18n.changeLanguage("ar");
    getAvailability.mockResolvedValue([{ action: "test.release", allowed: false, reason: SERVER_ENGLISH }]);
    mount();
    expect(await screen.findByText(SERVER_ENGLISH)).toBeTruthy();
  });

  it("switching the language re-renders the reason", async () => {
    getAvailability.mockResolvedValue([
      {
        action: "test.release",
        allowed: false,
        reason: "server wording",
        reasonCode: "Test.AlreadyTaken",
        reasonParams: { name: "Dana" }
      }
    ]);
    mount();
    expect(await screen.findByText(ENGLISH)).toBeTruthy();
    await act(async () => {
      await i18n.changeLanguage("ar");
    });
    expect(await screen.findByText("تم أخذ الاختبار بالفعل بواسطة Dana.")).toBeTruthy();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
    expect(await screen.findByText(ENGLISH)).toBeTruthy();
  });
});
