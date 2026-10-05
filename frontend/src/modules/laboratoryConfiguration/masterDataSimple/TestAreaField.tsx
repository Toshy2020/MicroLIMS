import React from "react";
import { FormControlLabel, Checkbox } from "@mui/material";
import { PageArea, PhyschemArea, toggleBoth, isBoth } from "./testMasterArea";

export interface TestAreaFieldProps {
  pageArea: PageArea;
  value: PhyschemArea | null | undefined;
  onChange: (area: PhyschemArea) => void;
  disabled?: boolean;
}

/**
 * Checkbox field to toggle whether a test is also used in the other area.
 * On FP: "Also used for RM & PM".
 * On RM & PM: "Also used for FP".
 */
export const TestAreaField: React.FC<TestAreaFieldProps> = ({
  pageArea,
  value,
  onChange,
  disabled = false
}) => {
  const label = pageArea === "fp" ? "Also used for RM & PM" : "Also used for FP";
  const checked = isBoth(value);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    onChange(toggleBoth(value, pageArea, e.target.checked));
  };

  return (
    <FormControlLabel
      control={
        <Checkbox
          checked={checked}
          onChange={handleChange}
          disabled={disabled}
          size="small"
         
        />
      }
      label={label}
    />
  );
};
