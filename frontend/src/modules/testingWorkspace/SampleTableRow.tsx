import { useState } from "react";
import { TableRow, TableCell, Box, Typography, Collapse, IconButton } from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import { SampleCard as SampleCardType } from "./types/workspaceTypes";
import { EditableCell } from "./EditableCell";
import { WorkspaceService } from "./services/WorkspaceService";
import { CategoryBadge } from "../../components/StatusBadge";
import { SampleLifecycleBadge } from "./SampleLifecycleBadge";
import { useAuth } from "../../contexts/AuthContext";
import { brandColors } from "../../theme";

interface Props {
  sample: SampleCardType;
  isSelected?: boolean;
  onSelectSample?: (sample: SampleCardType) => void;
  onNeedsPreparationClick: () => void;
  onCorrected: () => void;
  onLifecycleBadgeClick: (sampleId: number) => void;
  visibleColumns: Set<string>;
  colSpan: number;
  isCompact?: boolean;
}

const PRODUCT_LIKE = ["FinishedProduct", "RawMaterial", "PackagingMaterial"];
const formatDate = (d: string | null) => (d ? new Date(d).toLocaleDateString() : "");

function assignedToLabel(tests: SampleCardType["assignedTests"]): string {
  const names = Array.from(new Set(tests.map((t) => t.assignedAnalystName).filter((n): n is string => !!n)));
  if (names.length === 0) return "Unassigned";
  if (names.length === 1) return names[0];
  return `${names[0]} +${names.length - 1}`;
}

export function SampleTableRow({
  sample,
  isSelected,
  onSelectSample,
  onNeedsPreparationClick,
  onCorrected,
  onLifecycleBadgeClick,
  visibleColumns,
  colSpan,
  isCompact
}: Props) {
  const { role } = useAuth();
  const [expanded, setExpanded] = useState(false);
  const needsPreparation = sample.preparationStatus === "NeedsPreparation";
  const isProductLike = PRODUCT_LIKE.includes(sample.category);
  const isWater = sample.category === "Water";
  const isEmOrAfterCleaning = sample.category === "EnvironmentalMonitoring" || sample.category === "AfterCleaning";
  const hasDetails = !isCompact && (isProductLike || isWater || (!isEmOrAfterCleaning && sample.sampleQuantity) || sample.sampledBy);

  const correct = async (field: "batchNumber" | "controlNumber", value: string) => {
    await WorkspaceService.correctSample(
      sample.sampleId,
      field === "batchNumber" ? value : undefined,
      field === "controlNumber" ? value : undefined
    );
    onCorrected();
  };

  const handleRowClick = () => {
    if (onSelectSample) {
      onSelectSample(sample);
    }
  };

  if (isCompact) {
    return (
      <TableRow
        hover
        onClick={handleRowClick}
        sx={{
          cursor: "pointer",
          bgcolor: isSelected ? "#faf5ff" : "inherit",
          borderLeft: isSelected
            ? `4px solid ${brandColors.sectionTitle}`
            : needsPreparation
            ? "4px solid #f59e0b"
            : "4px solid transparent",
          "&:hover": { bgcolor: isSelected ? "#faf5ff" : "#fdfbfe" }
        }}
      >
        <TableCell sx={{ py: 1.25 }}>
          <Typography sx={{ fontWeight: isSelected ? 700 : 600, fontSize: 13, color: isSelected ? brandColors.pageTitle : "#111827" }}>
            {sample.displayName}
          </Typography>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
            {sample.referenceNumber}
          </Typography>
        </TableCell>

        <TableCell sx={{ py: 1.25 }}>
          <CategoryBadge category={sample.category} />
        </TableCell>

        <TableCell sx={{ py: 1.25 }}>
          <Typography sx={{ fontSize: 11, color: "#374151", fontWeight: 600 }}>
            {sample.batchNumber ? `B: ${sample.batchNumber}` : `C: ${sample.controlNumber || "—"}`}
          </Typography>
        </TableCell>

        <TableCell sx={{ py: 1.25 }}>
          <SampleLifecycleBadge
            status={sample.status}
            role={role}
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
        onClick={handleRowClick}
        sx={{
          cursor: "pointer",
          bgcolor: isSelected ? "#faf5ff" : "inherit",
          borderLeft: isSelected
            ? `4px solid ${brandColors.sectionTitle}`
            : needsPreparation
            ? "4px solid #f59e0b"
            : "4px solid transparent",
          "&:hover": { bgcolor: isSelected ? "#faf5ff" : "#fdfbfe" }
        }}
      >
        <TableCell sx={{ width: 36 }} onClick={(e) => e.stopPropagation()}>
          {hasDetails && (
            <IconButton size="small" onClick={() => setExpanded((e) => !e)}>
              {expanded ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
            </IconButton>
          )}
        </TableCell>

        <TableCell>
          <Typography sx={{ fontWeight: isSelected ? 700 : 600, fontSize: 13, color: isSelected ? brandColors.pageTitle : "#111827" }}>
            {sample.displayName}
          </Typography>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>{sample.referenceNumber}</Typography>
        </TableCell>

        {visibleColumns.has("category") && (
          <TableCell>
            <CategoryBadge category={sample.category} />
          </TableCell>
        )}

        {visibleColumns.has("batch") && (
          <TableCell onClick={(e) => e.stopPropagation()}>
            {isProductLike ? (
              <EditableCell
                value={sample.batchNumber ?? ""}
                editable={!sample.incubationStarted}
                onSave={(v) => correct("batchNumber", v)}
              />
            ) : (
              "—"
            )}
          </TableCell>
        )}

        {visibleColumns.has("control") && (
          <TableCell onClick={(e) => e.stopPropagation()}>
            <EditableCell
              value={sample.controlNumber}
              editable={!sample.incubationStarted}
              onSave={(v) => correct("controlNumber", v)}
            />
          </TableCell>
        )}

        {visibleColumns.has("cause") && (
          <TableCell sx={{ fontSize: 12 }}>{sample.causeOfTesting}</TableCell>
        )}

        {visibleColumns.has("receivedAt") && (
          <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>
            {formatDate(sample.receivedAt)}
          </TableCell>
        )}

        {visibleColumns.has("assignedTo") && (
          <TableCell sx={{ fontSize: 12 }}>{assignedToLabel(sample.assignedTests)}</TableCell>
        )}

        {visibleColumns.has("status") && (
          <TableCell onClick={(e) => e.stopPropagation()}>
            <SampleLifecycleBadge
              status={sample.status}
              role={role}
              onClick={() => onLifecycleBadgeClick(sample.sampleId)}
            />
          </TableCell>
        )}
      </TableRow>

      {hasDetails && (
        <TableRow>
          <TableCell sx={{ p: 0, border: 0 }} colSpan={colSpan}>
            <Collapse in={expanded} unmountOnExit>
              <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 1, p: 1.5, bgcolor: "#faf9fc" }}>
                {sample.category === "FinishedProduct" && sample.productionStage && (
                  <DetailField label="Production Stage" value={sample.productionStage} />
                )}
                {!isEmOrAfterCleaning && sample.sampleQuantity && (
                  <DetailField label="Sample Quantity" value={sample.sampleQuantity} />
                )}
                <DetailField label="Sampled By" value={sample.sampledBy} />
                {isProductLike && <DetailField label="Mfg Date" value={formatDate(sample.mfgDate)} />}
                {isProductLike && <DetailField label="Exp Date" value={formatDate(sample.expDate)} />}
                {isWater && (
                  <DetailField
                    label="Sampling Point"
                    value={sample.waterSamplingPointCode ? `${sample.waterSamplingPointCode} — ${sample.waterSamplingPointLocation}` : ""}
                  />
                )}
                {isWater && sample.storageCondition && (
                  <DetailField
                    label="Storage Condition"
                    value={sample.storageCondition === "Refrigerator" ? `Refrigerator (${sample.storageTimeHours ?? "?"}h)` : sample.storageCondition}
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

function DetailField({ label, value }: { label: string; value: string }) {
  return (
    <Box>
      <Typography sx={{ fontSize: 10, color: "text.secondary" }}>{label}</Typography>
      <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{value || "—"}</Typography>
    </Box>
  );
}
