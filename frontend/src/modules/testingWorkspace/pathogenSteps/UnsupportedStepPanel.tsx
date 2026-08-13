import { Alert } from "@mui/material";

// The explicit fallback every step-type switch in this module must have.
// Rendering a growth-observation form here by accident is the exact bug
// class this whole refactor exists to eliminate - this panel is what
// stands in its place instead of a silent fallthrough.
export function UnsupportedStepPanel({ stepType }: { stepType: string }) {
  return (
    <Alert severity="error">
      This workflow step's type ("{stepType}") is not supported by this dialog. Contact a System Administrator -
      the Test Master template for this step may be misconfigured.
    </Alert>
  );
}
