import { Button } from "@mui/material";
import VisibilityIcon from "@mui/icons-material/Visibility";
import { IcpStatusBadge } from "../components/IcpStatusBadge";
import { RegisterTable, ResultSection } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { monospaceFontFamily } from "../../../theme/palette";
import type { IcpRunListItem } from "../types";

export interface IcpRunHistoryTableProps {
  historyRuns: IcpRunListItem[];
  equipmentId: number;
  onSelectRun: (runId: number) => void;
}

export function IcpRunHistoryTable({
  historyRuns,
  onSelectRun
}: IcpRunHistoryTableProps) {
  const columns: RegisterColumn<IcpRunListItem>[] = [
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
      key: "sampleCount",
      label: "Samples",
      sortable: true,
      align: "right",
      render: (hr) => <span style={{ fontVariantNumeric: "tabular-nums" }}>{hr.sampleCount}</span>
    },
    { key: "status", label: "Run Status", sortable: true, render: (hr) => <IcpStatusBadge status={hr.status} /> },
    { key: "calibrationStatus", label: "Calibration", sortable: true, render: (hr) => <IcpStatusBadge status={hr.calibrationStatus} /> },
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
          description: "Completed, abandoned, and past ICP runs on this instrument appear here."
        }}
      />
    </ResultSection>
  );
}
