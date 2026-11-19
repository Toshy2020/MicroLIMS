import { useState } from "react";
import { TableRow, TableCell, Box, Typography, Collapse, IconButton, Checkbox, useTheme } from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import { SampleCard as SampleCardType } from "./types/workspaceTypes";
import { CategoryBadge } from "../../components/StatusBadge";
import { SampleLifecycleBadge } from "./SampleLifecycleBadge";
import { Trans } from "react-i18next";
import { Ltr } from "../../components/Ltr";
import { useTranslation } from "../../i18n";
import { isInteractiveElement } from "../../utils/isInteractiveElement";
import { formatDateTimeWithTimeZone, formatLabCalendarDate } from "../../utils/formatDate";

interface Props {
  sample: SampleCardType;
  isSelected?: boolean;
  onSelectSample?: (sample: SampleCardType) => void;
  onNeedsPreparationClick: () => void;
  onLifecycleBadgeClick: (sampleId: number) => void;
  visibleColumns: Set<string>;
  colSpan: number;
  isCompact?: boolean;
  isChecked?: boolean;
  onToggleCheck?: (sampleId: number, checked: boolean) => void;
}

const PRODUCT_LIKE = ["FinishedProduct", "RawMaterial", "PackagingMaterial"];

function assignedToLabel(tests: SampleCardType["assignedTests"], unassignedLabel: string): string {
  const names = Array.from(new Set(tests.map((t) => t.assignedAnalystName).filter((n): n is string => !!n)));
  if (names.length === 0) return unassignedLabel;
  if (names.length === 1) return names[0];
  return `${names[0]} +${names.length - 1}`;
}

export function SampleTableRow({
  sample,
  isSelected,
  onSelectSample,
  onNeedsPreparationClick,
  onLifecycleBadgeClick,
  visibleColumns,
  colSpan,
  isCompact,
  isChecked,
  onToggleCheck
}: Props) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  const [expanded, setExpanded] = useState(false);
  const edgeOffset = theme.direction === "rtl" ? "-4px" : "4px";
  const needsPreparation = sample.preparationStatus === "NeedsPreparation";
  const isProductLike = PRODUCT_LIKE.includes(sample.category);
  const isWater = sample.category === "Water";
  const isEmOrAfterCleaning = sample.category === "EnvironmentalMonitoring" || sample.category === "AfterCleaning";
  const hasDetails = !isCompact && (isProductLike || isWater || (!isEmOrAfterCleaning && sample.sampleQuantity) || sample.sampledBy);

  const handleRowClick = (e: React.MouseEvent<HTMLTableRowElement>) => {
    if (isInteractiveElement(e.target, e.currentTarget)) {
      return;
    }
    if (onSelectSample) {
      onSelectSample(sample);
    }
  };

  const handleRowKeyDown = (e: React.KeyboardEvent<HTMLTableRowElement>) => {
    if (e.key === "Enter" || e.key === " ") {
      if (isInteractiveElement(e.target, e.currentTarget)) {
        return;
      }
      e.preventDefault();
      onSelectSample?.(sample);
    }
  };

  if (isCompact) {
    return (
      <TableRow
        hover
        tabIndex={0}
        role="row"
        onClick={handleRowClick}
        onKeyDown={handleRowKeyDown}
        sx={{
          cursor: "pointer",
          bgcolor: isSelected ? theme.custom.status.purple.bg : "inherit",
          // Drawn on the first cell: a row border does not render in a
          // collapsed-border table.
          "& > td:first-of-type": {
            boxShadow: `inset ${edgeOffset} 0 0 ${
              sample.isOverdue
                ? theme.custom.status.detected.text
                : isSelected
                ? theme.custom.status.purple.border
                : needsPreparation
                ? theme.custom.status.inconclusive.border
                : "transparent"
            }`
          },
          "&:hover": { bgcolor: isSelected ? theme.custom.status.purple.bg : "action.hover" }
        }}
      >
        <TableCell sx={{ py: 1.25 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
            {onToggleCheck && (
              <Checkbox
                size="small"
                checked={Boolean(isChecked)}
                onChange={(e) => {
                  e.stopPropagation();
                  onToggleCheck(sample.sampleId, e.target.checked);
                }}
                onClick={(e) => e.stopPropagation()}
                sx={{ p: 0.25, mr: 0.25 }}
                slotProps={{
                  input: {
                    "aria-label": t("tableRow.selectForGroupedAria", { name: sample.displayName, ref: sample.referenceNumber })
                  }
                }}
              />
            )}
            <Box sx={{ minWidth: 0 }}>
              <Typography sx={{ fontWeight: isSelected ? 700 : 600, fontSize: 13, color: isSelected ? theme.palette.primary.main : "text.primary" }}>
                {sample.displayName}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                <Ltr>{sample.referenceNumber}</Ltr>
              </Typography>
            </Box>
          </Box>
        </TableCell>

        <TableCell sx={{ py: 1.25 }}>
          <CategoryBadge category={sample.category} />
        </TableCell>

        <TableCell sx={{ py: 1.25 }}>
          <Typography sx={{ fontSize: 12, color: "text.primary", fontWeight: 600 }}>
            {sample.batchNumber
              ? t("tableRow.batchShort", { batch: sample.batchNumber })
              : t("tableRow.controlShort", { control: sample.controlNumber || "—" })}
          </Typography>
        </TableCell>

        <TableCell sx={{ py: 1.25 }} onClick={(e) => e.stopPropagation()}>
          <SampleLifecycleBadge
            status={sample.status}
            onClick={() => onLifecycleBadgeClick(sample.sampleId)}
          />
        </TableCell>
      </TableRow>
    );
  }

  return (
    <>
      <TableRow
        hover
        tabIndex={0}
        role="row"
        onClick={handleRowClick}
        onKeyDown={handleRowKeyDown}
        sx={{
          cursor: "pointer",
          bgcolor: isSelected ? theme.custom.status.purple.bg : "inherit",
          // Drawn on the first cell: a row border does not render in a
          // collapsed-border table.
          "& > td:first-of-type": {
            boxShadow: `inset ${edgeOffset} 0 0 ${
              sample.isOverdue
                ? theme.custom.status.detected.text
                : isSelected
                ? theme.custom.status.purple.border
                : needsPreparation
                ? theme.custom.status.inconclusive.border
                : "transparent"
            }`
          },
          "&:hover": { bgcolor: isSelected ? theme.custom.status.purple.bg : "action.hover" }
        }}
      >
        <TableCell sx={{ width: 36 }} onClick={(e) => e.stopPropagation()}>
          {hasDetails && (
            <IconButton
              size="small"
              onClick={() => setExpanded((e) => !e)}
              aria-label={expanded ? t("tableRow.hideDetailsAria", { name: sample.displayName }) : t("tableRow.showDetailsAria", { name: sample.displayName })}
              aria-expanded={expanded}
            >
              {expanded ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
            </IconButton>
          )}
        </TableCell>

        <TableCell>
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
            {onToggleCheck && (
              <Checkbox
                size="small"
                checked={Boolean(isChecked)}
                onChange={(e) => {
                  e.stopPropagation();
                  onToggleCheck(sample.sampleId, e.target.checked);
                }}
                onClick={(e) => e.stopPropagation()}
                sx={{ p: 0.25, mr: 0.25 }}
                slotProps={{
                  input: {
                    "aria-label": t("tableRow.selectForGroupedAria", { name: sample.displayName, ref: sample.referenceNumber })
                  }
                }}
              />
            )}
            <Box sx={{ minWidth: 0 }}>
              <Typography sx={{ fontWeight: isSelected ? 700 : 600, fontSize: 13, color: isSelected ? theme.palette.primary.main : "text.primary" }}>
                {sample.displayName}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                <Ltr>{sample.referenceNumber}</Ltr>
              </Typography>
            </Box>
          </Box>
        </TableCell>

        {visibleColumns.has("category") && (
          <TableCell>
            <CategoryBadge category={sample.category} />
          </TableCell>
        )}

        {/* Corrections go through the signed Edit Details dialog. */}
        {visibleColumns.has("batch") && (
          <TableCell sx={{ fontSize: 12 }}>{isProductLike && sample.batchNumber ? <Ltr>{sample.batchNumber}</Ltr> : "—"}</TableCell>
        )}

        {visibleColumns.has("control") && (
          <TableCell sx={{ fontSize: 12 }}>{sample.controlNumber ? <Ltr>{sample.controlNumber}</Ltr> : ""}</TableCell>
        )}

        {visibleColumns.has("cause") && (
          <TableCell sx={{ fontSize: 12 }}>{sample.causeOfTesting}</TableCell>
        )}

        {visibleColumns.has("receivedAt") && (
          <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>
            {sample.receivedAt ? <Ltr>{formatDateTimeWithTimeZone(sample.receivedAt)}</Ltr> : ""}
          </TableCell>
        )}

        {visibleColumns.has("assignedTo") && (
          <TableCell sx={{ fontSize: 12 }}>
            {(() => {
              const label = assignedToLabel(sample.assignedTests, t("tableRow.unassigned"));
              return label === t("tableRow.unassigned") ? label : <Ltr>{label}</Ltr>;
            })()}
          </TableCell>
        )}

        {visibleColumns.has("status") && (
          <TableCell onClick={(e) => e.stopPropagation()}>
            <SampleLifecycleBadge
              status={sample.status}
              onClick={() => onLifecycleBadgeClick(sample.sampleId)}
            />
          </TableCell>
        )}
      </TableRow>

      {hasDetails && (
        <TableRow>
          <TableCell sx={{ p: 0, border: 0 }} colSpan={colSpan}>
            <Collapse in={expanded} unmountOnExit>
              <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 1, p: 1.5, bgcolor: "background.default" }}>
                {sample.category === "FinishedProduct" && sample.productionStage && (
                  <DetailField label={t("tableRow.productionStage")} value={sample.productionStage} />
                )}
                {!isEmOrAfterCleaning && sample.sampleQuantity && (
                  <DetailField label={t("tableRow.sampleQuantity")} value={<Ltr>{sample.sampleQuantity}</Ltr>} />
                )}
                <DetailField label={t("tableRow.sampledBy")} value={sample.sampledBy ? <Ltr>{sample.sampledBy}</Ltr> : ""} />
                {isProductLike && <DetailField label={t("tableRow.mfgDate")} value={sample.mfgDate ? <Ltr>{formatLabCalendarDate(sample.mfgDate)}</Ltr> : ""} />}
                {isProductLike && <DetailField label={t("tableRow.expDate")} value={sample.expDate ? <Ltr>{formatLabCalendarDate(sample.expDate)}</Ltr> : ""} />}
                {isWater && (
                  <DetailField
                    label={t("tableRow.samplingPoint")}
                    value={sample.waterSamplingPointCode ? `${sample.waterSamplingPointCode}, ${sample.waterSamplingPointLocation}` : ""}
                  />
                )}
                {isWater && sample.storageCondition && (
                  <DetailField
                    label={t("tableRow.storageCondition")}
                    value={
                      sample.storageCondition === "Refrigerator" ? (
                        <Trans
                          i18nKey="tableRow.refrigeratorHours"
                          ns="testingWorkspace"
                          values={{ hours: sample.storageTimeHours ?? "?" }}
                          components={{ ltr: <Ltr>{null}</Ltr> }}
                        />
                      ) : (
                        sample.storageCondition
                      )
                    }
                  />
                )}
              </Box>
            </Collapse>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

function DetailField({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Box>
      <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{label}</Typography>
      <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{value || "—"}</Typography>
    </Box>
  );
}
