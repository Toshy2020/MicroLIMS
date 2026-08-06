import { useState } from "react";
import { Paper, Box, Typography, Stack, IconButton } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import BlockIcon from "@mui/icons-material/Block";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import { Item } from "./services/ItemService";
import { CategoryBadge, StatusBadge } from "../../../components/StatusBadge";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";

interface ItemTableProps {
  items: Item[];
  onEdit: (item: Item) => void;
  onDelete: (item: Item) => void;
  onToggleFreeze: (item: Item) => void;
}

export function ItemTable({ items, onEdit, onDelete, onToggleFreeze }: ItemTableProps) {
  const [pendingDelete, setPendingDelete] = useState<Item | null>(null);

  if (items.length === 0) return <Typography sx={{ color: "#9ca3af", fontSize: 13, p: 2 }}>No items configured yet.</Typography>;

  return (
    <>
      <Stack spacing={1.25}>
        {items.map((item) => (
          <Paper key={item.id} sx={{ p: 2, opacity: item.isActive ? 1 : 0.7 }}>
            <Stack direction="row" justifyContent="space-between" alignItems="flex-start">
              <Box>
                <Typography sx={{ fontWeight: 700, fontSize: 14 }}>{item.name} <Typography component="span" sx={{ color: "text.secondary", fontWeight: 400, fontSize: 12 }}>({item.code})</Typography></Typography>
                <Stack direction="row" spacing={0.75} sx={{ mt: 0.75 }}>
                  <CategoryBadge category={item.category} />
                  <StatusBadge status={item.isActive ? "Active" : "Frozen"} />
                </Stack>
              </Box>
              <Stack direction="row" spacing={2} alignItems="center">
                <Box sx={{ textAlign: "right" }}>
                  <Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Assigned Tests</Typography>
                  <Typography sx={{ fontSize: 13 }}>{item.assignedTests.map((t) => t.testCode).join(", ") || "—"}</Typography>
                </Box>
                <Stack direction="row" spacing={0.5}>
                  <IconButton size="small" onClick={() => onEdit(item)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                  <IconButton size="small" onClick={() => onToggleFreeze(item)} title={item.isActive ? "Freeze" : "Unfreeze"}>
                    {item.isActive ? <BlockIcon fontSize="small" /> : <LockOpenIcon fontSize="small" />}
                  </IconButton>
                  <IconButton size="small" color="error" onClick={() => setPendingDelete(item)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                </Stack>
              </Stack>
            </Stack>
          </Paper>
        ))}
      </Stack>

      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? `Delete item "${pendingDelete.name}" (${pendingDelete.code})? This cannot be undone. If it has already been used to receive samples, deletion will be blocked - freeze it instead.` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => { if (pendingDelete) onDelete(pendingDelete); setPendingDelete(null); }}
      />
    </>
  );
}
