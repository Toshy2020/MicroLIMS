import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { i18n } from "../../../../i18n";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { MaterialService } from "../../../inventory/materials/services/MaterialService";
import { EquipmentInventoryService } from "../../../inventory/equipment/services/EquipmentInventoryService";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { PrepareCryovialBatchDialog } from "./PrepareCryovialBatchDialog";

vi.mock("../../../inventory/materials/services/MaterialService", () => ({
  MaterialService: { getAll: vi.fn() }
}));
vi.mock("../../../inventory/equipment/services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: { getAll: vi.fn() }
}));
vi.mock("../../../../services/masterDataOptions", () => ({
  masterDataOptions: { getReleasedMedia: vi.fn(), getEquipment: vi.fn() }
}));

const renderDialog = () =>
  render(
    <PinnedLightTheme>
      <PrepareCryovialBatchDialog open onClose={vi.fn()} onSuccess={vi.fn()} />
    </PinnedLightTheme>
  );

describe("PrepareCryovialBatchDialog language", () => {
  beforeEach(() => {
    vi.mocked(MaterialService.getAll).mockResolvedValue([]);
    vi.mocked(EquipmentInventoryService.getAll).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getReleasedMedia).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getEquipment).mockResolvedValue([]);
  });
  afterEach(cleanup);

  it("shows the English title by default", async () => {
    renderDialog();
    expect(await screen.findByText("Prepare Cryovial Batch")).toBeTruthy();
  });

  it("shows the Arabic title and section heading when Arabic is active", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["labConfig"]);
    try {
      renderDialog();
      expect(await screen.findByText("تحضير دفعة قوارير مجمّدة")).toBeTruthy();
      expect(screen.getByText("القسم 2: لوحة تأكيد الهوية")).toBeTruthy();
    } finally {
      await i18n.changeLanguage("en");
    }
  });
});
