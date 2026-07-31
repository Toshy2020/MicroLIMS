import { DataTable, Column } from "../../components/DataTable";
import { StatusBadge } from "../../components/StatusBadge";

interface ReviewRow { testOrderId: number; testCode: string; status: string; }

// "Reviewer chooses: Detailed workflow or Quick table review."
export function ReviewTable({ rows, onSelect }: { rows: ReviewRow[]; onSelect: (id: number) => void }) {
  const columns: Column<ReviewRow>[] = [
    { key: "testCode", label: "Test" },
    { key: "status", label: "Status", render: (r) => <StatusBadge status={r.status} /> }
  ];
  return <DataTable columns={columns} rows={rows} getRowId={(r) => r.testOrderId} />;
}
