import { Button } from "@mui/material";
import VisibilityIcon from "@mui/icons-material/Visibility";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { Ltr } from "../../../components/Ltr";
import { RegisterTable, ResultSection } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import { useTechnique } from "../useTechnique";
import type { HplcRunListItem, HplcTechnique } from "../types";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

export interface HplcRunHistoryTableProps {
  historyRuns: HplcRunListItem[];
  equipmentId: number;
  technique?: HplcTechnique;
  onSelectRun: (runId: number) => void;
}

export function HplcRunHistoryTable({
  historyRuns,
  technique,
  onSelectRun
}: HplcRunHistoryTableProps) {
  const { t } = useTranslation("hplcWorkspace");
  const { technique: routeTechnique } = useTechnique();
  const activeTechnique = technique ?? routeTechnique;
  const label = activeTechnique === "Gc" ? "GC" : "HPLC";
  const columns: RegisterColumn<HplcRunListItem>[] = [
    {
      key: "code",
      label: t("history.columns.runCode"),
      sortable: true,
      render: (hr) => <span style={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}><Ltr>{hr.code}</Ltr></span>
    },
    { key: "methodAbbreviation", label: t("history.columns.method"), sortable: true },
    { key: "analystUserName", label: t("history.columns.analyst"), sortable: true },
    {
      key: "startedAt",
      label: t("history.columns.startedAt"),
      sortable: true,
      sortValue: (hr) => new Date(hr.startedAt).getTime(),
      render: (hr) => <Ltr>{formatDateTimeWithTimeZone(hr.startedAt)}</Ltr>
    },
    {
      key: "closedAt",
      label: t("history.columns.closedAt"),
      sortable: true,
      sortValue: (hr) => (hr.closedAt ? new Date(hr.closedAt).getTime() : null),
      render: (hr) => (hr.closedAt ? <Ltr>{formatDateTimeWithTimeZone(hr.closedAt)}</Ltr> : "—")
    },
    { key: "status", label: t("history.columns.runStatus"), sortable: true, render: (hr) => <HplcStatusBadge status={hr.status} /> },
    { key: "sstStatus", label: t("history.columns.sstStatus"), sortable: true, render: (hr) => <HplcStatusBadge status={hr.sstStatus} /> },
    {
      key: "action",
      label: t("history.columns.action"),
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
          {t("history.view")}
        </Button>
      )
    }
  ];

  return (
    <ResultSection title={t("history.title", { count: historyRuns.length })}>
      <RegisterTable
        columns={columns}
        rows={historyRuns}
        getRowId={(hr) => hr.id}
        onRowClick={(hr) => onSelectRun(hr.id)}
        defaultSort={{ key: "startedAt", direction: "desc" }}
        empty={{
          title: t("history.emptyTitle"),
          description: t("history.emptyDescription", { label })
        }}
      />
    </ResultSection>
  );
}
