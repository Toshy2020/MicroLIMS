import { useEffect, useState } from "react";
import { apiClient } from "../../services/apiClient";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { ReviewTable } from "./ReviewTable";
import { ReviewDialog } from "./ReviewDialog";

export function ReviewPage() {
  const [rows, setRows] = useState<{ testOrderId: number; testCode: string; status: string }[]>([]);
  const [selected, setSelected] = useState<number | null>(null);

  const load = () => apiClient.get("/testorders").then((res) => {
    const flat = res.data.data.flatMap((s: any) =>
      s.assignedTests.map((t: { testOrderId: number; testCode: string; status: string }) => t)
    );
    // Only ResultEntered test orders are actually reviewable - see
    // ReviewService.MarkReviewedAsync's gate. Everything else (Pending,
    // InProgress, ...) would just fail with "results not entered yet."
    setRows(flat.filter((t: { status: string }) => t.status === "ResultEntered"));
  });

  useEffect(() => { load(); }, []);

  return (
    <>
      <PageHeader title="Review" subtitle="Choose a detailed workflow review or a quick table review." />
      <SectionTitle>Awaiting Review</SectionTitle>
      <ReviewTable rows={rows} onSelect={setSelected} />
      <ReviewDialog open={selected !== null} testOrderId={selected} onClose={() => { setSelected(null); load(); }} />
    </>
  );
}
