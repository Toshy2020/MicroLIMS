import { Button } from "@mui/material";
import VisibilityIcon from "@mui/icons-material/Visibility";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { RegisterTable, ResultSection } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { monospaceFontFamily } from "../../../theme/palette";
import type { HplcRunListItem } from "../types";

export interface HplcRunHistoryTableProps {
  historyRuns: HplcRunListItem[];
  equipmentId: number;
  onSelectRun: (runId: number) => void;
}

export function HplcRunHistoryTable({
  historyRuns,
  onSelectRun
}: HplcRunHistoryTableProps) {
  const columns: RegisterColumn<HplcRunListItem>[] = [
    {
      key: "code",
      label: "Run Code",
      sortable: true,
      render: (hr) => <span style={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>{hr.code}</span>
    },
    { key: "methodAbbreviation", label: "Method", sortable: true },
    { key: "analystUserName", label: "Analyst", sortable: true },
    {
      key: "startedAt",
      label: "Started At",
      sortable: true,
      sortValue: (hr) => new Date(hr.startedAt).getTime(),
      render: (hr) => new Date(hr.startedAt).toLocaleString()
    },
    {
      key: "closedAt",
      label: "Closed At",
      sortable: true,
      sortValue: (hr) => (hr.closedAt ? new Date(hr.closedAt).getTime() : null),
      render: (hr) => (hr.closedAt ? new Date(hr.closedAt).toLocaleString() : "—")
    },
    { key: "status", label: "Run Status", sortable: true, render: (hr) => <HplcStatusBadge status={hr.status} /> },
    { key: "sstStatus", label: "SST Status", sortable: true, render: (hr) => <HplcStatusBadge status={hr.sstStatus} /> },
    {
      key: "action",
      label: "Action",
      align: "right",
      render: (hr) => (
        <Button
          size="small"
          variant="outlined"
          startIcon={<VisibilityIcon fontSize="small" />}
          onClick={(e) => {
            e.stopPropagation();
            onSelectRun(hr.id);
          }}
          sx={{ textTransform: "none" }}
        >
          View
        </Button>
      )
    }
  ];

  return (
    <ResultSection title={`Instrument run history (${historyRuns.length})`}>
      <RegisterTable
        columns={columns}
        rows={historyRuns}
        getRowId={(hr) => hr.id}
        onRowClick={(hr) => onSelectRun(hr.id)}
        defaultSort={{ key: "startedAt", direction: "desc" }}
        empty={{
          title: "No past runs recorded",
          description: "Completed, abandoned, and past HPLC runs on this instrument appear here."
        }}
      />
    </ResultSection>
  );
}
