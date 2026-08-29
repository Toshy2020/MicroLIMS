import { useEffect, useMemo, useState } from "react";
import { Box, Typography } from "@mui/material";
import { AfterCleaningPreparationService } from "./services/AfterCleaningPreparationService";
import { SamplingPointGrid, SamplingPointGridItem } from "../../testPreparation/components/SamplingPointGrid";

interface PartConfig { id: number; testType: string; testCode: string }
interface Part { id: number; name: string; configs?: PartConfig[] }

interface Props {
  sampleId: number;
  machineId: number;
  onComplete: () => void;
}

// Grid/card-based selection per machine part - checking a part includes ALL of its
// configured tests in this batch (one TestOrder per distinct TestCode
// across every selected part, not one TestOrder per part).
export function AfterCleaningPreparationForm({ sampleId, machineId, onComplete }: Props) {
  const [parts, setParts] = useState<Part[]>([]);
  const [checked, setChecked] = useState<Record<number, boolean>>({});
  const [loading, setLoading] = useState(false);
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

  const items: SamplingPointGridItem[] = useMemo(() => {
    return parts.map((part) => ({
      id: part.id,
      title: part.name,
      subtitle: "Machine Part",
      assignedTests: Array.from(new Set(part.configs?.map((c) => c.testCode) ?? [])),
      disabled: !part.configs || part.configs.length === 0
    }));
  }, [parts]);

  const toggle = (partId: number) => setChecked((c) => ({ ...c, [partId]: !c[partId] }));

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
    const machinePartConfigurationIds = parts
      .filter((p) => checked[p.id])
      .flatMap((p) => p.configs?.map((c) => c.id) ?? []);

    try {
      await AfterCleaningPreparationService.prepare(sampleId, machinePartConfigurationIds);
      onComplete();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare sample.", ok: false });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box>
      {parts.length === 0 ? (
        <Typography sx={{ fontSize: 13, color: "text.secondary", py: 2 }}>
          Loading machine parts...
        </Typography>
      ) : (
        <SamplingPointGrid
          items={items}
          selectedIds={checked}
          onToggle={toggle}
          onSelectAll={handleSelectAll}
          onConfirm={confirm}
          loading={loading}
          errorMessage={message?.text}
        />
      )}
    </Box>
  );
}
