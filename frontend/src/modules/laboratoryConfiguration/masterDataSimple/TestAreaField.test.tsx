import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { i18n } from "../../../i18n";
import { TestAreaField } from "./TestAreaField";

afterEach(cleanup);

describe("TestAreaField", () => {
  it("renders with 'Also used for RM & PM' on fp page", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<TestAreaField pageArea="fp" value="FinishedProduct" onChange={onChange} />);

    const checkbox = screen.getByRole("checkbox", { name: "Also used for RM & PM" });
    expect(checkbox).toBeTruthy();
    expect((checkbox as HTMLInputElement).checked).toBe(false);

    await user.click(checkbox);
    expect(onChange).toHaveBeenCalledWith("Both");
  });

  it("renders with 'Also used for FP' on rmpm page", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<TestAreaField pageArea="rmpm" value="RawPackaging" onChange={onChange} />);

    const checkbox = screen.getByRole("checkbox", { name: "Also used for FP" });
    expect(checkbox).toBeTruthy();
    expect((checkbox as HTMLInputElement).checked).toBe(false);

    await user.click(checkbox);
    expect(onChange).toHaveBeenCalledWith("Both");
  });

  it("shows checked when value is Both, unchecking on fp gives FinishedProduct", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<TestAreaField pageArea="fp" value="Both" onChange={onChange} />);

    const checkbox = screen.getByRole("checkbox", { name: "Also used for RM & PM" });
    expect((checkbox as HTMLInputElement).checked).toBe(true);

    await user.click(checkbox);
    expect(onChange).toHaveBeenCalledWith("FinishedProduct");
  });

  it("shows checked when value is Both, unchecking on rmpm gives RawPackaging", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<TestAreaField pageArea="rmpm" value="Both" onChange={onChange} />);

    const checkbox = screen.getByRole("checkbox", { name: "Also used for FP" });
    expect((checkbox as HTMLInputElement).checked).toBe(true);

    await user.click(checkbox);
    expect(onChange).toHaveBeenCalledWith("RawPackaging");
  });

  it("renders checkbox label in Arabic when Arabic is active", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["common", "masterData"]);
    try {
      render(<TestAreaField pageArea="fp" value="FinishedProduct" onChange={vi.fn()} />);
      expect(await screen.findByRole("checkbox", { name: "يُستخدم أيضًا لـ RM & PM" })).toBeTruthy();
    } finally {
      await i18n.changeLanguage("en");
    }
  });
});
