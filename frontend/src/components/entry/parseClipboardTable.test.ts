import { describe, expect, it } from "vitest";
import { parseClipboardTable } from "./parseClipboardTable";

describe("parseClipboardTable", () => {
  it("splits tab separated cells and newline separated rows", () => {
    expect(parseClipboardTable("a\tb\nc\td")).toEqual([["a", "b"], ["c", "d"]]);
  });

  it("accepts Windows line endings", () => {
    expect(parseClipboardTable("a\tb\r\nc\td\r\n")).toEqual([["a", "b"], ["c", "d"]]);
  });

  it("keeps a newline inside a quoted cell and unescapes doubled quotes", () => {
    expect(parseClipboardTable('"line one\nline two"\tx\n"say ""hi"""\ty')).toEqual([
      ["line one\nline two", "x"],
      ['say "hi"', "y"]
    ]);
  });

  it("drops trailing blank lines but keeps blank cells inside a row", () => {
    expect(parseClipboardTable("a\t\tc\n")).toEqual([["a", "", "c"]]);
    expect(parseClipboardTable("a\tb\n\n")).toEqual([["a", "b"]]);
  });

  it("returns no table for a single cell, so a normal paste is left alone", () => {
    expect(parseClipboardTable("12")).toEqual([]);
    expect(parseClipboardTable("12\n")).toEqual([]);
    expect(parseClipboardTable("")).toEqual([]);
  });

  it("treats several rows of one column as a table", () => {
    expect(parseClipboardTable("a\nb")).toEqual([["a"], ["b"]]);
  });
});
