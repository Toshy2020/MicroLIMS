import { afterEach, describe, expect, it, vi } from "vitest";

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn()
}));

vi.mock("./apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./apiClient")>();
  return { ...actual, apiClient: http };
});

import { labelService } from "./labelService";

afterEach(() => {
  vi.clearAllMocks();
});

describe("labelService", () => {
  it("getStatus posts entity types and ids and forwards AbortSignal", async () => {
    const controller = new AbortController();
    const statuses = [
      {
        entityType: "Sample",
        entityId: 12,
        canPrint: true,
        blockedReason: null,
        printCount: 0,
        defaultCopies: 2
      }
    ];
    http.post.mockResolvedValue({ data: { success: true, message: null, data: statuses, errors: null } });

    const result = await labelService.getStatus(
      [{ entityType: "Sample", entityId: 12, code: "S-012", displayName: "Water" }],
      controller.signal
    );

    expect(http.post).toHaveBeenCalledWith(
      "/labels/status",
      { items: [{ entityType: "Sample", entityId: 12 }] },
      { signal: controller.signal }
    );
    expect(result).toEqual(statuses);
  });

  it("print posts items and reason with responseType blob", async () => {
    const pdfBlob = new Blob(["%PDF-1.4"], { type: "application/pdf" });
    http.post.mockResolvedValue({ data: pdfBlob });

    const result = await labelService.print(
      [{ entityType: "EquipmentAsset", entityId: 7, copies: 3 }],
      "Damaged label on incubator"
    );

    expect(http.post).toHaveBeenCalledWith(
      "/labels/print",
      {
        items: [{ entityType: "EquipmentAsset", entityId: 7, copies: 3 }],
        reason: "Damaged label on incubator"
      },
      { responseType: "blob" }
    );
    expect(result).toBe(pdfBlob);
  });

  it("getFormats fetches label formats and forwards AbortSignal", async () => {
    const controller = new AbortController();
    const formats = [
      {
        id: 1,
        version: 4,
        entityType: "Sample",
        widthMm: 50,
        heightMm: 25,
        layout: "Standard",
        defaultCopies: 1
      }
    ];
    http.get.mockResolvedValue({ data: { success: true, message: null, data: formats, errors: null } });

    const result = await labelService.getFormats(controller.signal);

    expect(http.get).toHaveBeenCalledWith("/labels/formats", { signal: controller.signal });
    expect(result).toEqual(formats);
  });

  it("updateFormat puts body with If-Match header for record version", async () => {
    http.put.mockResolvedValue({ data: { success: true, message: null, data: { id: 3 }, errors: null } });

    await labelService.updateFormat(3, 9, {
      widthMm: 60,
      heightMm: 30,
      layout: "Small",
      defaultCopies: 2
    });

    expect(http.put).toHaveBeenCalledWith(
      "/labels/formats/3",
      {
        widthMm: 60,
        heightMm: 30,
        layout: "Small",
        defaultCopies: 2
      },
      { headers: { "If-Match": '"9"' } }
    );
  });

  it("resolveScan queries /scan/resolve with value and AbortSignal", async () => {
    const controller = new AbortController();
    const resolved = {
      outcome: "Found",
      layoutCorrected: false,
      normalizedValue: "S-012",
      match: {
        entityType: "Sample",
        id: 12,
        code: "S-012",
        displayName: "Purified Water",
        status: "Received",
        usable: true,
        blockedReason: null,
        navigationTarget: "/receiving-testing?sampleId=12",
        linkedEquipmentId: null
      },
      candidates: [],
      hint: null
    };
    http.get.mockResolvedValue({ data: { success: true, message: null, data: resolved, errors: null } });

    const result = await labelService.resolveScan("S-012", controller.signal);

    expect(http.get).toHaveBeenCalledWith("/scan/resolve", {
      params: { value: "S-012" },
      signal: controller.signal
    });
    expect(result).toEqual(resolved);
  });
});
