import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor } from "@testing-library/react";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../../../i18n/testUtils";
import { EquipmentInventoryService } from "../services/EquipmentInventoryService";
import type { EquipmentItem } from "../types/equipmentTypes";
import { EquipmentDetailsDialog } from "./EquipmentDetailsDialog";

vi.mock("../services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: {
    getStatusHistory: vi.fn(),
    getDocuments: vi.fn()
  }
}));

const sampleEquipment: EquipmentItem = {
  id: 1,
  instrumentType: "Autoclave",
  manufacturerName: "Tuttnauer",
  serialNumber: "SN-100",
  firmwareVersion: "1.2",
  code: "EQ-01",
  location: "Lab A",
  calibrationDueDate: "2027-01-01",
  status: "InService",
  isCalibrationOverdue: false,
  sectionId: 1
};

describe("EquipmentDetailsDialog in Arabic", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.mocked(EquipmentInventoryService.getStatusHistory).mockResolvedValue([]);
    vi.mocked(EquipmentInventoryService.getDocuments).mockResolvedValue([]);
  });

  afterEach(async () => {
    cleanup();
    localStorage.clear();
    await resetLanguage();
  });

  it("shows tabs, metadata labels, and section headings in Arabic", async () => {
    await renderInArabic(
      <AuthProvider>
        <EquipmentDetailsDialog
          open
          equipment={sampleEquipment}
          onClose={() => {}}
          onEditEquipment={() => {}}
          onEquipmentUpdated={() => {}}
        />
      </AuthProvider>,
      ["common", "inventory"]
    );
    expect(screen.getByRole("tab", { name: "التفاصيل" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "الجدولة" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "سجل الصيانة" })).toBeTruthy();
    expect(screen.getByText("معلومات الجهاز")).toBeTruthy();
    expect(screen.getByText("شهادات المعايرة")).toBeTruthy();
    await waitFor(() =>
      expect(screen.getByText("لم يتم تسجيل أي تغييرات في الحالة التشغيلية لهذه المعدات.")).toBeTruthy()
    );
  });
});
