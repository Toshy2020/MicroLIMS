import { useState } from "react";
import { Paper, Box, Typography, Stack, IconButton, Collapse, Divider, Chip } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import BlockIcon from "@mui/icons-material/Block";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import { Item } from "./services/ItemService";
import { CategoryBadge, StatusBadge } from "../../../components/StatusBadge";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { ItemSpecificationsSection } from "./components/ItemSpecificationsSection";

const ALLOWED_ITEM_CATEGORIES = ["FinishedProduct", "RawMaterial", "PackagingMaterial"];

interface ItemTableProps {
  items: Item[];
  onEdit: (item: Item) => void;
  onDelete: (item: Item) => void;
  onToggleFreeze: (item: Item) => void;
  onSpecsChanged?: () => void;
}

export function ItemTable({ items, onEdit, onDelete, onToggleFreeze, onSpecsChanged }: ItemTableProps) {
  const [expandedItemId, setExpandedItemId] = useState<number | null>(null);
  const [pendingDelete, setPendingDelete] = useState<Item | null>(null);

  const toggleExpand = (itemId: number) => {
    setExpandedItemId((prev) => (prev === itemId ? null : itemId));
  };

  if (items.length === 0)
    return <Typography sx={{ color: "#9ca3af", fontSize: 13, p: 2 }}>No items configured yet.</Typography>;

  return (
    <>
      <Stack spacing={1.5}>
        {items.map((item) => {
          const isExpanded = expandedItemId === item.id;
          const specCount = item.specifications?.length ?? 0;
          const isLegacyCategory = !ALLOWED_ITEM_CATEGORIES.includes(item.category);

          return (
            <Paper
              key={item.id}
              sx={{
                overflow: "hidden",
                border: "1px solid #e5e7eb",
                borderRadius: 1.5,
                opacity: item.isActive ? 1 : 0.75,
                transition: "all 0.15s ease-in-out"
              }}
            >
              {/* Item Card Header / Summary Row */}
              <Box
                onClick={() => toggleExpand(item.id)}
                sx={{
                  p: 2,
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  cursor: "pointer",
                  bgcolor: isExpanded ? "#faf5ff" : "#ffffff",
                  "&:hover": { bgcolor: isExpanded ? "#faf5ff" : "#f9fafb" }
                }}
              >
                <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
                  <IconButton
                    size="small"
                    onClick={(e) => {
                      e.stopPropagation();
                      toggleExpand(item.id);
                    }}
                    title={isExpanded ? "Collapse Specifications" : "Expand Specifications"}
                    sx={{ color: isExpanded ? "#7b1fa2" : "#6b7280" }}
                  >
                    {isExpanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}
                  </IconButton>

                  <Box>
                    <Typography sx={{ fontWeight: 700, fontSize: 14, color: "#1f2937" }}>
                      {item.name}{" "}
                      <Typography component="span" sx={{ color: "text.secondary", fontWeight: 400, fontSize: 12 }}>
                        ({item.code})
                      </Typography>
                    </Typography>

                    <Stack direction="row" spacing={0.75} alignItems="center" flexWrap="wrap" sx={{ mt: 0.5 }}>
                      <CategoryBadge category={item.category} />
                      <StatusBadge status={item.isActive ? "Active" : "Frozen"} />
                      {specCount > 0 && (
                        <Typography
                          component="span"
                          sx={{
                            fontSize: 11,
                            fontWeight: 600,
                            color: "#7b1fa2",
                            bgcolor: "#f3e8ff",
                            px: 0.75,
                            py: 0.25,
                            borderRadius: 1
                          }}
                        >
                          {specCount} {specCount === 1 ? "spec" : "specs"}
                        </Typography>
                      )}
                      {isLegacyCategory && (
                        <Chip
                          label="⚠ Legacy category — managed via dedicated configuration page"
                          size="small"
                          sx={{
                            backgroundColor: "#fef3c7",
                            color: "#92400e",
                            fontSize: "11px",
                            fontWeight: 600,
                            height: 20
                          }}
                        />
                      )}
                    </Stack>
                  </Box>
                </Box>

                <Stack direction="row" spacing={2} alignItems="center">
                  <Box sx={{ textAlign: "right", display: { xs: "none", sm: "block" } }}>
                    <Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Assigned Tests</Typography>
                    <Typography sx={{ fontSize: 13, fontWeight: 500 }}>
                      {item.assignedTests.map((t) => t.testCode).join(", ") || "—"}
                    </Typography>
                  </Box>

                  <Stack direction="row" spacing={0.5} onClick={(e) => e.stopPropagation()}>
                    <IconButton size="small" onClick={() => onEdit(item)} title="Edit">
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton
                      size="small"
                      onClick={() => onToggleFreeze(item)}
                      title={item.isActive ? "Freeze" : "Unfreeze"}
                    >
                      {item.isActive ? <BlockIcon fontSize="small" /> : <LockOpenIcon fontSize="small" />}
                    </IconButton>
                    <IconButton
                      size="small"
                      color="error"
                      onClick={() => setPendingDelete(item)}
                      title="Delete"
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Stack>
                </Stack>
              </Box>

              {/* Collapsible Inline Specifications Section */}
              <Collapse in={isExpanded} unmountOnExit>
                <Divider />
                <Box sx={{ p: 2.5, bgcolor: "#faf9fc" }}>
                  <ItemSpecificationsSection
                    item={item}
                    onSpecsChanged={() => onSpecsChanged?.()}
                  />
                </Box>
              </Collapse>
            </Paper>
          );
        })}
      </Stack>

      <ConfirmationDialog
        open={pendingDelete != null}
        message={
          pendingDelete
            ? `Delete item "${pendingDelete.name}" (${pendingDelete.code})? This cannot be undone. If it has already been used to receive samples, deletion will be blocked - freeze it instead.`
            : ""
        }
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => {
          if (pendingDelete) onDelete(pendingDelete);
          setPendingDelete(null);
        }}
      />
    </>
  );
}
