import type { ReactElement } from "react";
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import {
  Button, Checkbox, IconButton, MenuItem, MenuList, Radio, Switch, Tab, Tabs,
  Table, TableBody, TableCell, TableRow, TextField
} from "@mui/material";
import { getTheme } from "./index";

function renderIn(bench: boolean, ui: ReactElement) {
  return render(<ThemeProvider theme={getTheme("light", bench)}>{ui}</ThemeProvider>);
}

// Each case renders the real MUI component under the theme and measures the
// resolved CSS of the element that receives the touch.
const cases: { name: string; ui: ReactElement; find: () => HTMLElement; width: boolean }[] = [
  { name: "Button", ui: <Button>Save</Button>, find: () => screen.getByRole("button", { name: "Save" }), width: true },
  { name: "IconButton", ui: <IconButton aria-label="Close">x</IconButton>, find: () => screen.getByRole("button", { name: "Close" }), width: true },
  { name: "TextField", ui: <TextField label="Count" />, find: () => screen.getByLabelText("Count").parentElement as HTMLElement, width: false },
  { name: "Checkbox", ui: <Checkbox slotProps={{ input: { "aria-label": "Tick" } }} />, find: () => screen.getByLabelText("Tick").closest(".MuiCheckbox-root") as HTMLElement, width: true },
  { name: "Radio", ui: <Radio slotProps={{ input: { "aria-label": "Pick" } }} />, find: () => screen.getByLabelText("Pick").closest(".MuiRadio-root") as HTMLElement, width: true },
  { name: "Switch", ui: <Switch slotProps={{ input: { "aria-label": "Flip" } }} />, find: () => screen.getByLabelText("Flip").closest(".MuiSwitch-root") as HTMLElement, width: true },
  { name: "Tab", ui: <Tabs value={0}><Tab label="Results" /></Tabs>, find: () => screen.getByRole("tab", { name: "Results" }), width: true },
  { name: "MenuItem", ui: <MenuList><MenuItem>Open</MenuItem></MenuList>, find: () => screen.getByRole("menuitem", { name: "Open" }), width: true },
  {
    name: "TableCell",
    ui: <Table><TableBody><TableRow><TableCell>cell</TableCell></TableRow></TableBody></Table>,
    find: () => screen.getByRole("cell", { name: "cell" }),
    width: false
  }
];

describe("bench theme touch targets", () => {
  it.each(cases)("$name resolves to at least 48 px in bench mode", ({ ui, find, width }) => {
    renderIn(true, ui);
    const style = getComputedStyle(find());
    expect(parseFloat(style.minHeight)).toBeGreaterThanOrEqual(48);
    if (width) expect(parseFloat(style.minWidth)).toBeGreaterThanOrEqual(48);
  });

  // MUI already gives Tab and MenuItem a 48 px minimum, so the baseline check
  // only applies to the controls the bench theme actually changes.
  it.each(cases.filter((c) => c.name !== "Tab" && c.name !== "MenuItem"))("$name has no 48 px minimum outside bench mode", ({ ui, find }) => {
    renderIn(false, ui);
    const minHeight = parseFloat(getComputedStyle(find()).minHeight);
    expect(Number.isNaN(minHeight) || minHeight < 48).toBe(true);
  });

  it("uses 18 px text in entry inputs", () => {
    renderIn(true, <TextField label="Count" />);
    expect(getComputedStyle(screen.getByLabelText("Count")).fontSize).toBe("18px");
  });

  it("flags the theme through custom.bench", () => {
    expect(getTheme("light", true).custom.bench).toBe(true);
    expect(getTheme("dark", true).custom.bench).toBe(true);
    expect(getTheme("light").custom.bench).toBe(false);
    expect(getTheme("dark", false).custom.bench).toBe(false);
  });

  it("keeps the mode's palette in bench mode", () => {
    expect(getTheme("dark", true).palette.mode).toBe("dark");
    expect(getTheme("light", true).palette.primary.main).toBe(getTheme("light").palette.primary.main);
  });
});
