import { useState, useEffect } from "react";
import { Paper, Box, Typography, Stack, IconButton, Chip, useTheme } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import BlockIcon from "@mui/icons-material/Block";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import DescriptionIcon from "@mui/icons-material/Description";
import { Item } from "./services/ItemService";
import { CategoryBadge, StatusBadge } from "../../../components/StatusBadge";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { ItemDocumentService, ItemDocumentDto } from "./services/ItemDocumentService";

const ALLOWED_ITEM_CATEGORIES = ["FinishedProduct", "RawMaterial", "PackagingMaterial"];

interface ItemTableProps {
  items: Item[];
  selectedItemId: number | null;
  onSelectItem: (item: Item) => void;
  onEdit: (item: Item) => void;
  onDelete: (item: Item) => void;
  onToggleFreeze: (item: Item) => void;
}

export function ItemTable({
  items,
  selectedItemId,
  onSelectItem,
  onEdit,
  onDelete,
  onToggleFreeze,
}: ItemTableProps) {
  const theme = useTheme();
  const [pendingDelete, setPendingDelete] = useState<Item | null>(null);

  if (items.length === 0)
    return <Typography sx={{ color: "text.secondary", fontSize: 13, p: 2 }}>No matching items found.</Typography>;

  return (
    <>
      <Stack spacing={1.5}>
        {items.map((item) => {
          const isSelected = selectedItemId === item.id;
          const isLegacyCategory = !ALLOWED_ITEM_CATEGORIES.includes(item.category);
          const testCount = item.assignedTests?.length ?? 0;

          return (
            <ItemRowCard
              key={item.id}
              item={item}
              isSelected={isSelected}
              isLegacyCategory={isLegacyCategory}
              testCount={testCount}
              onSelectItem={onSelectItem}
              onEdit={onEdit}
              onToggleFreeze={onToggleFreeze}
              onDelete={(item) => setPendingDelete(item)}
            />
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

function ItemRowCard({
  item,
  isSelected,
  isLegacyCategory,
  testCount,
  onSelectItem,
  onEdit,
  onToggleFreeze,
  onDelete,
}: {
  item: Item;
  isSelected: boolean;
  isLegacyCategory: boolean;
  testCount: number;
  onSelectItem: (item: Item) => void;
  onEdit: (item: Item) => void;
  onToggleFreeze: (item: Item) => void;
  onDelete: (item: Item) => void;
}) {
  const theme = useTheme();
  const [docCount, setDocCount] = useState<number | null>(null);

  useEffect(() => {
    ItemDocumentService.getDocumentsForItem(item.id)
      .then((docs) => setDocCount(docs.length))
      .catch(() => setDocCount(0));
  }, [item.id]);

  return (
    <Paper
      onClick={() => onSelectItem(item)}
      sx={{
        overflow: "hidden",
        border: "1px solid",
        borderColor: isSelected ? "primary.main" : "divider",
        borderRadius: 1.5,
        opacity: item.isActive ? 1 : 0.75,
        transition: "all 0.15s ease-in-out",
        cursor: "pointer",
        bgcolor: isSelected ? theme.custom.status.purple.bg : "background.paper",
        boxShadow: isSelected ? "0 0 0 2px rgba(124, 58, 237, 0.2)" : "none",
        "&:hover": {
          borderColor: isSelected ? "primary.main" : "primary.light",
          bgcolor: isSelected ? theme.custom.status.purple.bg : "action.hover",
        },
      }}
    >
      <Box
        sx={{
          p: 2,
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
        }}
      >
        <Box>
          <Typography sx={{ fontWeight: 700, fontSize: 14, color: "text.primary" }}>
            {item.name}{" "}
            <Typography component="span" sx={{ color: "text.secondary", fontWeight: 400, fontSize: 12 }}>
              ({item.code})
            </Typography>
          </Typography>

          <Stack direction="row" spacing={0.75} alignItems="center" flexWrap="wrap" sx={{ mt: 0.5 }}>
            <CategoryBadge category={item.category} />
            <StatusBadge status={item.isActive ? "Active" : "Frozen"} />

            <Chip
              label={`${testCount} ${testCount === 1 ? "Test" : "Tests"}`}
              size="small"
              sx={{
                fontSize: 11,
                fontWeight: 600,
                color: theme.custom.status.purple.text,
                bgcolor: theme.custom.status.purple.bg,
                height: 20,
              }}
            />

            {docCount !== null && (
              <Chip
                icon={<DescriptionIcon style={{ fontSize: 13, color: "inherit" }} />}
                label={`${docCount} ${docCount === 1 ? "Doc" : "Docs"}`}
                size="small"
                variant="outlined"
                sx={{
                  fontSize: 11,
                  fontWeight: 600,
                  height: 20,
                  cursor: "pointer",
                }}
              />
            )}

            {isLegacyCategory && (
              <Chip
                label="⚠ Legacy category"
                size="small"
                sx={{
                  backgroundColor: theme.custom.status.inconclusive.bg,
                  color: theme.custom.status.inconclusive.text,
                  fontSize: "11px",
                  fontWeight: 600,
                  height: 20,
                }}
              />
            )}
          </Stack>
        </Box>

        <Stack direction="row" spacing={0.5} onClick={(e) => e.stopPropagation()}>
          <IconButton
            size="small"
            onClick={(e) => {
              e.stopPropagation();
              onEdit(item);
            }}
            title="Edit Item"
          >
            <EditIcon fontSize="small" />
          </IconButton>
          <IconButton
            size="small"
            onClick={(e) => {
              e.stopPropagation();
              onToggleFreeze(item);
            }}
            title={item.isActive ? "Freeze Item" : "Unfreeze Item"}
          >
            {item.isActive ? <BlockIcon fontSize="small" /> : <LockOpenIcon fontSize="small" />}
          </IconButton>
          <IconButton
            size="small"
            color="error"
            onClick={(e) => {
              e.stopPropagation();
              onDelete(item);
            }}
            title="Delete Item"
          >
            <DeleteIcon fontSize="small" />
          </IconButton>
        </Stack>
      </Box>
    </Paper>
  );
}
