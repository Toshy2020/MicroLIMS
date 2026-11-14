import { AxiosError, AxiosHeaders } from "axios";
import { describe, expect, it } from "vitest";
import { getErrorMessage } from "./errorMessage";
import { errorsByField, errorsByRow, extractBatchSaveErrors, getFieldErrors } from "./fieldErrors";

function makeAxiosError(data: unknown, message = "Request failed"): AxiosError {
  return new AxiosError(
    message,
    "ERR_BAD_REQUEST",
    undefined,
    undefined,
    {
      data,
      status: 400,
      statusText: "Bad Request",
      headers: {},
      config: { headers: new AxiosHeaders() },
    }
  );
}

describe("fieldErrors", () => {
  it("getFieldErrors returns [] for network errors, non-axios errors and old-shape responses", () => {
    const networkError = new AxiosError("Network Error", "ERR_NETWORK");
    expect(getFieldErrors(networkError)).toEqual([]);

    expect(getFieldErrors(new Error("plain error"))).toEqual([]);
    expect(getFieldErrors(null)).toEqual([]);
    expect(getFieldErrors("oops")).toEqual([]);

    const oldShape = makeAxiosError({
      success: false,
      message: "Validation failed.",
      data: null,
      errors: ["Name is required."],
    });
    expect(getFieldErrors(oldShape)).toEqual([]);
  });

  it("getFieldErrors extracts valid FieldError entries from response data", () => {
    const err = makeAxiosError({
      success: false,
      message: "Some rows need correction.",
      fieldErrors: [
        { field: "batchNumber", message: "Batch number is required." },
        { field: "sampleName", message: "Required", row: 2 },
      ],
    });

    expect(getFieldErrors(err)).toEqual([
      { field: "batchNumber", message: "Batch number is required." },
      { field: "sampleName", message: "Required", row: 2 },
    ]);
  });

  it("errorsByField maps top-level field errors and ignores row-scoped errors", () => {
    const err = makeAxiosError({
      success: false,
      message: "Validation failed.",
      fieldErrors: [
        { field: "batchNumber", message: "Batch number is required." },
        { field: "batchNumber", message: "Duplicate message ignored." },
        { field: "sampleName", message: "Row error", row: 0 },
        { field: "controlNumber", message: "Another top-level error", row: null },
      ],
    });

    expect(errorsByField(err)).toEqual({
      batchNumber: "Batch number is required.",
      controlNumber: "Another top-level error",
    });
  });

  it("errorsByRow groups by row", () => {
    const err = makeAxiosError({
      success: false,
      message: "Some rows need correction.",
      fieldErrors: [
        { field: "category", message: "Top-level error" },
        { field: "batchNumber", message: "Required", row: 0 },
        { field: "controlNumber", message: "Invalid format", row: 0 },
        { field: "batchNumber", message: "Duplicate", row: 3 },
      ],
    });

    expect(errorsByRow(err)).toEqual({
      0: {
        batchNumber: "Required",
        controlNumber: "Invalid format",
      },
      3: {
        batchNumber: "Duplicate",
      },
    });
  });

  it("getErrorMessage unchanged for old responses", () => {
    const oldErr = makeAxiosError({
      success: false,
      message: "Lot is expired.",
      data: null,
      errors: null,
    });
    expect(getErrorMessage(oldErr, "Fallback")).toBe("Lot is expired.");

    const noServerMessage = makeAxiosError(
      { success: false, message: null, errors: ["Something"] },
      "Request failed with status code 400"
    );
    expect(getErrorMessage(noServerMessage, "Fallback")).toBe(
      "Request failed with status code 400"
    );

    expect(getErrorMessage(new Error("boom"), "Fallback")).toBe("Fallback");
  });

  it("extractBatchSaveErrors summarizes row errors and falls back for non-row errors", () => {
    const rowErr = makeAxiosError({
      success: false,
      message: "Some rows need correction.",
      fieldErrors: [
        { field: "name", message: "Duplicate", row: 0 },
        { field: "grade", message: "Invalid", row: 0 },
        { field: "name", message: "Exists", row: 2 }
      ]
    });
    expect(extractBatchSaveErrors(rowErr, "Fallback")).toEqual({
      summary: "3 problems in 2 rows. Nothing was created.",
      rowErrors: {
        0: { name: "Duplicate", grade: "Invalid" },
        2: { name: "Exists" }
      }
    });

    expect(extractBatchSaveErrors(new Error("boom"), "Fallback")).toEqual({
      summary: "Fallback",
      rowErrors: null
    });
  });
});
