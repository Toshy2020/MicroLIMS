import { useEffect, useState } from "react";
import { Box, Table, TableHead, TableRow, TableCell, TableBody, Checkbox, Button, Alert, Typography } from "@mui/material";
import { WaterPreparationService } from "./services/WaterPreparationService";

interface SamplingPoint { id: number; code: string; location: string; assignedTestCodes: string[] }

interface Props {
  sampleId: number;
  waterDepartmentId: number;
  onComplete: () => void;
}

// One checkbox per sampling point - checking a point includes ALL of its
// assigned tests in this batch (one TestOrder per distinct TestCode
// across every selected point, not one TestOrder per point). Mirrors
// EMPreparationForm's Room checklist.
export function WaterPreparationForm({ sampleId, waterDepartmentId, onComplete }: Props) {
  const [points, setPoints] = useState<SamplingPoint[]>([]);
  const [checked, setChecked] = useState<Record<number, boolean>>({});
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  useEffect(() => {
    WaterPreparationService.getSamplingPointsForDepartment(waterDepartmentId).then(setPoints);
  }, [waterDepartmentId]);

  const toggle = (pointId: number) => setChecked((c) => ({ ...c, [pointId]: !c[pointId] }));

  const confirm = async () => {
    setMessage(null);
    const waterSamplingPointIds = points.filter((p) => checked[p.id]).map((p) => p.id);

    try {
      await WaterPreparationService.prepare(sampleId, waterSamplingPointIds);
      onComplete();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare sample.", ok: false });
    }
  };

  return (
    <Box>
      {message && <Alert severity="error" sx={{ mb: 2 }}>{message.text}</Alert>}
      {points.length > 0 && (
        <Box sx={{ overflowX: "auto" }}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell padding="checkbox" />
                <TableCell>Sampling Point</TableCell>
                <TableCell>Assigned Tests</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {points.map((point) => (
                <TableRow key={point.id} hover>
                  <TableCell padding="checkbox">
                    <Checkbox
                      checked={!!checked[point.id]}
                      disabled={!point.assignedTestCodes || point.assignedTestCodes.length === 0}
                      onChange={() => toggle(point.id)}
                    />
                  </TableCell>
                  <TableCell>
                    {point.code}{point.location ? ` (${point.location})` : ""}
                  </TableCell>
                  <TableCell>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {point.assignedTestCodes && point.assignedTestCodes.length > 0
                        ? point.assignedTestCodes.join(", ")
                        : "No tests assigned"}
                    </Typography>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 2 }}>
            <Button variant="contained" onClick={confirm}>Start Testing</Button>
          </Box>
        </Box>
      )}
    </Box>
  );
}
