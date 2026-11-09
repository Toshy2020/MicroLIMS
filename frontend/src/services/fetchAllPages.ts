// Every list endpoint caps a page at 100 rows (backend Paging.MaxPageSize,
// CLAUDE.md Rule 5.4). A screen that needs every matching row - a register
// or an "export all" - pages through instead of asking for one huge page,
// so nothing is silently cut off at the cap.
export const MAX_PAGE_SIZE = 100;

export interface PageOf<T> {
  items: T[];
  totalCount: number;
}

// Stops when every row the server counted has arrived, or when a page comes
// back empty (the data shrank while paging); maxPages guards a runaway loop.
export async function fetchAllPages<T>(
  fetchPage: (page: number, pageSize: number) => Promise<PageOf<T>>,
  maxPages = 1000
): Promise<T[]> {
  const all: T[] = [];
  for (let page = 1; page <= maxPages; page++) {
    const result = await fetchPage(page, MAX_PAGE_SIZE);
    all.push(...result.items);
    if (result.items.length === 0 || all.length >= result.totalCount) return all;
  }
  throw new Error(`Stopped after ${maxPages} pages; the list is larger than expected.`);
}
