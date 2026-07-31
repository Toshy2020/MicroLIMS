import { useEffect, useState } from "react";
import { Paper, Box, Typography, Stack } from "@mui/material";
import { ItemService, Item } from "./services/ItemService";
import { CategoryBadge } from "../../../components/StatusBadge";

export function ItemTable() {
  const [items, setItems] = useState<Item[]>([]);
  useEffect(() => { ItemService.getAll().then(setItems); }, []);

  if (items.length === 0) return <Typography sx={{ color: "#9ca3af", fontSize: 13, p: 2 }}>No items configured yet.</Typography>;

  return (
    <Stack spacing={1.25}>
      {items.map((item) => (
        <Paper key={item.id} sx={{ p: 2 }}>
          <Stack direction="row" justifyContent="space-between" alignItems="flex-start">
            <Box>
              <Typography sx={{ fontWeight: 700, fontSize: 14 }}>{item.name} <Typography component="span" sx={{ color: "text.secondary", fontWeight: 400, fontSize: 12 }}>({item.code})</Typography></Typography>
              <Box sx={{ mt: 0.75 }}><CategoryBadge category={item.category} /></Box>
            </Box>
            <Box sx={{ textAlign: "right" }}>
              <Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Assigned Tests</Typography>
              <Typography sx={{ fontSize: 13 }}>{item.assignedTests.map((t) => t.testCode).join(", ") || "—"}</Typography>
            </Box>
          </Stack>
        </Paper>
      ))}
    </Stack>
  );
}
