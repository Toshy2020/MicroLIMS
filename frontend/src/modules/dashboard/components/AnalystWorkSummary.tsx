import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlined";
import AccessTimeIcon from "@mui/icons-material/AccessTime";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import { useDashboardLab } from "../DashboardLabContext";
import { SummaryTiles, SummaryTile } from "../../../components/configHierarchy/SummaryTiles";

interface AnalystWorkSummaryProps {
  overdueCount: number | null;
  dueNowCount: number | null;
  readyToReadCount: number;
}

export function AnalystWorkSummary({ overdueCount, dueNowCount, readyToReadCount }: AnalystWorkSummaryProps) {
  const lab = useDashboardLab();

  // Workspace filters per tile; the path is the dashboard's own laboratory.
  const tiles: SummaryTile[] = [
    { label: "Overdue", value: overdueCount ?? "—", tone: "detected", icon: <ErrorOutlineIcon />, to: lab.workspace("?scope=mine&urgency=overdue") },
    { label: "Due now", value: dueNowCount ?? "—", tone: "action", icon: <AccessTimeIcon />, to: lab.workspace("?scope=mine") }
  ];
  // "Ready to read" is the end of an incubation - Microbiology only.
  if (!lab.isPhyschem) {
    tiles.push({
      label: "Ready to read",
      value: readyToReadCount,
      tone: "notDetected",
      icon: <CheckCircleOutlineIcon />,
      to: lab.workspace("?scope=mine&testStatus=ReadyToRead")
    });
  }

  return <SummaryTiles tiles={tiles} />;
}
