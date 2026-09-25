import { useEffect, useState } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { Box, Alert } from "@mui/material";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { useMyLabs } from "../../hooks/useMyLabs";
import { laboratorySectionService, LaboratorySection } from "../../services/laboratorySectionService";
import { ReceivingTestingWorkspacePage, WorkspaceLab } from "./ReceivingTestingWorkspacePage";

const LAB_NAMES: Record<WorkspaceLab["code"], string> = {
  MICRO: "Microbiology Laboratory",
  FP: "Physicochemical Laboratory"
};

// Checked in Microbiology-first order wherever "the first lab the user
// belongs to" matters (design.md §4 lists Microbiology first).
const LAB_CODES: WorkspaceLab["code"][] = ["MICRO", "FP"];

function NotAMember({ message }: { message: string }) {
  return (
    <Box sx={{ p: 4, maxWidth: 800, mx: "auto" }}>
      <Alert severity="error">
        <strong>Not a member:</strong> {message}
      </Alert>
    </Box>
  );
}

// Resolves one lab workspace route (/microbiology/workspace,
// /physicochemical/workspace) to the caller's own section id + display
// name. Membership comes from useMyLabs (Task 11 - a System Administrator
// counts as a member of both labs); the id/name come from the full section
// list rather than useMyLabs's own `labs` array, because an administrator
// doesn't always carry a real membership row for a lab they administer.
export function LabWorkspaceRoute({ code }: { code: WorkspaceLab["code"] }) {
  const { codes, loading: labsLoading } = useMyLabs();
  const [sections, setSections] = useState<LaboratorySection[] | null>(null);
  const [sectionsFailed, setSectionsFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    laboratorySectionService
      .getSections()
      .then((data) => {
        if (!cancelled) setSections(data ?? []);
      })
      .catch(() => {
        if (!cancelled) setSectionsFailed(true);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  if (labsLoading || (sections === null && !sectionsFailed)) {
    return <LoadingSpinner />;
  }

  if (!codes.includes(code)) {
    return <NotAMember message={`You are not a member of the ${LAB_NAMES[code]}.`} />;
  }

  const section = sections?.find((s) => s.sectionCode === code);
  if (!section) {
    return (
      <Box sx={{ p: 4, maxWidth: 800, mx: "auto" }}>
        <Alert severity="error">Unable to load laboratory details. Please refresh the page.</Alert>
      </Box>
    );
  }

  return <ReceivingTestingWorkspacePage lab={{ sectionId: section.sectionId, code, name: section.sectionName }} />;
}

// /receiving-testing has no lab of its own any more - it lands on the
// first lab workspace the caller belongs to (Microbiology first),
// preserving the query string so deep links from notifications and
// dashboards still land on the right sample/filter.
export function FirstLabWorkspaceRedirect() {
  const { codes, loading } = useMyLabs();
  const location = useLocation();

  if (loading) return <LoadingSpinner />;

  const target = LAB_CODES.find((c) => codes.includes(c));
  if (!target) {
    return <NotAMember message="You are not a member of any laboratory workspace." />;
  }

  const path = target === "MICRO" ? "/microbiology/workspace" : "/physicochemical/workspace";
  return <Navigate to={`${path}${location.search}`} replace />;
}
