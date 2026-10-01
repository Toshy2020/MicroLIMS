import { Stack } from "@mui/material";
import type { ReactNode } from "react";
import { PageHeader } from "../PageHeader";

interface LabPageProps {
  title: string;
  subtitle?: string;
  // Primary action(s), right-aligned in the header.
  actions?: ReactNode;
  kpis?: ReactNode;
  filters?: ReactNode;
  children: ReactNode;
}

// Page anatomy for register-style pages: header -> KPIs -> filters ->
// content. Gaps live here so pages never set their own; the outer padding
// comes from MainLayout's <main>, like every other page (this used to add
// its own p: 3 on top, so these pages sat 24px further in than the rest).
export function LabPage({ title, subtitle, actions, kpis, filters, children }: LabPageProps) {
  return (
    <Stack spacing={2}>
      <PageHeader title={title} subtitle={subtitle}>{actions}</PageHeader>
      {kpis}
      {filters}
      {children}
    </Stack>
  );
}
