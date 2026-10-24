import { fireEvent, screen } from "@testing-library/react";

const ISO = /^(\d{4})-(\d{2})-(\d{2})$/;
const DISPLAY = /^(\d{2})\/(\d{2})\/(\d{4})$/;

// The field is a group of day/month/year sections named by its label.
export function getDateField(label: string | RegExp): HTMLElement {
  return screen.getByRole("group", { name: label });
}

function hiddenInput(label: string | RegExp): HTMLInputElement {
  const input = getDateField(label).querySelector("input");
  if (!input) throw new Error(`DateField "${label}" has no input`);
  return input;
}

// Sets a DateField the way the old native <input type="date"> tests did with
// fireEvent.change. The picker renders its date as day/month/year sections over
// a hidden input; writing a DD/MM/YYYY string (or "") into that input is the
// picker's own supported route for a whole-value change. "YYYY-MM-DD" is
// accepted too, so tests can keep speaking the value the app stores.
export function changeDateField(label: string | RegExp, date: string) {
  const iso = ISO.exec(date);
  const value = iso ? `${iso[3]}/${iso[2]}/${iso[1]}` : date;
  fireEvent.change(hiddenInput(label), { target: { value } });
}

// The field's current date as "YYYY-MM-DD", or "" when empty.
export function dateFieldValue(label: string | RegExp): string {
  const m = DISPLAY.exec(hiddenInput(label).value);
  return m ? `${m[3]}-${m[2]}-${m[1]}` : "";
}
