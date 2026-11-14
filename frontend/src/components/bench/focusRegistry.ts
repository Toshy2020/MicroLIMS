// A tiny registry of the numeric field that currently has focus, so the
// on-screen keypad can write into it without taking focus itself.

export interface ActiveNumericField {
  element: HTMLInputElement;
  kind: "count" | "decimal";
  maxLength: number;
  getValue: () => string;
  onChange: (value: string) => void;
  onNext?: () => void;
}

export const NUMERIC_FIELD_ATTRIBUTE = "data-numeric-entry";

let active: ActiveNumericField | null = null;
const listeners = new Set<() => void>();

function notify() {
  listeners.forEach((listener) => listener());
}

export function setActiveField(field: ActiveNumericField): void {
  active = field;
  notify();
}

export function clearActiveField(element: HTMLInputElement): void {
  if (active?.element === element) {
    active = null;
    notify();
  }
}

export function getActiveField(): ActiveNumericField | null {
  return active;
}

export function subscribeActiveField(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function allSelected(element: HTMLInputElement, value: string): boolean {
  return value.length > 0 && element.selectionStart === 0 && element.selectionEnd === value.length;
}

export function applyDigit(digit: string): void {
  const field = active;
  if (!field) return;
  if (digit === "." && field.kind !== "decimal") return;
  const current = field.getValue();
  const base = allSelected(field.element, current) ? "" : current;
  const next = base + digit;
  if (next.length > field.maxLength) return;
  field.onChange(next);
}

export function applyBackspace(): void {
  const field = active;
  if (!field) return;
  const current = field.getValue();
  field.onChange(allSelected(field.element, current) ? "" : current.slice(0, -1));
}

export function applyNext(): void {
  const field = active;
  if (!field) return;
  field.onNext?.();
  const scope: ParentNode = field.element.closest('[role="dialog"]') ?? document;
  const fields = Array.from(scope.querySelectorAll<HTMLInputElement>(`input[${NUMERIC_FIELD_ATTRIBUTE}]`)).filter((el) => !el.disabled);
  const index = fields.indexOf(field.element);
  const following = index >= 0 ? fields[index + 1] : undefined;
  following?.focus();
}
