import { expect } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import type userEvent from "@testing-library/user-event";
import { changeDateField } from "../../../../components/DateField.testUtils";
import { EQUIPMENT_TYPES } from "./equipmentOptions";
import type { EquipmentPayloadFixture } from "./__fixtures__/equipmentPayloads";

export type User = ReturnType<typeof userEvent.setup>;

async function setText(user: User, dialog: HTMLElement, label: RegExp | string, value: string) {
  const input = within(dialog).getByLabelText(label);
  await user.clear(input);
  if (value) await user.type(input, value);
}

export async function clickNext(user: User, dialog: HTMLElement) {
  await user.click(within(dialog).getByRole("button", { name: /^Next$/ }));
}

export async function chooseType(user: User, dialog: HTMLElement, type: string) {
  await user.click(within(dialog).getByRole("combobox", { name: /Equipment Type/ }));
  const label = EQUIPMENT_TYPES.find((t) => t.value === type)?.label ?? type;
  await user.click(await screen.findByRole("option", { name: label }));
}

// Enters every field of the fixture model, step by step, and stops on the Review step.
export async function fillWizardToReview(user: User, dialog: HTMLElement, fixture: EquipmentPayloadFixture) {
  const { model } = fixture;
  await setText(user, dialog, /^Equipment Name/, model.name);
  await setText(user, dialog, /^Equipment Code/, model.code);
  await chooseType(user, dialog, model.type);
  await setText(user, dialog, "Vendor / Manufacturer", model.vendor);
  if (model.sectionId !== "" && !fixture.ctx.isEditing && fixture.ctx.sectionCount > 1) {
    await user.click(within(dialog).getByRole("combobox", { name: /Laboratory Section/ }));
    await user.click(await screen.findByRole("option", { name: /Environmental/ }));
  }
  await setText(user, dialog, "Location", model.location);
  await clickNext(user, dialog);

  const connection = within(dialog).queryByLabelText("Connection Settings (optional)");
  if (connection) await setText(user, dialog, "Connection Settings (optional)", model.connectionSettings);
  const setPoint = within(dialog).queryByLabelText(/Set Point Temp/);
  if (setPoint) await setText(user, dialog, /Set Point Temp/, model.setPointTemperature);
  await clickNext(user, dialog);

  if (model.calibrationDueDate) changeDateField("Calibration Due Date", model.calibrationDueDate);
  if (model.equipmentInventoryId === 42) {
    await user.click(within(dialog).getByRole("combobox", { name: "Asset register entry" }));
    await user.click(await screen.findByText("AST-INC-42 — Binder Incubator"));
  }
  await clickNext(user, dialog);
}

export async function clickSave(user: User, dialog: HTMLElement) {
  const save = await within(dialog).findByRole("button", { name: /^Save$/ });
  await waitFor(() => expect((save as HTMLButtonElement).disabled).toBe(false));
  await user.click(save);
}
