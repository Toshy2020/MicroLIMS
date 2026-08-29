import { useEffect, useMemo, useState } from "react";
import { Box, Typography } from "@mui/material";
import { EMPreparationService } from "./services/EMPreparationService";
import { SamplingPointGrid, SamplingPointGridItem } from "../../testPreparation/components/SamplingPointGrid";

interface RoomConfig { id: number; testType: string; testCode: string }
interface Room { id: number; name: string; gradeClassification?: string; configs?: RoomConfig[] }

interface Props {
  sampleId: number;
  departmentId: number;
  onComplete: () => void;
}

// Grid/card-based selection per room - checking a room includes ALL of its configured
// tests in this monitoring session's batch (one TestOrder per distinct
// TestCode across every selected room, not one TestOrder per room).
export function EMPreparationForm({ sampleId, departmentId, onComplete }: Props) {
  const [rooms, setRooms] = useState<Room[]>([]);
  const [checked, setChecked] = useState<Record<number, boolean>>({});
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  useEffect(() => {
    (async () => {
      const departmentRooms: Room[] = await EMPreparationService.getRoomsForDepartment(departmentId);
      const withConfigs = await Promise.all(
        departmentRooms.map(async (r) => ({ ...r, configs: await EMPreparationService.getRoomTestConfigurations(r.id) }))
      );
      setRooms(withConfigs);
    })();
  }, [departmentId]);

  const items: SamplingPointGridItem[] = useMemo(() => {
    return rooms.map((room) => ({
      id: room.id,
      title: room.name,
      subtitle: room.gradeClassification ? `Grade ${room.gradeClassification.replace(/^Grade\s*/i, "")}` : undefined,
      assignedTests: Array.from(new Set(room.configs?.map((c) => c.testCode) ?? [])),
      disabled: !room.configs || room.configs.length === 0
    }));
  }, [rooms]);

  const toggle = (roomId: number) => setChecked((c) => ({ ...c, [roomId]: !c[roomId] }));

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
    const roomTestConfigurationIds = rooms
      .filter((r) => checked[r.id])
      .flatMap((r) => r.configs?.map((c) => c.id) ?? []);

    try {
      await EMPreparationService.prepare(sampleId, roomTestConfigurationIds);
      onComplete();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare sample.", ok: false });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box>
      {rooms.length === 0 ? (
        <Typography sx={{ fontSize: 13, color: "text.secondary", py: 2 }}>
          Loading rooms...
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
