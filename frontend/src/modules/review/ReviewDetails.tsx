import { Typography } from "@mui/material";

export function ReviewDetails({ testOrderId }: { testOrderId: number }) {
  return <Typography variant="body1">Detailed workflow view for test order #{testOrderId}</Typography>;
}
