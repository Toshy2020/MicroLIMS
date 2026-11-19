import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { MfaCodeField } from "./MfaCodeField";

afterEach(() => {
  cleanup();
});

function ControlledField({
  initialValue = "",
  onValueChange,
  error,
  disabled,
  autoFocus
}: {
  initialValue?: string;
  onValueChange?: (value: string) => void;
  error?: string;
  disabled?: boolean;
  autoFocus?: boolean;
}) {
  const [value, setValue] = useState(initialValue);
  return (
    <MfaCodeField
      value={value}
      onChange={(next) => {
        setValue(next);
        onValueChange?.(next);
      }}
      error={error}
      disabled={disabled}
      autoFocus={autoFocus}
    />
  );
}

describe("MfaCodeField", () => {
  it('renders a labelled "6-digit code" numeric input with one-time-code autocomplete', () => {
    render(
      <PinnedLightTheme>
        <ControlledField autoFocus />
      </PinnedLightTheme>
    );

    const input = screen.getByLabelText("6-digit code") as HTMLInputElement;
    expect(input).toBeTruthy();
    expect(input.getAttribute("inputmode")).toBe("numeric");
    expect(input.getAttribute("autocomplete")).toBe("one-time-code");
    expect(document.activeElement).toBe(input);
  });

  it("accepts digits only up to 6 characters", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <PinnedLightTheme>
        <ControlledField onValueChange={onChange} />
      </PinnedLightTheme>
    );

    const input = screen.getByLabelText("6-digit code") as HTMLInputElement;
    await user.type(input, "12ab34-5678");

    expect(input.value).toBe("123456");
    expect(onChange).toHaveBeenLastCalledWith("123456");
  });

  it("links the error message to the input as helper text and supports disabled", () => {
    render(
      <PinnedLightTheme>
        <ControlledField error="Enter all 6 digits." disabled />
      </PinnedLightTheme>
    );

    const input = screen.getByLabelText("6-digit code") as HTMLInputElement;
    expect(input.disabled).toBe(true);
    expect(input.getAttribute("aria-invalid")).toBe("true");
    const describedBy = input.getAttribute("aria-describedby");
    expect(describedBy).toBeTruthy();
    expect(document.getElementById(describedBy!)?.textContent).toBe("Enter all 6 digits.");
  });
});
