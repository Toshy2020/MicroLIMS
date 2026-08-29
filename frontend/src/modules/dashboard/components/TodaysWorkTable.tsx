import { useMemo, useState } from "react";
import {
  Paper, Table, TableHead, TableRow, TableCell, TableBody, Typography, Stack,
  ToggleButtonGroup, ToggleButton, Box
} from "@mui/material";
import { Link } from "react-router-dom";
import { TodaysWorkItem } from "../types/dashboard";
import { StatusBadge, CategoryBadge } from "../../../components/StatusBadge";
import { SectionTitle } from "../../../components/SectionTitle";

type Tab = "All" | "Returned" | "Incubating" | "ReadyToRead" | "AwaitingReview" | "UnderApproval" | "Overdue";
const OVERDUE_THRESHOLD_HOURS = 24;

function matchesTab(item: TodaysWorkItem, tab: Tab): boolean {
  if (tab === "All") return true;
  if (tab === "Returned") return item.tests.some((t) => t.isReturned);
  if (tab === "Incubating") return item.tests.some((t) => t.timeRemaining && t.timeRemaining !== "Ready to read");
  if (tab === "ReadyToRead") return item.tests.some((t) => t.timeRemaining === "Ready to read");
  if (tab === "AwaitingReview") return item.overallStatus === "ResultEntered";
  if (tab === "UnderApproval") return item.overallStatus === "Reviewed";
  if (tab === "Overdue") {
    const hoursSinceReceived = (Date.now() - new Date(item.receivedAt).getTime()) / 3_600_000;
    return hoursSinceReceived > OVERDUE_THRESHOLD_HOURS && (item.overallStatus === "Pending" || item.overallStatus === "InProgress");
  }
  return true;
}

export function TodaysWorkTable({ items }: { items: TodaysWorkItem[] }) {
  const [tab, setTab] = useState<Tab>("All");

  const counts = useMemo(() => ({
    All: items.length,
    Returned: items.filter((i) => matchesTab(i, "Returned")).length,
    Incubating: items.filter((i) => matchesTab(i, "Incubating")).length,
    ReadyToRead: items.filter((i) => matchesTab(i, "ReadyToRead")).length,
    AwaitingReview: items.filter((i) => matchesTab(i, "AwaitingReview")).length,
    UnderApproval: items.filter((i) => matchesTab(i, "UnderApproval")).length,
    Overdue: items.filter((i) => matchesTab(i, "Overdue")).length
  }), [items]);

  const visible = useMemo(() => items.filter((i) => matchesTab(i, tab)), [items, tab]);

  const tabs: { key: Tab; label: string }[] = [
    { key: "All", label: `All (${counts.All})` },
    { key: "Returned", label: `Returned (${counts.Returned})` },
    { key: "Incubating", label: `Incubating (${counts.Incubating})` },
    { key: "ReadyToRead", label: `Ready to Read (${counts.ReadyToRead})` },
    { key: "AwaitingReview", label: `Awaiting Review (${counts.AwaitingReview})` },
    { key: "UnderApproval", label: `Under Approval (${counts.UnderApproval})` },
    { key: "Overdue", label: `Overdue (${counts.Overdue})` }
  ];

  return (
    <>
      <SectionTitle tabs={[{ label: "View all", to: "/testing-workspace" }]}>Today's Laboratory Work</SectionTitle>
      <ToggleButtonGroup
        value={tab} exclusive size="small"
        onChange={(_, v) => v && setTab(v)}
        sx={{ mb: 1.5, flexWrap: "wrap", "& .MuiToggleButton-root": { textTransform: "none", fontSize: 12, fontWeight: 600, borderRadius: "16px !important", border: "1px solid #e0e0e0 !important", mr: 1, mb: 1 } }}
      >
        {tabs.map((t) => <ToggleButton key={t.key} value={t.key}>{t.label}</ToggleButton>)}
      </ToggleButtonGroup>

      <Paper sx={{ overflowX: "auto" }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Sample / Reference</TableCell>
              <TableCell>Category</TableCell>
              <TableCell>Tests</TableCell>
              <TableCell>Received At</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Next Action</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {visible.map((item) => (
              <TableRow key={item.sampleId} hover>
                <TableCell>
                  <Typography
                    component={Link}
                    to={`/testing-workspace?sampleId=${item.sampleId}`}
                    sx={{
                      fontWeight: 600,
                      fontSize: 13,
                      color: "text.primary",
                      textDecoration: "none",
                      display: "block",
                      "&:hover": { color: "primary.main", textDecoration: "underline" }
                    }}
                  >
                    {item.displayName || item.referenceNumber}
                  </Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{item.referenceNumber}</Typography>
                </TableCell>
                <TableCell><CategoryBadge category={item.category} /></TableCell>
                <TableCell>
                  <Stack direction="row" spacing={0.5} flexWrap="wrap" rowGap={0.5}>
                    {item.tests.map((t) => (
                      <Box key={t.testOrderId} sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                        <Typography sx={{ fontSize: 12 }}>{t.testCode}</Typography>
                        <StatusBadge status={t.status} />
                        {t.isReturned && <StatusBadge status="Returned" label="Returned" />}
                        {t.timeRemaining && <Typography sx={{ fontSize: 11, color: "text.secondary" }}>{t.timeRemaining}</Typography>}
                      </Box>
                    ))}
                  </Stack>
                </TableCell>
                <TableCell sx={{ fontSize: 12 }}>{new Date(item.receivedAt).toLocaleString()}</TableCell>
                <TableCell><StatusBadge status={item.overallStatus} /></TableCell>
                <TableCell sx={{ fontSize: 12 }}>{item.nextAction}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        {visible.length === 0 && (
          <Typography sx={{ color: "#9ca3af", fontSize: 13, p: 2 }}>No work matches this filter.</Typography>
        )}
      </Paper>
    </>
  );
}
