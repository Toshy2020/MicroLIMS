import { useEffect, useMemo, useState } from "react";
import { Box, MenuItem, Paper, Select, TextField, Typography } from "@mui/material";
import { MonitoringChecklist } from "../../monitoringPlan/components/MonitoringChecklist";
import { WaterPreparationService } from "./services/WaterPreparationService";
import { SamplingPointGrid, SamplingPointGridItem } from "../../testPreparation/components/SamplingPointGrid";
import { useTestDefinitions, type TestDefinitionOption } from "../../../hooks/useTestDefinitions";

interface SamplingPoint { id: number; code: string; location: string; assignedTestCodes: string[] }

interface Props {
  sampleId: number;
  waterDepartmentId: number;
  labSectionId?: number | null;
  onComplete: () => void;
}

export function isTestCodeInLab(
  code: string,
  labSectionId: number | null | undefined,
  testDefinitions: TestDefinitionOption[]
): boolean {
  const def = testDefinitions.find((t) => t.code === code);
  if (labSectionId != null) {
    if (!def) return false;
    return def.sectionId === labSectionId || def.section?.id === labSectionId;
  }
  // null or undefined labSectionId -> treat as the MICRO lab
  const hasMicroSection = testDefinitions.some(
    (t) => t.section?.code?.toUpperCase() === "MICRO"
  );
  if (!hasMicroSection) {
    // keep current behaviour if section code isn't available
    return true;
  }
  if (!def) return false;
  const microSectionId = testDefinitions.find(
    (t) => t.section?.code?.toUpperCase() === "MICRO"
  )?.section?.id ?? testDefinitions.find(
    (t) => t.section?.code?.toUpperCase() === "MICRO"
  )?.sectionId;

  return (
    def.section?.code?.toUpperCase() === "MICRO" ||
    (microSectionId != null && (def.sectionId === microSectionId || def.section?.id === microSectionId))
  );
}

// Grid/card-based selection per sampling point - checking a point includes ALL of its
// assigned tests in this batch (one TestOrder per distinct TestCode
// across every selected point, not one TestOrder per point). Mirrors
// EMPreparationForm's Room grid.
export function WaterPreparationForm({ sampleId, waterDepartmentId, labSectionId, onComplete }: Props) {
  const [points, setPoints] = useState<SamplingPoint[]>([]);
  const [loadingPoints, setLoadingPoints] = useState(true);
  const [checked, setChecked] = useState<Record<number, boolean>>({});
  const [monitoringIds, setMonitoringIds] = useState<number[]>([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const { options: testDefinitions, loading: testDefsLoading } = useTestDefinitions();

  // How the collected water was held before testing. Refrigerated samples
  // have a hold-time limit, so the duration is required alongside it.
  const [storageCondition, setStorageCondition] = useState("");
  const [storageTimeHours, setStorageTimeHours] = useState("");

  const storageIncomplete =
    !storageCondition || (storageCondition === "Refrigerator" && !storageTimeHours);

  useEffect(() => {
    let cancelled = false;
    setLoadingPoints(true);
    WaterPreparationService.getSamplingPointsForDepartment(waterDepartmentId)
      .then((data) => {
        if (!cancelled) setPoints(data ?? []);
      })
      .catch(() => {
        if (!cancelled) setPoints([]);
      })
      .finally(() => {
        if (!cancelled) setLoadingPoints(false);
      });
    return () => { cancelled = true; };
  }, [waterDepartmentId]);

  const items: SamplingPointGridItem[] = useMemo(() => {
    return points
      .map((point) => {
        const allowedCodes = (point.assignedTestCodes ?? []).filter((code) =>
          isTestCodeInLab(code, labSectionId, testDefinitions)
        );
        return {
          id: point.id,
          title: point.code,
          subtitle: point.location || undefined,
          assignedTests: allowedCodes,
          disabled: allowedCodes.length === 0
        };
      })
      .filter((item) => item.assignedTests.length > 0);
  }, [points, testDefinitions, labSectionId]);

  const toggle = (pointId: number) => setChecked((c) => ({ ...c, [pointId]: !c[pointId] }));

  const handleSelectAll = (select: boolean) => {
    const next: Record<number, boolean> = {};
    items.forEach((item) => {
      if (!item.disabled) {
        next[item.id] = select;
      }
    });
    setChecked(next);
  };

  const confirm = async () => {
    setMessage(null);
    setLoading(true);
    const waterSamplingPointIds = items.filter((item) => checked[item.id]).map((item) => item.id);

    try {
      await WaterPreparationService.prepare(
        sampleId,
        waterSamplingPointIds,
        storageCondition,
        storageCondition === "Refrigerator" ? Number(storageTimeHours) : null,
        monitoringIds
      );
      onComplete();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare sample.", ok: false });
    } finally {
      setLoading(false);
    }
  };

  const isStillLoading = loadingPoints || (testDefsLoading && testDefinitions.length === 0);

  return (
    <Box>
      <MonitoringChecklist sampleId={sampleId} onChange={setMonitoringIds} />
      {isStillLoading ? (
        <Typography sx={{ fontSize: 13, color: "text.secondary", py: 2 }}>
          Loading sampling points...
        </Typography>
      ) : (
        <>
          <Paper
            variant="outlined"
            sx={{ p: 1.5, mb: 2, display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}
          >
            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>
                Storage Condition *
              </Typography>
              <Select
                displayEmpty
                fullWidth
                size="small"
                value={storageCondition}
                onChange={(e) => setStorageCondition(e.target.value)}
                inputProps={{ "aria-label": "Storage condition" }}
              >
                <MenuItem value=""><em>Select storage condition</em></MenuItem>
                <MenuItem value="Refrigerator">Refrigerator</MenuItem>
                <MenuItem value="RoomTemperature">Room Temperature</MenuItem>
              </Select>
            </Box>

            {storageCondition === "Refrigerator" && (
              <Box>
                <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>
                  Storage Time (hours) *
                </Typography>
                <TextField
                  fullWidth
                  size="small"
                  type="number"
                  value={storageTimeHours}
                  onChange={(e) => setStorageTimeHours(e.target.value)}
                  slotProps={{ htmlInput: { "aria-label": "Storage time (hours)" } }}
                />
              </Box>
            )}
          </Paper>

          <SamplingPointGrid
            items={items}
            selectedIds={checked}
            onToggle={toggle}
            onSelectAll={handleSelectAll}
            onConfirm={confirm}
            loading={loading}
            errorMessage={message?.text}
            confirmDisabled={storageIncomplete}
          />
        </>
      )}
    </Box>
  );
}
