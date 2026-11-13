import { afterEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "./apiClient";
import { auditTrailReviewService } from "./auditTrailReviewService";

vi.mock("./apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./apiClient")>();
  return {
    ...actual,
    apiClient: {
      get: vi.fn(),
      post: vi.fn()
    }
  };
});

afterEach(() => {
  vi.clearAllMocks();
});

describe("auditTrailReviewService", () => {
  it("lists reviews with page and pageSize query params and forwards AbortSignal", async () => {
    const controller = new AbortController();
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: [] } });

    const result = await auditTrailReviewService.list(2, 25, controller.signal);

    expect(result).toEqual([]);
    expect(apiClient.get).toHaveBeenCalledWith("/audit-trail-reviews", {
      params: { page: 2, pageSize: 25 },
      signal: controller.signal
    });
  });

  it("loads a single review and paged entries", async () => {
    const controller = new AbortController();
    vi.mocked(apiClient.get)
      .mockResolvedValueOnce({ data: { data: { id: 5, title: "Q4" } } })
      .mockResolvedValueOnce({ data: { data: { items: [], page: 1, pageSize: 25, totalCount: 0, totalPages: 0 } } });

    await auditTrailReviewService.get(5, controller.signal);
    await auditTrailReviewService.getEntries(5, 1, 25, controller.signal);

    expect(apiClient.get).toHaveBeenNthCalledWith(1, "/audit-trail-reviews/5", { signal: controller.signal });
    expect(apiClient.get).toHaveBeenNthCalledWith(2, "/audit-trail-reviews/5/entries", {
      params: { page: 1, pageSize: 25 },
      signal: controller.signal
    });
  });

  it("sends the version in If-Match when signing", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: { data: { id: 5, status: "Signed" } } });

    await auditTrailReviewService.sign(5, { password: "pw", conclusion: "No issues" }, 7);

    expect(apiClient.post).toHaveBeenCalledWith(
      "/audit-trail-reviews/5/sign",
      { password: "pw", conclusion: "No issues" },
      { headers: { "If-Match": '"7"' } }
    );
  });

  it("downloads the PDF as a blob", async () => {
    const blob = new Blob(["%PDF-1.4"], { type: "application/pdf" });
    vi.mocked(apiClient.get).mockResolvedValue({ data: blob });
    const createObjectURL = vi.spyOn(window.URL, "createObjectURL").mockReturnValue("blob:pdf");
    const revokeObjectURL = vi.spyOn(window.URL, "revokeObjectURL").mockImplementation(() => {});
    const click = vi.fn();
    const createElement = vi.spyOn(document, "createElement").mockReturnValue({
      href: "",
      download: "",
      click
    } as unknown as HTMLAnchorElement);

    await auditTrailReviewService.exportPdf(9);

    expect(apiClient.get).toHaveBeenCalledWith("/audit-trail-reviews/9/pdf", { responseType: "blob" });
    expect(click).toHaveBeenCalledTimes(1);
    expect(revokeObjectURL).toHaveBeenCalledWith("blob:pdf");

    createObjectURL.mockRestore();
    revokeObjectURL.mockRestore();
    createElement.mockRestore();
  });
});
