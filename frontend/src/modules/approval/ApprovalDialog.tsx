import { Typography } from "@mui/material";

// Section Head sees: Workflow history -> Results -> Decision
export function ApprovalDialog({ testOrderId }: { testOrderId: number }) {
  return <Typography variant="body1">Workflow history + results for test order #{testOrderId}</Typography>;
}
