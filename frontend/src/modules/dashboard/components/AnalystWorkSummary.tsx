import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlined";
import AccessTimeIcon from "@mui/icons-material/AccessTime";
import TodayIcon from "@mui/icons-material/Today";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import { MyTask } from "../types/dashboard";
import { useDashboardLab } from "../DashboardLabContext";
import { SummaryTiles, SummaryTile } from "../../../components/configHierarchy/SummaryTiles";

interface AnalystWorkSummaryProps {
  tasks: MyTask[];
  readyToReadCount: number;
}

export function AnalystWorkSummary({ tasks, readyToReadCount }: AnalystWorkSummaryProps) {
  const lab = useDashboardLab();

  const count = (urgency: string) => tasks.filter((t) => t.urgency === urgency).length;

  // Workspace filters per tile; the path is the dashboard's own laboratory.
  const tiles: SummaryTile[] = [
    { label: "Overdue", value: count("Overdue"), tone: "detected", icon: <ErrorOutlineIcon />, to: lab.workspace("?scope=mine&urgency=overdue") },
    { label: "Due now", value: count("DueSoon"), tone: "action", icon: <AccessTimeIcon />, to: lab.workspace("?scope=mine") },
    { label: "Due today", value: count("DueToday"), tone: "info", icon: <TodayIcon />, to: lab.workspace("?scope=mine") }
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
