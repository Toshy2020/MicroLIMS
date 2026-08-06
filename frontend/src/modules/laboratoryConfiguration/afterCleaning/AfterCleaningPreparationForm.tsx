import { useEffect, useState } from "react";
import { Box, Table, TableHead, TableRow, TableCell, TableBody, Checkbox, Button, Alert, Typography } from "@mui/material";
import { AfterCleaningPreparationService } from "./services/AfterCleaningPreparationService";

interface PartConfig { id: number; testType: string; testCode: string }
interface Part { id: number; name: string; configs?: PartConfig[] }

interface Props {
  sampleId: number;
  machineId: number;
  onComplete: () => void;
}

// One checkbox per machine part - checking a part includes ALL of its
// configured tests in this batch (one TestOrder per distinct TestCode
// across every selected part, not one TestOrder per part).
export function AfterCleaningPreparationForm({ sampleId, machineId, onComplete }: Props) {
  const [parts, setParts] = useState<Part[]>([]);
  const [checked, setChecked] = useState<Record<number, boolean>>({});
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  useEffect(() => {
    (async () => {
      const machineParts: Part[] = await AfterCleaningPreparationService.getPartsForMachine(machineId);
      const withConfigs = await Promise.all(
        machineParts.map(async (p) => ({ ...p, configs: await AfterCleaningPreparationService.getPartConfigurations(p.id) }))
      );
      setParts(withConfigs);
    })();
  }, [machineId]);

  const toggle = (partId: number) => setChecked((c) => ({ ...c, [partId]: !c[partId] }));

  const confirm = async () => {
    setMessage(null);
    const machinePartConfigurationIds = parts
      .filter((p) => checked[p.id])
      .flatMap((p) => p.configs?.map((c) => c.id) ?? []);

    try {
      await AfterCleaningPreparationService.prepare(sampleId, machinePartConfigurationIds);
      onComplete();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare sample.", ok: false });
    }
  };

  return (
    <Box>
      {message && <Alert severity="error" sx={{ mb: 2 }}>{message.text}</Alert>}
      {parts.length > 0 && (
        <Box sx={{ overflowX: "auto" }}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell padding="checkbox" />
                <TableCell>Part</TableCell>
                <TableCell>Assigned Tests</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {parts.map((part) => (
                <TableRow key={part.id} hover>
                  <TableCell padding="checkbox">
                    <Checkbox
                      checked={!!checked[part.id]}
                      disabled={!part.configs || part.configs.length === 0}
                      onChange={() => toggle(part.id)}
                    />
                  </TableCell>
                  <TableCell>{part.name}</TableCell>
                  <TableCell>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {part.configs && part.configs.length > 0
                        ? Array.from(new Set(part.configs.map((c) => c.testCode))).join(", ")
                        : "No tests configured"}
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
