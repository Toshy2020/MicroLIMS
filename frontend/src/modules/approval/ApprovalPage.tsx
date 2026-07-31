import { useEffect, useState } from "react";
import { apiClient } from "../../services/apiClient";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { ApprovalTable } from "./ApprovalTable";
import { DecisionDialog } from "./DecisionDialog";

export function ApprovalPage() {
  const [rows, setRows] = useState<{ testOrderId: number; testCode: string; status: string }[]>([]);
  const [selected, setSelected] = useState<number | null>(null);

  const load = () => apiClient.get("/testorders").then((res) => {
    const flat = res.data.data.flatMap((s: any) =>
      s.assignedTests.map((t: { testOrderId: number; testCode: string; status: string }) => t)
    );
    setRows(flat);
  });

  useEffect(() => { load(); }, []);

  return (
    <>
      <PageHeader title="Approval" subtitle="Workflow history, results, and the final decision." />
      <SectionTitle>Awaiting Approval</SectionTitle>
      <ApprovalTable rows={rows} onSelect={setSelected} />
      <DecisionDialog open={selected !== null} testOrderId={selected} onClose={() => { setSelected(null); load(); }} />
    </>
  );
}
