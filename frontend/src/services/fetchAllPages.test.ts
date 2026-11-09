import { describe, expect, it, vi } from "vitest";
import { fetchAllPages, MAX_PAGE_SIZE } from "./fetchAllPages";

// A fake paged endpoint over `total` rows that honours the server cap.
function endpoint(total: number) {
  const rows = Array.from({ length: total }, (_, i) => i + 1);
  return vi.fn(async (page: number, pageSize: number) => {
    const size = Math.min(pageSize, MAX_PAGE_SIZE);
    return { items: rows.slice((page - 1) * size, page * size), totalCount: total };
  });
}

describe("fetchAllPages", () => {
  it("collects every row across pages, in order, at the server's page size", async () => {
    const fetchPage = endpoint(250);

    const all = await fetchAllPages(fetchPage);

    expect(all).toHaveLength(250);
    expect(all[0]).toBe(1);
    expect(all[249]).toBe(250);
    expect(fetchPage).toHaveBeenCalledTimes(3);
    expect(fetchPage.mock.calls.map((c) => c[1])).toEqual([MAX_PAGE_SIZE, MAX_PAGE_SIZE, MAX_PAGE_SIZE]);
  });

  it("makes one call when everything fits on the first page", async () => {
    const fetchPage = endpoint(40);
    expect(await fetchAllPages(fetchPage)).toHaveLength(40);
    expect(fetchPage).toHaveBeenCalledTimes(1);
  });

  it("returns nothing for an empty list", async () => {
    expect(await fetchAllPages(endpoint(0))).toEqual([]);
  });

  it("stops when a page comes back empty before the counted total", async () => {
    const fetchPage = vi.fn(async (page: number) => ({ items: page === 1 ? [1, 2] : [], totalCount: 500 }));
    expect(await fetchAllPages(fetchPage)).toEqual([1, 2]);
    expect(fetchPage).toHaveBeenCalledTimes(2);
  });

  it("refuses to loop forever", async () => {
    const fetchPage = vi.fn(async () => ({ items: [1], totalCount: Number.MAX_SAFE_INTEGER }));
    await expect(fetchAllPages(fetchPage, 3)).rejects.toThrow(/Stopped after 3 pages/);
  });
});
