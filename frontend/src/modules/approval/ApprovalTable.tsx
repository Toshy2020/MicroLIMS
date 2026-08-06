import { DataTable, Column } from "../../components/DataTable";
import { StatusBadge } from "../../components/StatusBadge";

interface ApprovalRow { testOrderId: number; testCode: string; status: string; }

export function ApprovalTable({ rows, onSelect }: { rows: ApprovalRow[]; onSelect: (id: number) => void }) {
  const columns: Column<ApprovalRow>[] = [
    { key: "testCode", label: "Test" },
    { key: "status", label: "Status", render: (r) => <StatusBadge status={r.status} /> }
  ];
  return <DataTable columns={columns} rows={rows} getRowId={(r) => r.testOrderId} onRowClick={(r) => onSelect(r.testOrderId)} />;
}
