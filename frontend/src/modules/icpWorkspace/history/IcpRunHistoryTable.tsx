import { Button } from "@mui/material";
import VisibilityIcon from "@mui/icons-material/Visibility";
import { IcpStatusBadge } from "../components/IcpStatusBadge";
import { Ltr } from "../../../components/Ltr";
import { RegisterTable, ResultSection } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import type { IcpRunListItem } from "../types";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

export interface IcpRunHistoryTableProps {
  historyRuns: IcpRunListItem[];
  equipmentId: number;
  onSelectRun: (runId: number) => void;
}

export function IcpRunHistoryTable({
  historyRuns,
  onSelectRun
}: IcpRunHistoryTableProps) {
  const { t } = useTranslation("icpWorkspace");
  const columns: RegisterColumn<IcpRunListItem>[] = [
    {
      key: "code",
      label: t("history.columns.runCode"),
      sortable: true,
      render: (hr) => <span style={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}><Ltr>{hr.code}</Ltr></span>
    },
    { key: "analystUserName", label: t("history.columns.analyst"), sortable: true },
    {
      key: "startedAt",
      label: t("history.columns.startedAt"),
      sortable: true,
      sortValue: (hr) => new Date(hr.startedAt).getTime(),
      render: (hr) => <Ltr>{formatDateTimeWithTimeZone(hr.startedAt)}</Ltr>
    },
    {
      key: "sampleCount",
      label: t("history.columns.samples"),
      sortable: true,
      align: "right",
      render: (hr) => <span style={{ fontVariantNumeric: "tabular-nums" }}><Ltr>{hr.sampleCount}</Ltr></span>
    },
    { key: "status", label: t("history.columns.runStatus"), sortable: true, render: (hr) => <IcpStatusBadge status={hr.status} /> },
    { key: "calibrationStatus", label: t("history.columns.calibration"), sortable: true, render: (hr) => <IcpStatusBadge status={hr.calibrationStatus} /> },
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
          description: t("history.emptyDescription")
        }}
      />
    </ResultSection>
  );
}
