import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, fireEvent, cleanup } from "@testing-library/react";
import { TestAreaField } from "./TestAreaField";

afterEach(cleanup);

describe("TestAreaField", () => {
  it("renders with 'Also used for RM & PM' on fp page", () => {
    const onChange = vi.fn();
    render(<TestAreaField pageArea="fp" value="FinishedProduct" onChange={onChange} />);

    const checkbox = screen.getByRole("checkbox", { name: "Also used for RM & PM" });
    expect(checkbox).toBeTruthy();
    expect((checkbox as HTMLInputElement).checked).toBe(false);

    fireEvent.click(checkbox);
    expect(onChange).toHaveBeenCalledWith("Both");
  });

  it("renders with 'Also used for FP' on rmpm page", () => {
    const onChange = vi.fn();
    render(<TestAreaField pageArea="rmpm" value="RawPackaging" onChange={onChange} />);

    const checkbox = screen.getByRole("checkbox", { name: "Also used for FP" });
    expect(checkbox).toBeTruthy();
    expect((checkbox as HTMLInputElement).checked).toBe(false);

    fireEvent.click(checkbox);
    expect(onChange).toHaveBeenCalledWith("Both");
  });

  it("shows checked when value is Both, unchecking on fp gives FinishedProduct", () => {
    const onChange = vi.fn();
    render(<TestAreaField pageArea="fp" value="Both" onChange={onChange} />);

    const checkbox = screen.getByRole("checkbox", { name: "Also used for RM & PM" });
    expect((checkbox as HTMLInputElement).checked).toBe(true);

    fireEvent.click(checkbox);
    expect(onChange).toHaveBeenCalledWith("FinishedProduct");
  });

  it("shows checked when value is Both, unchecking on rmpm gives RawPackaging", () => {
    const onChange = vi.fn();
    render(<TestAreaField pageArea="rmpm" value="Both" onChange={onChange} />);

    const checkbox = screen.getByRole("checkbox", { name: "Also used for FP" });
    expect((checkbox as HTMLInputElement).checked).toBe(true);

    fireEvent.click(checkbox);
    expect(onChange).toHaveBeenCalledWith("RawPackaging");
  });
});
