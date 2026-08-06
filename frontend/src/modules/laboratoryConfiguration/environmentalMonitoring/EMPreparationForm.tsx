import { useEffect, useState } from "react";
import { Box, Table, TableHead, TableRow, TableCell, TableBody, Checkbox, Button, Alert, Typography } from "@mui/material";
import { EMPreparationService } from "./services/EMPreparationService";

interface RoomConfig { id: number; testType: string; testCode: string }
interface Room { id: number; name: string; gradeClassification?: string; configs?: RoomConfig[] }

interface Props {
  sampleId: number;
  departmentId: number;
  onComplete: () => void;
}

// One checkbox per room - checking a room includes ALL of its configured
// tests in this monitoring session's batch (one TestOrder per distinct
// TestCode across every selected room, not one TestOrder per room).
export function EMPreparationForm({ sampleId, departmentId, onComplete }: Props) {
  const [rooms, setRooms] = useState<Room[]>([]);
  const [checked, setChecked] = useState<Record<number, boolean>>({});
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

  const toggle = (roomId: number) => setChecked((c) => ({ ...c, [roomId]: !c[roomId] }));

  const confirm = async () => {
    setMessage(null);
    const roomTestConfigurationIds = rooms
      .filter((r) => checked[r.id])
      .flatMap((r) => r.configs?.map((c) => c.id) ?? []);

    try {
      await EMPreparationService.prepare(sampleId, roomTestConfigurationIds);
      onComplete();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare sample.", ok: false });
    }
  };

  return (
    <Box>
      {message && <Alert severity="error" sx={{ mb: 2 }}>{message.text}</Alert>}
      {rooms.length > 0 && (
        <Box sx={{ overflowX: "auto" }}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell padding="checkbox" />
                <TableCell>Room</TableCell>
                <TableCell>Assigned Tests</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {rooms.map((room) => (
                <TableRow key={room.id} hover>
                  <TableCell padding="checkbox">
                    <Checkbox
                      checked={!!checked[room.id]}
                      disabled={!room.configs || room.configs.length === 0}
                      onChange={() => toggle(room.id)}
                    />
                  </TableCell>
                  <TableCell>
                    {room.name}{room.gradeClassification ? ` (${room.gradeClassification})` : ""}
                  </TableCell>
                  <TableCell>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {room.configs && room.configs.length > 0
                        ? Array.from(new Set(room.configs.map((c) => c.testCode))).join(", ")
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
