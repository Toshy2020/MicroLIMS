import { describe, expect, it } from "vitest";
import { getTable } from "./tableRegistry";
import { decodeState, encodeState, type TableState } from "./tableUrlCodec";

const def = getTable("inventory.equipment");
const defaultColumns = def.columns
  .filter((c) => c.defaultVisible)
  .map((c) => c.key);

const baseState: TableState = {
  filters: {},
  search: "",
  sort: null,
  pageSize: 25,
  columns: defaultColumns,
  density: "comfortable",
};

describe("tableUrlCodec", () => {
  it("encode omits defaults; decode(encode(s)) === s", () => {
    const defaultEncoded = encodeState(def, baseState, 1, null);
    expect(defaultEncoded.toString()).toBe("");
    expect(decodeState(def, defaultEncoded)).toEqual({
      state: {},
      page: 1,
      viewId: null,
      skipped: [],
    });

    const cases: Array<{
      label: string;
      state: TableState;
      page: number;
      viewId: number | null;
      expectedUrlState: Partial<TableState>;
    }> = [
      {
        label: "search only",
        state: { ...baseState, search: "HPLC-01" },
        page: 1,
        viewId: null,
        expectedUrlState: { search: "HPLC-01" },
      },
      {
        label: "single string filter + sort asc",
        state: {
          ...baseState,
          filters: { status: "Active" },
          sort: { key: "code", direction: "asc" },
        },
        page: 1,
        viewId: null,
        expectedUrlState: {
          filters: { status: "Active" },
          sort: { key: "code", direction: "asc" },
        },
      },
      {
        label: "multi-value filter + sort desc + page 3 + size 50 + view 12",
        state: {
          ...baseState,
          filters: { status: ["Active", "DueSoon"], location: "Lab A" },
          search: "balance",
          sort: { key: "calibrationDueDate", direction: "desc" },
          pageSize: 50,
          columns: ["code", "status"],
          density: "compact",
        },
        page: 3,
        viewId: 12,
        expectedUrlState: {
          filters: { status: ["Active", "DueSoon"], location: "Lab A" },
          search: "balance",
          sort: { key: "calibrationDueDate", direction: "desc" },
          pageSize: 50,
        },
      },
      {
        label: "pageSize all + page 2",
        state: { ...baseState, pageSize: "all" },
        page: 2,
        viewId: null,
        expectedUrlState: { pageSize: "all" },
      },
      {
        label: "pageSize 100",
        state: { ...baseState, pageSize: 100 },
        page: 1,
        viewId: 7,
        expectedUrlState: { pageSize: 100 },
      },
    ];

    for (const c of cases) {
      const encoded = encodeState(def, c.state, c.page, c.viewId);
      // Columns and density are personal and must never appear in the URL.
      expect(encoded.has("columns")).toBe(false);
      expect(encoded.has("density")).toBe(false);

      const decoded = decodeState(def, encoded);
      expect(decoded.skipped, c.label).toEqual([]);
      expect(decoded.page, c.label).toBe(c.page);
      expect(decoded.viewId, c.label).toBe(c.viewId);
      expect(decoded.state, c.label).toEqual(c.expectedUrlState);

      // With prefix
      const prefixed = encodeState(def, c.state, c.page, c.viewId, {
        prefix: "t2",
      });
      const decodedPrefixed = decodeState(def, prefixed, { prefix: "t2" });
      expect(decodedPrefixed.skipped, `${c.label} (prefixed)`).toEqual([]);
      expect(decodedPrefixed.page, `${c.label} (prefixed)`).toBe(c.page);
      expect(decodedPrefixed.viewId, `${c.label} (prefixed)`).toBe(c.viewId);
      expect(decodedPrefixed.state, `${c.label} (prefixed)`).toEqual(
        c.expectedUrlState
      );
    }
  });

  it("encode omits custom defaults when provided", () => {
    const customDefaults: Partial<TableState> = {
      filters: { status: "Active" },
      sort: { key: "code", direction: "asc" },
      pageSize: 50,
    };
    const stateMatchingDefaults: TableState = {
      ...baseState,
      filters: { status: "Active" },
      sort: { key: "code", direction: "asc" },
      pageSize: 50,
    };
    const encoded = encodeState(def, stateMatchingDefaults, 1, null, {
      defaults: customDefaults,
    });
    expect(encoded.toString()).toBe("");
  });

  it("unknown filter and unknown column are skipped and listed", () => {
    const params = new URLSearchParams(
      "q=pipette&f.status=Active&f.nonexistent=foo&sort=missingCol:asc&bogus=1&page=abc&size=999&view=xyz"
    );
    const decoded = decodeState(def, params);
    expect(decoded.state).toEqual({
      search: "pipette",
      filters: { status: "Active" },
    });
    expect(decoded.page).toBe(1);
    expect(decoded.viewId).toBeNull();
    expect(decoded.skipped).toEqual([
      "f.nonexistent",
      "sort:missingCol:asc",
      "bogus",
      "page:abc",
      "size:999",
      "view:xyz",
    ]);
  });
});
