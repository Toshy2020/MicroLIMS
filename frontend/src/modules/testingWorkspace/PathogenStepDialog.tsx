// TEMPORARY placeholder - Task 5 replaces this file wholesale with the
// real step-type-dispatching implementation.
import { Alert } from "@mui/material";

interface Props { testOrderId: number; testCode: string; displayName: string; }

export function PathogenStepDialog({ testOrderId }: Props) {
  return <Alert severity="info">Pathogen step UI for test order #{testOrderId} - under construction.</Alert>;
}
