import { fireEvent, screen } from "@testing-library/react";

// Sets a DateField the way the old native <input type="date"> tests did with
// fireEvent.change. The picker renders its date as day/month/year sections over
// a hidden input; writing a DD/MM/YYYY string (or "") into that input is the
// picker's own supported route for a whole-value change.
export function changeDateField(label: string, ddmmyyyy: string) {
  const group = screen.getByRole("group", { name: label });
  const input = group.querySelector("input");
  if (!input) throw new Error(`DateField "${label}" has no input`);
  fireEvent.change(input, { target: { value: ddmmyyyy } });
}
