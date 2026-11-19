import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { renderWithTheme } from "../../../components/bench/benchTestUtils";
import { i18n } from "../../../i18n";
import { AfterCleaningConfigPage } from "./AfterCleaningConfigPage";

const serviceMock = vi.hoisted(() => ({
  getMachines: vi.fn(),
  getPartConfigurations: vi.fn()
}));
const masterDataOptionsMock = vi.hoisted(() => ({
  getTestDefinitions: vi.fn(),
  getEditableTestCodes: vi.fn()
}));

vi.mock("./services/AfterCleaningConfigService", () => ({ AfterCleaningConfigService: serviceMock }));
vi.mock("../../../services/masterDataOptions", () => ({ masterDataOptions: masterDataOptionsMock }));

const renderPage = () =>
  renderWithTheme(
    <MemoryRouter>
      <AfterCleaningConfigPage />
    </MemoryRouter>,
    { bench: false }
  );

beforeEach(() => {
  serviceMock.getMachines.mockResolvedValue([
    { id: 1, name: "Filler 1", version: 1, parts: [{ id: 5, machineId: 1, name: "Needles", version: 1 }] }
  ]);
  serviceMock.getPartConfigurations.mockResolvedValue([]);
  masterDataOptionsMock.getTestDefinitions.mockResolvedValue([]);
  masterDataOptionsMock.getEditableTestCodes.mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("AfterCleaningConfigPage language", () => {
  it("shows the English heading by default", async () => {
    renderPage();
    expect(await screen.findByRole("heading", { name: "Filler 1" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Add machine" })).toBeTruthy();
  });

  it("shows the Arabic buttons and tiles when Arabic is active", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["labConfig"]);
    try {
      renderPage();
      expect(await screen.findByRole("heading", { name: "Filler 1" })).toBeTruthy();
      expect(screen.getByRole("button", { name: "إضافة آلة" })).toBeTruthy();
      expect(screen.getByText("الآلات")).toBeTruthy();
    } finally {
      await i18n.changeLanguage("en");
    }
  });
});
