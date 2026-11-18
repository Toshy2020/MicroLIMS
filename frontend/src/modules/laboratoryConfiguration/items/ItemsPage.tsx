import { useEffect, useState, useMemo, useCallback } from "react";
import { useSearchParams } from "react-router-dom";
import { Box, Button, Alert, Grid, Typography, Stack } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import { useTranslation } from "../../../i18n";
import { PageHeader } from "../../../components/PageHeader";
import { ItemTable, ITEM_ROWS_PER_PAGE_OPTIONS } from "./ItemTable";
import { CopyItemOptions, ItemService, Item, ItemSaveRequest } from "./services/ItemService";
import { isReasonRequiredError } from "../../../services/apiClient";
import { ChangeReasonDialog } from "../../../components/config/ChangeReasonDialog";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { ItemFilterBar } from "./components/ItemFilterBar";
import { noteOwnSave } from "../../../hooks/useRecordChanged";
import { AddItemDialog } from "./components/AddItemDialog";
import { ItemWorkspace, ITEM_WORKSPACE_TABS } from "./components/ItemWorkspace";
import { usePersistedTab } from "../../../hooks/usePersistedTab";
import { UnmappedPreparationConfigurations } from "./components/UnmappedPreparationConfigurations";

type ApiErrorShape = { response?: { data?: { message?: string } } };

function apiErrorMessage(e: unknown, fallback: string): string {
  return (e as ApiErrorShape | null | undefined)?.response?.data?.message ?? fallback;
}

export function ItemsPage() {
  const { t } = useTranslation("labConfig");
  // The QC Manager reaches this page to approve not-applicable tests; creating and changing items needs Items.Manage.
  const canManage = useHasPermission(PERMISSIONS.ITEMS_MANAGE);
  const [items, setItems] = useState<Item[]>([]);
  // The selected item and the workspace tab live in the URL (?item=<id>&tab=<slug>) so a refresh, Back or a shared link restores them.
  const [searchParams, setSearchParams] = useSearchParams();
  const itemParam = searchParams.get("item");
  const selectedItemId = useMemo(() => {
    const id = itemParam === null ? NaN : Number(itemParam);
    return Number.isInteger(id) && id > 0 ? id : null;
  }, [itemParam]);
  // ?tab= wins; without it the tab used last time opens (an unknown or retired value falls back to Overview).
  const [activeTab, handleTabChange] = usePersistedTab("items", ITEM_WORKSPACE_TABS, "tab");

  const setSelectedItemId = useCallback(
    (id: number | null) => {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        if (id === null) {
          next.delete("item");
          next.delete("tab");
        } else {
          next.set("item", String(id));
        }
        return next;
      });
    },
    [setSearchParams]
  );

  const [editingItem, setEditingItem] = useState<Item | null>(null);
  const [addDialogOpen, setAddDialogOpen] = useState(false);
  const [addFromExisting, setAddFromExisting] = useState(false);

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

  type PendingItemReasonAction =
    | { kind: "delete"; item: Item }
    | { kind: "freeze"; item: Item }
    | { kind: "unfreeze"; item: Item }
    | { kind: "edit"; id: number; name: string; payload: ItemSaveRequest; version?: number };
  const [pendingReasonAction, setPendingReasonAction] = useState<PendingItemReasonAction | null>(null);
  const [reasonSubmitting, setReasonSubmitting] = useState(false);
  const [reasonError, setReasonError] = useState<string | null>(null);

  const loadItems = () => {
    setLoading(true);
    ItemService.getAll()
      .then((data) => {
        setItems(data);
        setLoadError(null);
      })
      .catch((e: unknown) => {
        setLoadError(apiErrorMessage(e, t("items.page.loadError")));
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

  // A link to an item that is not in the loaded list (deleted, mistyped) shows the plain list and drops the parameter.
  // Only once loading has finished successfully, so a deep link survives the initial fetch and a failed load.
  useEffect(() => {
    if (loading || loadError !== null || selectedItemId === null) return;
    if (!items.some((i) => i.id === selectedItemId)) {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          next.delete("item");
          next.delete("tab");
          return next;
        },
        { replace: true }
      );
    }
  }, [loading, loadError, items, selectedItemId, setSearchParams]);

  const handleResetFilters = () => {
    setSearchQuery("");
    setCategoryFilter("ALL");
    setStatusFilter("ALL");
  };

  const handleOpenAdd = () => {
    setEditingItem(null);
    setAddFromExisting(false);
    setAddDialogOpen(true);
  };

  const handleOpenAddFromExisting = () => {
    setEditingItem(null);
    setAddFromExisting(true);
    setAddDialogOpen(true);
  };

  const handleOpenEdit = (item: Item) => {
    setEditingItem(item);
    setAddFromExisting(false);
    setAddDialogOpen(true);
  };

  const handleSaveItem = async (itemData: {
    name: string;
    code: string;
    category: string;
    sopNumber: string;
    testCodes: string[];
    minSampleQuantity?: number | null;
    minRetestSampleQuantity?: number | null;
    minSampleQuantityUnit?: string | null;
    copyFrom?: CopyItemOptions | null;
  }) => {
    setMessage(null);
    const basePayload: ItemSaveRequest = {
      name: itemData.name,
      code: itemData.code,
      category: itemData.category,
      sopNumber: itemData.sopNumber,
      assignedTests: itemData.testCodes.map((tc) => ({ testCode: tc, displayName: tc })),
      minSampleQuantity: itemData.minSampleQuantity,
      minRetestSampleQuantity: itemData.minRetestSampleQuantity,
      minSampleQuantityUnit: itemData.minSampleQuantityUnit,
    };

    if (editingItem) {
      try {
        await ItemService.update(editingItem.id, basePayload, editingItem.version);
        noteOwnSave("item", editingItem.id);
        setMessage({ text: t("items.page.messages.updated", { name: itemData.name }), ok: true });
      } catch (e: unknown) {
        if (isReasonRequiredError(e)) {
          setReasonError(null);
          setPendingReasonAction({
            kind: "edit",
            id: editingItem.id,
            name: itemData.name,
            payload: basePayload,
            version: editingItem.version,
          });
          return;
        }
        throw e;
      }
    } else {
      const createPayload: ItemSaveRequest = itemData.copyFrom
        ? { ...basePayload, copyFrom: itemData.copyFrom }
        : basePayload;
      const created = await ItemService.create(createPayload);
      setMessage({ text: t("items.page.messages.created", { name: itemData.name }), ok: true });
      setSelectedItemId(created.id);
    }
    loadItems();
  };

  const handleDeleteItem = (item: Item) => {
    setMessage(null);
    setReasonError(null);
    setPendingReasonAction({ kind: "delete", item });
  };

  const handleToggleFreezeItem = (item: Item) => {
    setMessage(null);
    setReasonError(null);
    setPendingReasonAction({ kind: item.isActive ? "freeze" : "unfreeze", item });
  };

  const handleConfirmReasonAction = async (reason: string) => {
    if (!pendingReasonAction) return;
    setReasonSubmitting(true);
    setReasonError(null);
    try {
      if (pendingReasonAction.kind === "delete") {
        await ItemService.remove(pendingReasonAction.item.id, reason);
        setMessage({ text: t("items.page.messages.deleted", { name: pendingReasonAction.item.name }), ok: true });
        if (selectedItemId === pendingReasonAction.item.id) {
          setSelectedItemId(null);
        }
      } else if (pendingReasonAction.kind === "freeze") {
        await ItemService.freeze(pendingReasonAction.item.id, reason);
        setMessage({ text: t("items.page.messages.frozen", { name: pendingReasonAction.item.name }), ok: true });
      } else if (pendingReasonAction.kind === "unfreeze") {
        await ItemService.unfreeze(pendingReasonAction.item.id, reason);
        setMessage({ text: t("items.page.messages.unfrozen", { name: pendingReasonAction.item.name }), ok: true });
      } else {
        await ItemService.update(
          pendingReasonAction.id,
          pendingReasonAction.payload,
          pendingReasonAction.version,
          reason
        );
        noteOwnSave("item", pendingReasonAction.id);
        setMessage({ text: t("items.page.messages.updated", { name: pendingReasonAction.name }), ok: true });
      }
      setPendingReasonAction(null);
      loadItems();
    } catch (e: unknown) {
      setReasonError(apiErrorMessage(e, t("items.page.actionError")));
    } finally {
      setReasonSubmitting(false);
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
        <PageHeader title={t("items.page.title")} subtitle={t("items.page.subtitle")} />
        {canManage && (
        <Stack direction="row" spacing={1}>
          <Button
            variant="outlined"
            startIcon={<ContentCopyIcon />}
            onClick={handleOpenAddFromExisting}
            sx={{ textTransform: "none", fontWeight: 700 }}
          >
            {t("items.page.newFromExisting")}
          </Button>
          <Button
            variant="contained"
            color="primary"
            startIcon={<AddIcon />}
            onClick={handleOpenAdd}
            sx={{ textTransform: "none", fontWeight: 700, px: 2.5, py: 1 }}
          >
            {t("items.page.addItem")}
          </Button>
        </Stack>
        )}
      </Box>

      {message && (
        <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }} onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      {canManage && <UnmappedPreparationConfigurations onOpenItem={setSelectedItemId} />}

      {loadError && (
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          action={
            <Button color="inherit" size="small" onClick={loadItems} disabled={loading}>
              {t("items.page.retry")}
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
                {isFiltered && items.length !== filteredItems.length
                  ? t("items.page.configuredCountFiltered", { count: filteredItems.length, total: items.length })
                  : t("items.page.configuredCount", { count: filteredItems.length })}
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
              readOnly={!canManage}
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
              activeTab={activeTab}
              onTabChange={handleTabChange}
            />
          </Grid>
        </Grid>
      ) : (
        /* Full width register when no item is selected */
        <Box sx={{ mt: 1 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.primary" }}>
            {isFiltered && items.length !== filteredItems.length
              ? t("items.page.configuredCountFiltered", { count: filteredItems.length, total: items.length })
              : t("items.page.configuredCount", { count: filteredItems.length })}
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
              readOnly={!canManage}
          />
        </Box>
      )}

      {/* Add / Edit Item Modal Dialog */}
      <AddItemDialog
        open={addDialogOpen}
        itemToEdit={editingItem}
        onItemReloaded={setEditingItem}
        existingItems={items}
        startFromExisting={addFromExisting}
        onClose={() => {
          setAddDialogOpen(false);
          setAddFromExisting(false);
        }}
        onSave={handleSaveItem}
      />

      <ChangeReasonDialog
        open={pendingReasonAction !== null}
        title={
          pendingReasonAction?.kind === "delete"
            ? t("items.page.reasonDialog.deleteTitle", { name: pendingReasonAction.item.name })
            : pendingReasonAction?.kind === "freeze"
            ? t("items.page.reasonDialog.freezeTitle", { name: pendingReasonAction.item.name })
            : pendingReasonAction?.kind === "unfreeze"
            ? t("items.page.reasonDialog.unfreezeTitle", { name: pendingReasonAction.item.name })
            : pendingReasonAction
            ? t("items.page.reasonDialog.editTitle", { name: pendingReasonAction.name })
            : t("items.page.reasonDialog.defaultTitle")
        }
        message={
          pendingReasonAction?.kind === "delete"
            ? t("items.page.reasonDialog.deleteMessage")
            : pendingReasonAction?.kind === "freeze"
            ? t("items.page.reasonDialog.freezeMessage")
            : pendingReasonAction?.kind === "unfreeze"
            ? t("items.page.reasonDialog.unfreezeMessage")
            : t("items.page.reasonDialog.editMessage")
        }
        confirmText={
          pendingReasonAction?.kind === "delete"
            ? t("items.page.reasonDialog.confirmDelete")
            : pendingReasonAction?.kind === "freeze"
            ? t("items.page.reasonDialog.confirmFreeze")
            : pendingReasonAction?.kind === "unfreeze"
            ? t("items.page.reasonDialog.confirmUnfreeze")
            : t("items.page.reasonDialog.confirmEdit")
        }
        loading={reasonSubmitting}
        error={reasonError}
        onCancel={() => { setPendingReasonAction(null); setReasonError(null); }}
        onConfirm={handleConfirmReasonAction}
      />
    </Box>
  );
}
