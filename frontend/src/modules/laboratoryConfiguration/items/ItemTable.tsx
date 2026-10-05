import { useState, useEffect } from "react";
import {
  Paper,
  Box,
  Typography,
  Stack,
  IconButton,
  Chip,
  Tooltip,
  Skeleton,
  Button,
  TablePagination,
  useTheme
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import BlockIcon from "@mui/icons-material/Block";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import DescriptionIcon from "@mui/icons-material/Description";
import Inventory2OutlinedIcon from "@mui/icons-material/Inventory2Outlined";
import { Item } from "./services/ItemService";
import { CategoryBadge, StatusBadge } from "../../../components/StatusBadge";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { ItemDocumentService } from "./services/ItemDocumentService";

const ALLOWED_ITEM_CATEGORIES = ["FinishedProduct", "RawMaterial", "PackagingMaterial"];

interface ItemTableProps {
  items: Item[];
  selectedItemId: number | null;
  onSelectItem: (item: Item) => void;
  onEdit: (item: Item) => void;
  onDelete: (item: Item) => void;
  onToggleFreeze: (item: Item) => void;
  page: number;
  rowsPerPage: number;
  onPageChange: (page: number) => void;
  onRowsPerPageChange: (rowsPerPage: number) => void;
  loading?: boolean;
  // Compact = the narrow left-hand register shown beside an open item.
  compact?: boolean;
  // Set when filters are hiding items, so the empty state can offer a reset.
  onResetFilters?: () => void;
}

export const ITEM_ROWS_PER_PAGE_OPTIONS = [10, 25, 50];

export function ItemTable({
  items,
  selectedItemId,
  onSelectItem,
  onEdit,
  onDelete,
  onToggleFreeze,
  page,
  rowsPerPage,
  onPageChange,
  onRowsPerPageChange,
  loading = false,
  compact = false,
  onResetFilters,
}: ItemTableProps) {
  const [pendingDelete, setPendingDelete] = useState<Item | null>(null);

  if (loading && items.length === 0) {
    return (
      <Stack spacing={1.5} aria-busy="true" aria-label="Loading items">
        {Array.from({ length: 4 }).map((_, i) => (
          <Skeleton key={i} variant="rounded" height={76} />
        ))}
      </Stack>
    );
  }

  if (items.length === 0) {
    return (
      <Paper
        variant="outlined"
        sx={{ p: 4, textAlign: "center", borderRadius: 1.5, borderStyle: "dashed" }}
      >
        <Inventory2OutlinedIcon sx={{ fontSize: 36, color: "text.disabled", mb: 1 }} />
        <Typography sx={{ fontWeight: 600, fontSize: 14 }}>
          {onResetFilters ? "No items match your filters" : "No items configured yet"}
        </Typography>
        <Typography sx={{ color: "text.secondary", fontSize: 13, mt: 0.5 }}>
          {onResetFilters
            ? "Try a different search term or clear the filters."
            : "Use \u201CAdd Item\u201D to create the first one."}
        </Typography>
        {onResetFilters && (
          <Button size="small" onClick={onResetFilters} sx={{ mt: 1.5, textTransform: "none" }}>
            Clear filters
          </Button>
        )}
      </Paper>
    );
  }

  const pageItems = items.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);

  return (
    <>
      <Stack spacing={compact ? 1 : 1.5} component="ul" sx={{ listStyle: "none", m: 0, p: 0 }}>
        {pageItems.map((item) => {
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

      {items.length > ITEM_ROWS_PER_PAGE_OPTIONS[0] && (
        <TablePagination
          component="div"
          count={items.length}
          page={page}
          rowsPerPage={rowsPerPage}
          rowsPerPageOptions={compact ? [] : ITEM_ROWS_PER_PAGE_OPTIONS}
          labelRowsPerPage="Items per page:"
          onPageChange={(_, newPage) => onPageChange(newPage)}
          onRowsPerPageChange={(e) => onRowsPerPageChange(parseInt(e.target.value, 10))}
          sx={{
            mt: 1.5,
            border: "1px solid",
            borderColor: "divider",
            borderRadius: 1.5,
            bgcolor: "background.paper",
            "& .MuiTablePagination-toolbar": { minHeight: 44, px: compact ? 1 : 2 }
          }}
        />
      )}

      <ConfirmationDialog
        destructive
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
  const specCount = item.specifications?.length ?? 0;

  useEffect(() => {
    ItemDocumentService.getDocumentsForItem(item.id)
      .then((docs) => setDocCount(docs.length))
      .catch(() => setDocCount(0));
  }, [item.id]);

  return (
    <Box component="li">
    <Paper
      role="button"
      tabIndex={0}
      aria-pressed={isSelected}
      aria-label={`${item.name} (${item.code})${item.isActive ? "" : ", frozen"}`}
      onClick={() => onSelectItem(item)}
      onKeyDown={(e) => {
        if (e.target !== e.currentTarget) return;
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onSelectItem(item);
        }
      }}
      sx={{
        overflow: "hidden",
        border: "1px solid",
        borderColor: isSelected ? "primary.main" : "divider",
        borderLeft: "3px solid",
        borderLeftColor: isSelected ? "primary.main" : item.isActive ? "divider" : "text.disabled",
        borderRadius: 1.5,
        transition: "border-color 150ms ease, background-color 150ms ease, box-shadow 150ms ease",
        cursor: "pointer",
        bgcolor: isSelected
          ? theme.custom.status.purple.bg
          : item.isActive
            ? "background.paper"
            : "action.hover",
        boxShadow: isSelected ? "0 0 0 2px rgba(124, 58, 237, 0.2)" : "none",
        "&:hover": {
          borderColor: isSelected ? "primary.main" : "primary.light",
          bgcolor: isSelected ? theme.custom.status.purple.bg : "action.hover",
        },
        "&:focus-visible": {
          outline: "2px solid",
          outlineColor: "primary.main",
          outlineOffset: 2,
        },
      }}
    >
      <Box
        sx={{
          p: 2,
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          gap: 1,
        }}
      >
        <Box sx={{ minWidth: 0 }}>
          <Typography
            sx={{
              fontWeight: 700,
              fontSize: 14,
              color: item.isActive ? "text.primary" : "text.secondary",
              overflowWrap: "anywhere",
            }}
          >
            {item.name}{" "}
            <Typography component="span" sx={{ color: "text.secondary", fontWeight: 400, fontSize: 12 }}>
              ({item.code})
            </Typography>
          </Typography>

          <Stack useFlexGap
            direction="row"
            spacing={0.75}
            sx={{
              alignItems: "center",
              flexWrap: "wrap",
              mt: 0.5
            }}>
            <CategoryBadge category={item.category} />
            <StatusBadge status={item.isActive ? "Active" : "Frozen"} />

            <Chip
              label={`${testCount} ${testCount === 1 ? "Test" : "Tests"}`}
              size="small"
              sx={{
                fontSize: 12,
                fontWeight: 600,
                color: theme.custom.status.purple.text,
                bgcolor: theme.custom.status.purple.bg,
                height: 20,
              }}
            />

            {specCount > 0 && (
              <Chip
                label={`${specCount} ${specCount === 1 ? "Spec" : "Specs"}`}
                size="small"
                variant="outlined"
                color="primary"
                sx={{
                  fontSize: 12,
                  fontWeight: 600,
                  height: 20,
                }}
              />
            )}

            {docCount !== null && (
              <Chip
                icon={<DescriptionIcon style={{ fontSize: 13, color: "inherit" }} />}
                label={`${docCount} ${docCount === 1 ? "Doc" : "Docs"}`}
                size="small"
                variant="outlined"
                sx={{
                  fontSize: 12,
                  fontWeight: 600,
                  height: 20,
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
                  fontSize: "12px",
                  fontWeight: 600,
                  height: 20,
                }}
              />
            )}
          </Stack>
        </Box>

        <Stack
          direction="row"
          spacing={0.5}
          sx={{ flexShrink: 0 }}
          onClick={(e) => e.stopPropagation()}
          onKeyDown={(e) => e.stopPropagation()}
        >
          <Tooltip title="Edit item">
            <IconButton size="small" aria-label={`Edit ${item.name}`} onClick={() => onEdit(item)}>
              <EditIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title={item.isActive ? "Freeze item" : "Unfreeze item"}>
            <IconButton
              size="small"
              aria-label={`${item.isActive ? "Freeze" : "Unfreeze"} ${item.name}`}
              onClick={() => onToggleFreeze(item)}
            >
              {item.isActive ? <BlockIcon fontSize="small" /> : <LockOpenIcon fontSize="small" />}
            </IconButton>
          </Tooltip>
          <Tooltip title="Delete item">
            <IconButton
              size="small"
              color="error"
              aria-label={`Delete ${item.name}`}
              onClick={() => onDelete(item)}
            >
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Stack>
      </Box>
    </Paper>
    </Box>
  );
}
