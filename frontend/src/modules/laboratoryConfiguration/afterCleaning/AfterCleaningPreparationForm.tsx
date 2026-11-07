import { useEffect, useMemo, useState } from "react";
import { Box, Typography } from "@mui/material";
import { MonitoringChecklist } from "../../monitoringPlan/components/MonitoringChecklist";
import { AfterCleaningPreparationService } from "./services/AfterCleaningPreparationService";
import { SamplingPointGrid, SamplingPointGridItem } from "../../testPreparation/components/SamplingPointGrid";
import { useTestDefinitions } from "../../../hooks/useTestDefinitions";
import { isTestCodeInLab } from "../water/WaterPreparationForm";
import { cleaningTechniqueOf } from "./cleaningTechniques";

interface PartConfig { id: number; testType: string; testCode: string }
interface Part { id: number; name: string; configs?: PartConfig[] }

interface Props {
  sampleId: number;
  machineId: number;
  labSectionId?: number | null;
  cleaningTechniques?: string[] | null;
  onComplete: () => void;
}

// Grid/card-based selection per machine part - checking a part includes ALL of its
// configured tests in this batch (one TestOrder per distinct TestCode
// across every selected part, not one TestOrder per part).
export function AfterCleaningPreparationForm({ sampleId, machineId, labSectionId, cleaningTechniques, onComplete }: Props) {
  const [parts, setParts] = useState<Part[]>([]);
  const [loadingParts, setLoadingParts] = useState(true);
  const [checked, setChecked] = useState<Record<number, boolean>>({});
  const [monitoringIds, setMonitoringIds] = useState<number[]>([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const { options: testDefinitions, loading: testDefsLoading } = useTestDefinitions();

  useEffect(() => {
    let cancelled = false;
    setLoadingParts(true);
    (async () => {
      try {
        const machineParts: Part[] = await AfterCleaningPreparationService.getPartsForMachine(machineId);
        const withConfigs = await Promise.all(
          machineParts.map(async (p) => ({
            ...p,
            configs: await AfterCleaningPreparationService.getPartConfigurations(p.id)
          }))
        );
        if (!cancelled) {
          setParts(withConfigs);
        }
      } catch {
        if (!cancelled) setParts([]);
      } finally {
        if (!cancelled) setLoadingParts(false);
      }
    })();
    return () => { cancelled = true; };
  }, [machineId]);

  const filteredParts = useMemo(() => {
    return parts.map((part) => ({
      ...part,
      configs: (part.configs ?? []).filter((c) => {
        if (!isTestCodeInLab(c.testCode, labSectionId, testDefinitions)) {
          return false;
        }
        if (cleaningTechniques !== undefined && cleaningTechniques !== null) {
          const def = testDefinitions.find((t) => t.code === c.testCode);
          const tech = cleaningTechniqueOf(def?.workflowType);
          if (!cleaningTechniques.includes(tech)) {
            return false;
          }
        }
        return true;
      })
    }));
  }, [parts, testDefinitions, labSectionId, cleaningTechniques]);

  const items: SamplingPointGridItem[] = useMemo(() => {
    return filteredParts.map((part) => ({
      id: part.id,
      title: part.name,
      subtitle: "Machine Part",
      assignedTests: Array.from(new Set(part.configs?.map((c) => c.testCode) ?? [])),
      disabled: !part.configs || part.configs.length === 0
    }));
  }, [filteredParts]);

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
    const machinePartConfigurationIds = filteredParts
      .filter((p) => checked[p.id])
      .flatMap((p) => p.configs?.map((c) => c.id) ?? []);

    try {
      await AfterCleaningPreparationService.prepare(sampleId, machinePartConfigurationIds, monitoringIds);
      onComplete();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare sample.", ok: false });
    } finally {
      setLoading(false);
    }
  };

  const isStillLoading = loadingParts || (testDefsLoading && testDefinitions.length === 0);

  return (
    <Box>
      <MonitoringChecklist sampleId={sampleId} onChange={setMonitoringIds} />
      {isStillLoading ? (
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
