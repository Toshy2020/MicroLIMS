import { useEffect, useState } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { Box, Alert } from "@mui/material";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { useMyLabs } from "../../hooks/useMyLabs";
import { laboratorySectionService, LaboratorySection } from "../../services/laboratorySectionService";
import { ReceivingTestingWorkspacePage, WorkspaceLab } from "./ReceivingTestingWorkspacePage";
import { FP_WORKSPACE_PATH, RMPM_WORKSPACE_PATH, firstWorkspacePath } from "../../routes/physchemWorkspacePath";
import { ReceiveService } from "../receiving/services/ReceiveService";

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

interface LabWorkspaceRouteProps {
  code: WorkspaceLab["code"];
  area?: "fp" | "rmpm";
}

// Resolves one lab workspace route (/microbiology/workspace,
// /physicochemical/workspace, /physicochemical/rm-pm-workspace) to the caller's own
// section id + display name. Membership comes from useMyLabs (Task 11 - a System Administrator
// counts as a member of both labs); the id/name come from the full section
// list rather than useMyLabs's own `labs` array, because an administrator
// doesn't always carry a real membership row for a lab they administer.
export function LabWorkspaceRoute({ code, area }: LabWorkspaceRouteProps) {
  const { codes, physchemAreas, loading: labsLoading } = useMyLabs();
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

  if (code === "FP" && area && !physchemAreas.includes(area)) {
    return (
      <NotAMember
        message={
          area === "rmpm"
            ? "You do not have access to the RM & PM workspace."
            : "You do not have access to the FP workspace."
        }
      />
    );
  }

  const section = sections?.find((s) => s.sectionCode === code);
  if (!section) {
    return (
      <Box sx={{ p: 4, maxWidth: 800, mx: "auto" }}>
        <Alert severity="error">Unable to load laboratory details. Please refresh the page.</Alert>
      </Box>
    );
  }

  const lab = { sectionId: section.sectionId, code, name: section.sectionName, area };
  const other = area === "fp" ? "rmpm" : "fp";
  if (code === "FP" && area && physchemAreas.includes(other)) {
    return <OtherAreaSampleGuard key={area} lab={lab} otherPath={other === "rmpm" ? RMPM_WORKSPACE_PATH : FP_WORKSPACE_PATH} />;
  }
  return <ReceivingTestingWorkspacePage lab={lab} />;
}

// A per-sample deep link (notification, dashboard row) cannot tell which area its sample is in.
// When this area's workspace answers 404 for the requested sample and the user has the other
// area, send them there with the same query. The state flag stops a bounce between the two.
function OtherAreaSampleGuard({ lab, otherPath }: { lab: WorkspaceLab; otherPath: string }) {
  const location = useLocation();
  const sampleId = Number(new URLSearchParams(location.search).get("sampleId"));
  const alreadyRedirected = (location.state as { areaRedirected?: boolean } | null)?.areaRedirected === true;
  const check = sampleId > 0 && !alreadyRedirected;
  const [state, setState] = useState<"checking" | "stay" | "redirect">(check ? "checking" : "stay");

  useEffect(() => {
    if (!check) return;
    let cancelled = false;
    ReceiveService.getSample(sampleId, lab.sectionId, lab.area)
      .then(() => !cancelled && setState("stay"))
      .catch((err) => !cancelled && setState(err?.response?.status === 404 ? "redirect" : "stay"));
    return () => {
      cancelled = true;
    };
  }, [check, sampleId, lab.sectionId, lab.area]);

  if (state === "checking") return <LoadingSpinner />;
  if (state === "redirect") return <Navigate to={`${otherPath}${location.search}`} replace state={{ areaRedirected: true }} />;
  return <ReceivingTestingWorkspacePage lab={lab} />;
}

// /receiving-testing has no lab of its own any more - it lands on the
// first lab workspace the caller belongs to (Microbiology first),
// preserving the query string so deep links from notifications and
// dashboards still land on the right sample/filter.
export function FirstLabWorkspaceRedirect() {
  const { codes, physchemAreas, loading } = useMyLabs();
  const location = useLocation();

  if (loading) return <LoadingSpinner />;

  const target = LAB_CODES.find((c) => codes.includes(c));
  if (!target) {
    return <NotAMember message="You are not a member of any laboratory workspace." />;
  }

  const path = target === "MICRO" ? "/microbiology/workspace" : firstWorkspacePath(physchemAreas);
  return <Navigate to={`${path}${location.search}`} replace />;
}
