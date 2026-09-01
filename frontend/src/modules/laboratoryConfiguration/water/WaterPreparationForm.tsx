import { useEffect, useMemo, useState } from "react";
import { Box, MenuItem, Paper, Select, TextField, Typography } from "@mui/material";
import { WaterPreparationService } from "./services/WaterPreparationService";
import { SamplingPointGrid, SamplingPointGridItem } from "../../testPreparation/components/SamplingPointGrid";

interface SamplingPoint { id: number; code: string; location: string; assignedTestCodes: string[] }

interface Props {
  sampleId: number;
  waterDepartmentId: number;
  onComplete: () => void;
}

// Grid/card-based selection per sampling point - checking a point includes ALL of its
// assigned tests in this batch (one TestOrder per distinct TestCode
// across every selected point, not one TestOrder per point). Mirrors
// EMPreparationForm's Room grid.
export function WaterPreparationForm({ sampleId, waterDepartmentId, onComplete }: Props) {
  const [points, setPoints] = useState<SamplingPoint[]>([]);
  const [checked, setChecked] = useState<Record<number, boolean>>({});
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  // How the collected water was held before testing. Refrigerated samples
  // have a hold-time limit, so the duration is required alongside it.
  const [storageCondition, setStorageCondition] = useState("");
  const [storageTimeHours, setStorageTimeHours] = useState("");

  const storageIncomplete =
    !storageCondition || (storageCondition === "Refrigerator" && !storageTimeHours);

  useEffect(() => {
    WaterPreparationService.getSamplingPointsForDepartment(waterDepartmentId).then(setPoints);
  }, [waterDepartmentId]);

  const items: SamplingPointGridItem[] = useMemo(() => {
    return points.map((point) => ({
      id: point.id,
      title: point.code,
      subtitle: point.location || undefined,
      assignedTests: point.assignedTestCodes ?? [],
      disabled: !point.assignedTestCodes || point.assignedTestCodes.length === 0
    }));
  }, [points]);

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
    const waterSamplingPointIds = points.filter((p) => checked[p.id]).map((p) => p.id);

    try {
      await WaterPreparationService.prepare(
        sampleId,
        waterSamplingPointIds,
        storageCondition,
        storageCondition === "Refrigerator" ? Number(storageTimeHours) : null
      );
      onComplete();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare sample.", ok: false });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box>
      {points.length === 0 ? (
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
