import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Button, ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import {
  LAST_CHOICE_CAPTION,
  LastChoiceField,
  UNCONFIRMED_LOT_SAVE_REASON,
  useLastChoiceDefault,
  type LastChoiceKind
} from "./LastChoiceField";

// Minimal host for the hook: the "Apply" button stands in for a server default arriving.
function Host({ kind }: { kind: LastChoiceKind }) {
  const lastChoice = useLastChoiceDefault<number>(kind);
  return (
    <ThemeProvider theme={lightTheme}>
      <Button onClick={() => lastChoice.applyDefault(1)}>Apply</Button>
      <Button onClick={lastChoice.notifyValueChanged}>User changes value</Button>
      <LastChoiceField
        kind={kind}
        isDefault={lastChoice.isDefault}
        confirmed={lastChoice.confirmed}
        onConfirm={lastChoice.confirm}
      >
        <span>field</span>
      </LastChoiceField>
      <span data-testid="reason">{lastChoice.saveBlockedReason ?? "none"}</span>
    </ThemeProvider>
  );
}

afterEach(cleanup);

describe("LastChoiceField", () => {
  it("shows nothing extra until a default is applied", () => {
    render(<Host kind="lot" />);
    expect(screen.queryByText(LAST_CHOICE_CAPTION)).toBeNull();
    expect(screen.getByTestId("reason").textContent).toBe("none");
  });

  it("shows the caption for an equipment default and needs no confirmation", async () => {
    const user = userEvent.setup();
    render(<Host kind="equipment" />);
    await user.click(screen.getByRole("button", { name: "Apply" }));
    expect(screen.getByText(LAST_CHOICE_CAPTION)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Confirm" })).toBeNull();
    expect(screen.getByTestId("reason").textContent).toBe("none");
  });

  it("blocks saving a pre-filled lot until it is confirmed", async () => {
    const user = userEvent.setup();
    render(<Host kind="lot" />);
    await user.click(screen.getByRole("button", { name: "Apply" }));
    expect(screen.getByTestId("reason").textContent).toBe(UNCONFIRMED_LOT_SAVE_REASON);
    await user.click(screen.getByRole("button", { name: "Confirm" }));
    expect(screen.getByTestId("reason").textContent).toBe("none");
    expect(screen.getByText(LAST_CHOICE_CAPTION)).toBeTruthy();
  });

  it("changing the value removes the caption and unblocks saving", async () => {
    const user = userEvent.setup();
    render(<Host kind="lot" />);
    await user.click(screen.getByRole("button", { name: "Apply" }));
    await user.click(screen.getByRole("button", { name: "User changes value" }));
    expect(screen.queryByText(LAST_CHOICE_CAPTION)).toBeNull();
    expect(screen.queryByRole("button", { name: "Confirm" })).toBeNull();
    expect(screen.getByTestId("reason").textContent).toBe("none");
  });
});
