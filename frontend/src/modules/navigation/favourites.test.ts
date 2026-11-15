import { describe, expect, it } from "vitest";
import {
  MAX_FAVOURITES,
  MAX_FAVOURITES_BYTES,
  byteLength,
  parseFavourites,
  serialiseFavourites,
  viewFavouriteTarget,
  type Favourite
} from "./favourites";

describe("favourites storage format", () => {
  it("15 view favourites with 7-digit ids serialise under 2,000 bytes", () => {
    const items: Favourite[] = Array.from({ length: MAX_FAVOURITES }, (_, i) => ({
      t: "v",
      // The longest table key in the registry.
      table: "documentControl.library",
      view: 1_000_000 + i
    }));
    const json = serialiseFavourites(items);
    expect(byteLength(json)).toBeLessThan(MAX_FAVOURITES_BYTES);
    expect(parseFavourites(json)).toEqual(items);
  });

  it("keeps what is valid from a damaged value and never throws", () => {
    expect(parseFavourites("not json")).toEqual([]);
    expect(parseFavourites('{"t":"p"}')).toEqual([]);
    expect(parseFavourites('[{"t":"p","path":"/a"},{"t":"x"},{"t":"p","path":"/a"},{"t":"v","table":"t","view":"3"}]')).toEqual([
      { t: "p", path: "/a" }
    ]);
  });

  it("opens a view on its table's page", () => {
    expect(viewFavouriteTarget("inventory.equipment", 7)).toBe("/inventory/equipment?view=7");
    expect(viewFavouriteTarget("unknown.table", 7)).toBeNull();
  });
});
