import { useMemo } from "react";
import { useSearchParams } from "react-router-dom";
import { Tab, Tabs } from "@mui/material";
import { useAuth } from "../../contexts/AuthContext";
import { useMyLabs } from "../../hooks/useMyLabs";
import { WorkspaceSkeleton } from "../../components/WorkspaceSkeleton";
import { AnalystDashboardPage } from "./AnalystDashboardPage";
import { ReviewerDashboardPage } from "./ReviewerDashboardPage";
import { SectionHeadDashboardPage } from "./SectionHeadDashboardPage";
import { AdminDashboardPage } from "./AdminDashboardPage";
import { DashboardLabCode, DashboardLabProvider, LAB_LABELS, makeDashboardLab } from "./DashboardLabContext";

// Microbiology first, the same order the workspace redirect uses.
const LAB_ORDER: DashboardLabCode[] = ["MICRO", "FP"];

export function DashboardPage() {
  const { role } = useAuth();
  const { codes, physchemAreas, loading } = useMyLabs();
  const [params, setParams] = useSearchParams();

  // Each dashboard is for one laboratory: a Microbiology dashboard never
  // shows Physicochemical activity and vice versa. The laboratory lives in
  // the address (?lab=FP) so a reload, a bookmark or a shared link keeps it.
  const labs = LAB_ORDER.filter((c) => codes.includes(c));
  const requested = params.get("lab")?.toUpperCase() as DashboardLabCode | undefined;
  const code: DashboardLabCode | null = requested && labs.includes(requested) ? requested : labs[0] ?? null;
  const lab = useMemo(() => makeDashboardLab(code, physchemAreas), [code, physchemAreas]);

  if (loading) return <WorkspaceSkeleton tiles={4} rows={5} />;

  const selectLab = (next: DashboardLabCode) =>
    setParams((p) => {
      const copy = new URLSearchParams(p);
      copy.set("lab", next);
      return copy;
    }, { replace: true });

  // Role-specific operational dashboards. SectionHead (and any other role)
  // gets laboratory-wide operational oversight.
  const Page =
    role === "Analyst" ? AnalystDashboardPage
      : role === "Reviewer" ? ReviewerDashboardPage
        : role === "SystemAdministrator" ? AdminDashboardPage
          : SectionHeadDashboardPage;

  return (
    <DashboardLabProvider value={lab}>
      {labs.length > 1 && code && (
        <Tabs
          value={code}
          onChange={(_, next: DashboardLabCode) => selectLab(next)}
          aria-label="Laboratory dashboard"
          sx={{ mb: 2, borderBottom: 1, borderColor: "divider" }}
        >
          {labs.map((c) => (
            <Tab key={c} value={c} label={LAB_LABELS[c]} />
          ))}
        </Tabs>
      )}
      {/* Keyed on the laboratory so switching remounts the page: no figure
          from the other laboratory survives the switch while new data loads. */}
      <Page key={code ?? "none"} />
    </DashboardLabProvider>
  );
}
