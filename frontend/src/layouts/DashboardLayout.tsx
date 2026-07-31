import { ReactNode } from "react";
import { Grid } from "@mui/material";

// Different dashboard content is composed per role - see modules/dashboard.
export function DashboardLayout({ children }: { children: ReactNode }) {
  return <Grid container spacing={2}>{children}</Grid>;
}
