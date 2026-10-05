import React from "react";
import { Box, TextField } from "@mui/material";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import {
  TitrationFormState,
  titrationFormFromDefinition,
  titrationPayloadFields
} from "./titrationConfig";
import type { PhyschemArea } from "./testMasterArea";

export interface TitrationChangeReasonFieldProps {
  value: string;
  onChange: (value: string) => void;
  error?: string | null;
  disabled?: boolean;
}

/**
 * Checks whether any titration field (or workflow type to/from Titration) differs
 * between the originally loaded test definition and the current form state.
 */
export function hasTitrationSettingsChanged(
  initialTest: TestDefinitionOption | null,
  currentForm: TitrationFormState,
  currentWorkflowType: string,
  currentArea?: PhyschemArea | null
): boolean {
  if (!initialTest) return false;
  const wasTitration = initialTest.workflowType === "Titration";
  const isTitration = currentWorkflowType === "Titration";

  if (!wasTitration && !isTitration) return false;
  if (wasTitration !== isTitration) return true;

  // Both were/are Titration: compare the normalized payload fields and replicate count
  const before = {
    ...titrationPayloadFields(titrationFormFromDefinition(initialTest), true),
    replicateCount: initialTest.replicateCount != null ? Number(initialTest.replicateCount) : 3
  };
  const after = {
    ...titrationPayloadFields(currentForm, true),
    replicateCount: Number(currentForm.replicateCount) || 3
  };

  // The area is part of the titration snapshot on the server (spec 2026-10-05 section 2).
  if (currentArea && currentArea !== (initialTest.physchemArea ?? null)) return true;

  type Key = keyof typeof before;
  for (const k of Object.keys(before) as Key[]) {
    if (before[k] !== after[k]) {
      return true;
    }
  }
  return false;
}

/**
 * Validates the changeReason string (must be 5 to 500 characters).
 * Returns an error message or null if valid.
 */
export function validateTitrationChangeReason(reason: string | null | undefined): string | null {
  const trimmed = (reason ?? "").trim();
  if (!trimmed || trimmed.length < 5 || trimmed.length > 500) {
    return "A reason for change is required when titration settings change (5 to 500 characters).";
  }
  return null;
}

/**
 * One answer for the save step: is a reason required (the test was or is
 * Titration and its titration settings changed, including switching away from
 * Titration), is the typed one valid, and what to send.
 */
export function titrationChangeReasonCheck(
  initialTest: TestDefinitionOption | null,
  currentForm: TitrationFormState,
  currentWorkflowType: string,
  reason: string,
  currentArea?: PhyschemArea | null
): { required: boolean; error: string | null; payload: string | null } {
  const required = hasTitrationSettingsChanged(initialTest, currentForm, currentWorkflowType, currentArea);
  return {
    required,
    error: required ? validateTitrationChangeReason(reason) : null,
    payload: required ? reason.trim() : null
  };
}

/**
 * Required field shown in the save step when an existing Titration test's
 * titration settings differ from the loaded values.
 */
export const TitrationChangeReasonField: React.FC<TitrationChangeReasonFieldProps> = ({
  value,
  onChange,
  error,
  disabled = false
}) => {
  return (
    <Box
      sx={{
        p: 2,
        bgcolor: "action.hover",
        borderRadius: 1,
        border: "1px solid",
        borderColor: error ? "error.main" : "divider"
      }}
    >
      <TextField
        size="small"
        label="Reason for Change"
        placeholder="Enter reason for changing titration settings (5–500 characters)"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        required
        fullWidth
        multiline
        minRows={2}
        error={Boolean(error)}
        helperText={error || "A reason for change is required when titration settings change (5–500 characters)."}
        slotProps={{ htmlInput: { minLength: 5, maxLength: 500 } }}
        disabled={disabled}
      />
    </Box>
  );
};
