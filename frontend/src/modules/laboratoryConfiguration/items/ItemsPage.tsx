import { useEffect, useState, useMemo } from "react";
import { Box, Button, Alert, Grid, Typography, Stack } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { PageHeader } from "../../../components/PageHeader";
import { ItemTable, ITEM_ROWS_PER_PAGE_OPTIONS } from "./ItemTable";
import { ItemService, Item } from "./services/ItemService";
import { ItemFilterBar } from "./components/ItemFilterBar";
import { AddItemDialog } from "./components/AddItemDialog";
import { ItemWorkspace } from "./components/ItemWorkspace";

export function ItemsPage() {
  const [items, setItems] = useState<Item[]>([]);
  const [selectedItemId, setSelectedItemId] = useState<number | null>(null);
  const [editingItem, setEditingItem] = useState<Item | null>(null);
  const [addDialogOpen, setAddDialogOpen] = useState(false);

  // Search & Filter state
  const [searchQuery, setSearchQuery] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("ALL");
  const [statusFilter, setStatusFilter] = useState("ALL");

  // Pagination
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(ITEM_ROWS_PER_PAGE_OPTIONS[0]);

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const loadItems = () => {
    setLoading(true);
    ItemService.getAll()
      .then((data) => {
        setItems(data);
        setLoadError(null);
      })
      .catch((e: any) => {
        setLoadError(e?.response?.data?.message ?? "Could not load items.");
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadItems();
  }, []);

  // Client-side fast search & filtering
  const filteredItems = useMemo(() => {
    return items.filter((item) => {
      // 1. Search Query (Name, Code, SOP Number)
      if (searchQuery.trim() !== "") {
        const q = searchQuery.toLowerCase().trim();
        const nameMatch = item.name?.toLowerCase().includes(q);
        const codeMatch = item.code?.toLowerCase().includes(q);
        const sopMatch = item.sopNumber?.toLowerCase().includes(q);
        if (!nameMatch && !codeMatch && !sopMatch) return false;
      }

      // 2. Category Filter
      if (categoryFilter !== "ALL" && item.category !== categoryFilter) {
        return false;
      }

      // 3. Status Filter
      if (statusFilter === "Active" && !item.isActive) return false;
      if (statusFilter === "Frozen" && item.isActive) return false;

      return true;
    });
  }, [items, searchQuery, categoryFilter, statusFilter]);

  // A new search or filter starts again from the first page.
  useEffect(() => {
    setPage(0);
  }, [searchQuery, categoryFilter, statusFilter]);

  // Keep the page in range when the list shrinks (delete, reload).
  useEffect(() => {
    const lastPage = Math.max(0, Math.ceil(filteredItems.length / rowsPerPage) - 1);
    if (page > lastPage) setPage(lastPage);
  }, [filteredItems.length, rowsPerPage, page]);

  // When the selection changes, show the page holding the selected item
  // (e.g. a newly created item, which only appears once the reload lands).
  // Cleared once revealed, so later reloads don't pull the user back.
  const [revealItemId, setRevealItemId] = useState<number | null>(null);
  useEffect(() => {
    setRevealItemId(selectedItemId);
  }, [selectedItemId]);
  useEffect(() => {
    if (revealItemId == null) return;
    const index = filteredItems.findIndex((i) => i.id === revealItemId);
    if (index >= 0) {
      setPage(Math.floor(index / rowsPerPage));
      setRevealItemId(null);
    }
  }, [revealItemId, filteredItems, rowsPerPage]);

  const isFiltered = searchQuery.trim() !== "" || categoryFilter !== "ALL" || statusFilter !== "ALL";

  const selectedItem = useMemo(() => {
    return items.find((i) => i.id === selectedItemId) || null;
  }, [items, selectedItemId]);

  const handleResetFilters = () => {
    setSearchQuery("");
    setCategoryFilter("ALL");
    setStatusFilter("ALL");
  };

  const handleOpenAdd = () => {
    setEditingItem(null);
    setAddDialogOpen(true);
  };

  const handleOpenEdit = (item: Item) => {
    setEditingItem(item);
    setAddDialogOpen(true);
  };

  const handleSaveItem = async (itemData: {
    name: string;
    code: string;
    category: string;
    sopNumber: string;
    testCodes: string[];
  }) => {
    setMessage(null);
    const payload = {
      name: itemData.name,
      code: itemData.code,
      category: itemData.category,
      sopNumber: itemData.sopNumber,
      assignedTests: itemData.testCodes.map((tc) => ({ testCode: tc, displayName: tc })),
    };

    if (editingItem) {
      await ItemService.update(editingItem.id, payload, editingItem.version);
      setMessage({ text: `Item "${itemData.name}" updated successfully.`, ok: true });
    } else {
      const created = await ItemService.create(payload);
      setMessage({ text: `Item "${itemData.name}" created successfully.`, ok: true });
      setSelectedItemId(created.id);
    }
    loadItems();
  };

  const handleDeleteItem = async (item: Item) => {
    setMessage(null);
    try {
      await ItemService.remove(item.id);
      setMessage({ text: `Item "${item.name}" deleted.`, ok: true });
      if (selectedItemId === item.id) {
        setSelectedItemId(null);
      }
      loadItems();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not delete item.", ok: false });
    }
  };

  const handleToggleFreezeItem = async (item: Item) => {
    setMessage(null);
    try {
      if (item.isActive) {
        await ItemService.freeze(item.id);
        setMessage({ text: `Item "${item.name}" frozen.`, ok: true });
      } else {
        await ItemService.unfreeze(item.id);
        setMessage({ text: `Item "${item.name}" unfrozen.`, ok: true });
      }
      loadItems();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not update item status.", ok: false });
    }
  };

  return (
    <Box sx={{ pb: 4 }}>
      {/* Top Header with Add Item button */}
      <Box
        sx={{
          display: "flex",
          flexWrap: "wrap",
          gap: 1.5,
          justifyContent: "space-between",
          alignItems: "flex-start",
          mb: 2
        }}
      >
        <PageHeader title="Items" subtitle="Configure which tests are auto-assigned when a sample is received." />
        <Button
          variant="contained"
          color="primary"
          startIcon={<AddIcon />}
          onClick={handleOpenAdd}
          sx={{ textTransform: "none", fontWeight: 700, px: 2.5, py: 1 }}
        >
          Add Item
        </Button>
      </Box>

      {message && (
        <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }} onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      {loadError && (
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          action={
            <Button color="inherit" size="small" onClick={loadItems} disabled={loading}>
              Retry
            </Button>
          }
        >
          {loadError}
        </Alert>
      )}

      {/* Search and Filters Bar */}
      <ItemFilterBar
        searchQuery={searchQuery}
        onSearchChange={setSearchQuery}
        categoryFilter={categoryFilter}
        onCategoryChange={setCategoryFilter}
        statusFilter={statusFilter}
        onStatusChange={setStatusFilter}
        onReset={handleResetFilters}
      />

      {/* Main Content Area: Split Workspace Layout */}
      {loadError && items.length === 0 ? null : selectedItem ? (
        <Grid container spacing={2.5}>
          {/* Left Panel: ~35-40% compact register */}
          <Grid
            size={{
              xs: 12,
              md: 4.5,
              lg: 4
            }}>
            <Stack
              direction="row"
              sx={{
                justifyContent: "space-between",
                alignItems: "center",
                mb: 1
              }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.primary" }}>
                Configured Items ({filteredItems.length}
                {isFiltered && items.length !== filteredItems.length ? ` of ${items.length}` : ""})
              </Typography>
            </Stack>
            <ItemTable
              items={filteredItems}
              selectedItemId={selectedItemId}
              onSelectItem={(item) => setSelectedItemId(item.id)}
              onEdit={handleOpenEdit}
              onDelete={handleDeleteItem}
              onToggleFreeze={handleToggleFreezeItem}
              page={page}
              rowsPerPage={rowsPerPage}
              onPageChange={setPage}
              onRowsPerPageChange={(n) => {
                setRowsPerPage(n);
                setPage(0);
              }}
              loading={loading}
              onResetFilters={isFiltered ? handleResetFilters : undefined}
              compact
            />
          </Grid>

          {/* Right Workspace Panel: ~60-65% detailed workspace */}
          <Grid
            size={{
              xs: 12,
              md: 7.5,
              lg: 8
            }}>
            <ItemWorkspace
              item={selectedItem}
              onClose={() => setSelectedItemId(null)}
              onItemUpdated={loadItems}
            />
          </Grid>
        </Grid>
      ) : (
        /* Full width register when no item is selected */
        <Box sx={{ mt: 1 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.primary" }}>
            Configured Items ({filteredItems.length}
            {isFiltered && items.length !== filteredItems.length ? ` of ${items.length}` : ""})
          </Typography>
          <ItemTable
            items={filteredItems}
            selectedItemId={selectedItemId}
            onSelectItem={(item) => setSelectedItemId(item.id)}
            onEdit={handleOpenEdit}
            onDelete={handleDeleteItem}
            onToggleFreeze={handleToggleFreezeItem}
            page={page}
            rowsPerPage={rowsPerPage}
            onPageChange={setPage}
            onRowsPerPageChange={(n) => {
              setRowsPerPage(n);
              setPage(0);
            }}
            loading={loading}
            onResetFilters={isFiltered ? handleResetFilters : undefined}
          />
        </Box>
      )}

      {/* Add / Edit Item Modal Dialog */}
      <AddItemDialog
        open={addDialogOpen}
        itemToEdit={editingItem}
        onClose={() => setAddDialogOpen(false)}
        onSave={handleSaveItem}
      />
    </Box>
  );
}
