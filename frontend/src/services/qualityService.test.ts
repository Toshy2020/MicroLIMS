import { afterEach, describe, expect, it, vi } from "vitest";

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn()
}));
vi.mock("./apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./apiClient")>();
  return { ...actual, apiClient: http };
});

import { qualityService } from "./qualityService";

afterEach(() => {
  vi.clearAllMocks();
});

const version = (v: number) => ({ headers: { "If-Match": `"${v}"` } });
const envelope = <T,>(data: T) => ({ data: { success: true, data } });

describe("qualityService reads", () => {
  it("lists records with the filter as query parameters and the abort signal", async () => {
    http.get.mockResolvedValue(envelope({ items: [], totalCount: 0, page: 2, pageSize: 25 }));
    const controller = new AbortController();

    const page = await qualityService.listRecords(
      { type: "Capa", status: "PlanApproved", overdue: true, page: 2, pageSize: 25 },
      controller.signal
    );

    expect(http.get).toHaveBeenCalledWith("/quality/records", {
      params: { type: "Capa", status: "PlanApproved", overdue: true, page: 2, pageSize: 25 },
      signal: controller.signal
    });
    expect(page.totalCount).toBe(0);
  });

  it("reads the record detail, links, comments, extensions and classifications with the abort signal", async () => {
    http.get.mockImplementation((url: string) =>
      url === "/quality/5/links"
        ? Promise.resolve(envelope({ items: [], totalCount: 3, page: 1, pageSize: 100 }))
        : Promise.resolve(envelope([]))
    );
    const { signal } = new AbortController();

    await qualityService.getRecordDetail(5, signal);
    const linksPage = await qualityService.listLinks(5, 0, 100, signal);
    await qualityService.listComments(5, 0, 100, signal);
    await qualityService.listExtensions(5, 1, 100, signal);
    await qualityService.listClassifications(1, 100, signal);

    expect(linksPage.totalCount).toBe(3);
    expect(http.get).toHaveBeenNthCalledWith(1, "/quality/records/5", { signal });
    expect(http.get).toHaveBeenNthCalledWith(2, "/quality/5/links", { params: { offset: 0, pageSize: 100 }, signal });
    expect(http.get).toHaveBeenNthCalledWith(3, "/quality/5/comments", { params: { offset: 0, pageSize: 100 }, signal });
    expect(http.get).toHaveBeenNthCalledWith(4, "/quality/5/extensions", { params: { page: 1, pageSize: 100 }, signal });
    expect(http.get).toHaveBeenNthCalledWith(5, "/quality/config/classifications", { params: { page: 1, pageSize: 100 }, signal });
  });

  it("downloads the PDF and an attachment as blobs", async () => {
    http.get.mockResolvedValue({ data: new Blob(["x"]) });

    await qualityService.downloadPdf(5);
    await qualityService.downloadAttachment(5, 9);

    expect(http.get).toHaveBeenNthCalledWith(1, "/quality/records/5/pdf", { responseType: "blob" });
    expect(http.get).toHaveBeenNthCalledWith(2, "/quality/5/attachments/9", { responseType: "blob" });
  });
});

describe("qualityService mutations send the record version as If-Match", () => {
  it("adds and removes a link", async () => {
    http.post.mockResolvedValue(envelope({ id: 3, number: "S-1", label: null, route: null, isRestricted: false }));
    http.delete.mockResolvedValue({});

    await qualityService.addLink(5, { targetType: "Sample", targetId: 77 }, 4);
    await qualityService.removeLink(5, 3, 5);

    expect(http.post).toHaveBeenCalledWith("/quality/5/links", { targetType: "Sample", targetId: 77 }, version(4));
    expect(http.delete).toHaveBeenCalledWith("/quality/5/links/3", version(5));
  });

  it("adds a comment", async () => {
    http.post.mockResolvedValue(envelope({}));

    await qualityService.addComment(5, "Checked the log", 6);

    expect(http.post).toHaveBeenCalledWith("/quality/5/comments", { text: "Checked the log" }, version(6));
  });

  it("uploads a file as multipart form data under the name 'file' and voids it with a reason", async () => {
    http.post.mockResolvedValue(envelope({}));
    const file = new File(["abc"], "evidence.pdf", { type: "application/pdf" });

    await qualityService.uploadAttachment(5, file, 7);
    await qualityService.voidAttachment(5, 9, "Wrong file", 8);

    const [url, body, config] = http.post.mock.calls[0] as [string, FormData, unknown];
    expect(url).toBe("/quality/5/attachments");
    expect(body.get("file")).toBe(file);
    expect(config).toEqual(version(7));
    expect(http.post).toHaveBeenNthCalledWith(2, "/quality/5/attachments/9/void", { reason: "Wrong file" }, version(8));
  });

  it("requests and decides an extension", async () => {
    http.post.mockResolvedValue(envelope({}));

    await qualityService.requestExtension(5, { actionId: 11, requestedDueDate: "2026-12-20", reason: "Supplier delay" }, 9);
    await qualityService.decideExtension(31, { approve: true, comment: null, password: "pw" }, 10);

    expect(http.post).toHaveBeenNthCalledWith(
      1,
      "/quality/5/extensions",
      { actionId: 11, requestedDueDate: "2026-12-20", reason: "Supplier delay" },
      version(9)
    );
    expect(http.post).toHaveBeenNthCalledWith(
      2,
      "/quality/extensions/31/decision",
      { approve: true, comment: null, password: "pw" },
      version(10)
    );
  });

  it("completes a CAPA action and a change-control task on their own routes", async () => {
    http.post.mockResolvedValue(envelope({}));

    await qualityService.completeCapaAction(11, { evidence: "Retrained", evidenceLinkId: 3 }, 12);
    await qualityService.completeChangeControlTask(12, { evidence: "Updated SOP", evidenceLinkId: null }, 13);

    expect(http.post).toHaveBeenNthCalledWith(
      1,
      "/quality/capas/actions/11/complete",
      { evidence: "Retrained", evidenceLinkId: 3 },
      version(12)
    );
    expect(http.post).toHaveBeenNthCalledWith(
      2,
      "/quality/change-controls/tasks/12/complete",
      { evidence: "Updated SOP", evidenceLinkId: null },
      version(13)
    );
  });
});
