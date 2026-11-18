import { useId, useRef, useState } from "react";
import {
  Box,
  Button,
  Chip,
  Divider,
  IconButton,
  Popover,
  Stack,
  Typography,
} from "@mui/material";
import BookmarkBorderIcon from "@mui/icons-material/BookmarkBorder";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import StarIcon from "@mui/icons-material/Star";
import StarBorderIcon from "@mui/icons-material/StarBorder";
import { toast } from "sonner";
import { ConfirmationDialog } from "../../ConfirmationDialog";
import { useTranslation } from "../../../i18n";
import { getErrorMessage } from "../../../utils/errorMessage";
import { FavouriteStar } from "../../../modules/navigation/FavouriteStar";
import { viewRoute } from "../../../modules/navigation/favourites";
import {
  SaveViewDialog,
  type SaveViewMode,
} from "./SaveViewDialog";
import {
  tableViewService,
  type TableViewDto,
} from "./services/tableViewService";
import type { UseTableState } from "./useTableState";

export interface ViewsMenuProps {
  tableKey: string;
  tableState: UseTableState;
}

export function ViewsMenu({ tableKey, tableState }: ViewsMenuProps) {
  const { t } = useTranslation();
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const popoverId = useId();
  const open = Boolean(anchorEl);

  const [saveDialog, setSaveDialog] = useState<{
    open: boolean;
    mode: SaveViewMode;
    initialName: string;
    viewToRename: TableViewDto | null;
  }>({
    open: false,
    mode: "create",
    initialName: "",
    viewToRename: null,
  });

  const [deleteTarget, setDeleteTarget] = useState<TableViewDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [busyId, setBusyId] = useState<number | null>(null);

  const { views, activeView, isViewModified } = tableState;
  const myViews = views.filter((v) => v.scope === "Personal");
  const sharedViews = views.filter((v) => v.scope === "Section");

  const handleClose = () => {
    setAnchorEl(null);
    triggerRef.current?.focus();
  };

  const openSaveDialog = (
    mode: SaveViewMode,
    initialName = "",
    viewToRename: TableViewDto | null = null
  ) => {
    setAnchorEl(null);
    setSaveDialog({ open: true, mode, initialName, viewToRename });
  };

  const handleSaved = async (saved: TableViewDto, mode: SaveViewMode) => {
    setSaveDialog((prev) => ({ ...prev, open: false }));
    await tableState.refreshViews();
    if (mode !== "rename") {
      tableState.applyView(saved);
    }
  };

  const handleSaveChanges = async () => {
    try {
      await tableState.saveViewChanges();
      handleClose();
    } catch (err) {
      toast.error(getErrorMessage(err, t("components.viewsMenu.saveChangesFailed")));
    }
  };

  const handleToggleDefault = async (view: TableViewDto) => {
    setBusyId(view.id);
    try {
      await tableViewService.setDefault(
        tableKey,
        view.isDefault ? null : view.id
      );
      await tableState.refreshViews();
    } catch (err) {
      toast.error(getErrorMessage(err, t("components.viewsMenu.setDefaultFailed")));
    } finally {
      setBusyId(null);
    }
  };

  const handleConfirmDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await tableViewService.remove(deleteTarget.id);
      const wasActive = activeView?.id === deleteTarget.id;
      setDeleteTarget(null);
      if (wasActive) {
        tableState.applyState(tableState.state, null);
      }
      await tableState.refreshViews();
    } catch (err) {
      toast.error(getErrorMessage(err, t("components.viewsMenu.deleteFailed")));
    } finally {
      setDeleting(false);
    }
  };

  const triggerAriaLabel = activeView
    ? isViewModified
      ? t("components.viewsMenu.viewsModifiedAria", { name: activeView.name })
      : t("components.viewsMenu.viewsNamedAria", { name: activeView.name })
    : t("components.viewsMenu.views");

  const renderViewRow = (view: TableViewDto) => {
    const isActive = activeView?.id === view.id;
    const isBusy = busyId === view.id;
    return (
      <Box
        key={view.id}
        role="listitem"
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 0.5,
        }}
      >
        <Button
          size="small"
          variant={isActive ? "contained" : "text"}
          color={isActive ? "primary" : "inherit"}
          onClick={() => {
            tableState.applyView(view);
            handleClose();
          }}
          sx={{
            justifyContent: "flex-start",
            textAlign: "left",
            flex: 1,
            minWidth: 0,
            textTransform: "none",
          }}
        >
          <Box sx={{ minWidth: 0, width: "100%" }}>
            <Typography variant="body2" noWrap sx={{ fontWeight: isActive ? 600 : 400 }}>
              {view.name}
            </Typography>
            {view.scope === "Section" && view.sectionName && (
              <Typography
                variant="caption"
                color={isActive ? "inherit" : "text.secondary"}
                sx={{ display: "block", opacity: isActive ? 0.9 : 1 }}
                noWrap
              >
                {view.sectionName}
              </Typography>
            )}
          </Box>
        </Button>
        <Box sx={{ display: "flex", alignItems: "center", flexShrink: 0 }}>
          {viewRoute(tableKey) !== null && (
            <FavouriteStar
              size="small"
              favourite={{ t: "v", table: tableKey, view: view.id }}
              name={t("components.viewsMenu.favouriteName", { name: view.name })}
            />
          )}
          <IconButton
            size="small"
            disabled={isBusy}
            aria-label={
              view.isDefault
                ? t("components.viewsMenu.clearDefaultAria", { name: view.name })
                : t("components.viewsMenu.setDefaultAria", { name: view.name })
            }
            onClick={() => handleToggleDefault(view)}
            color={view.isDefault ? "primary" : "default"}
          >
            {view.isDefault ? (
              <StarIcon fontSize="small" />
            ) : (
              <StarBorderIcon fontSize="small" />
            )}
          </IconButton>
          {view.canEdit && (
            <>
              <IconButton
                size="small"
                disabled={isBusy}
                aria-label={t("components.viewsMenu.renameAria", { name: view.name })}
                onClick={() => openSaveDialog("rename", view.name, view)}
              >
                <EditOutlinedIcon fontSize="small" />
              </IconButton>
              <IconButton
                size="small"
                disabled={isBusy}
                aria-label={t("components.viewsMenu.deleteAria", { name: view.name })}
                onClick={() => {
                  setAnchorEl(null);
                  setDeleteTarget(view);
                }}
              >
                <DeleteOutlinedIcon fontSize="small" />
              </IconButton>
            </>
          )}
        </Box>
      </Box>
    );
  };

  return (
    <>
      <Button
        ref={triggerRef}
        size="small"
        variant="outlined"
        startIcon={<BookmarkBorderIcon fontSize="small" />}
        aria-label={triggerAriaLabel}
        aria-haspopup="true"
        aria-expanded={open}
        aria-controls={open ? popoverId : undefined}
        onClick={(e) => setAnchorEl(e.currentTarget)}
        sx={{ gap: 0.75 }}
      >
        <span>{activeView ? activeView.name : t("components.viewsMenu.views")}</span>
        {isViewModified && (
          <Chip
            size="small"
            label={t("components.viewsMenu.modified")}
            color="warning"
            variant="outlined"
            sx={{ height: 20, fontSize: "0.7rem" }}
          />
        )}
      </Button>
      <Popover
        id={popoverId}
        open={open}
        anchorEl={anchorEl}
        onClose={handleClose}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
      >
        <Box
          role="region"
          aria-label={t("components.viewsMenu.savedViewsAria")}
          sx={{ p: 1.5, minWidth: 280, maxWidth: 380 }}
        >
          <Typography variant="subtitle2" sx={{ mb: 1 }}>
            {t("components.viewsMenu.myViews")}
          </Typography>
          {myViews.length === 0 ? (
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1 }}>
              {t("components.viewsMenu.noPersonalViews")}
            </Typography>
          ) : (
            <Stack role="list" spacing={0.5} sx={{ mb: 1 }}>
              {myViews.map(renderViewRow)}
            </Stack>
          )}

          <Divider sx={{ my: 1 }} />

          <Typography variant="subtitle2" sx={{ mb: 1 }}>
            {t("components.viewsMenu.sharedViews")}
          </Typography>
          {sharedViews.length === 0 ? (
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1 }}>
              {t("components.viewsMenu.noSharedViews")}
            </Typography>
          ) : (
            <Stack role="list" spacing={0.5} sx={{ mb: 1 }}>
              {sharedViews.map(renderViewRow)}
            </Stack>
          )}

          <Divider sx={{ my: 1 }} />

          <Stack spacing={0.75}>
            {activeView &&
              (isViewModified || tableState.skipped.length > 0) &&
              (activeView.canEdit ? (
                <Button
                  size="small"
                  variant="contained"
                  onClick={handleSaveChanges}
                >
                  {t("components.viewsMenu.saveChanges")}
                </Button>
              ) : (
                <Button
                  size="small"
                  variant="contained"
                  onClick={() =>
                    openSaveDialog("copy", t("components.viewsMenu.copyName", { name: activeView.name }))
                  }
                >
                  {t("components.viewsMenu.saveCopy")}
                </Button>
              ))}
            <Button
              size="small"
              variant="outlined"
              onClick={() => openSaveDialog("create")}
            >
              {t("components.viewsMenu.saveAsNew")}
            </Button>
          </Stack>
        </Box>
      </Popover>

      <SaveViewDialog
        open={saveDialog.open}
        mode={saveDialog.mode}
        tableKey={tableKey}
        state={tableState.state}
        initialName={saveDialog.initialName}
        viewToRename={saveDialog.viewToRename}
        onClose={() => setSaveDialog((prev) => ({ ...prev, open: false }))}
        onSaved={handleSaved}
      />

      <ConfirmationDialog
        open={Boolean(deleteTarget)}
        destructive
        title={t("components.viewsMenu.deleteTitle")}
        message={
          deleteTarget
            ? t("components.viewsMenu.deleteMessage", { name: deleteTarget.name })
            : ""
        }
        confirmText={t("components.viewsMenu.deleteConfirm")}
        cancelText={t("actions.cancel")}
        loading={deleting}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={handleConfirmDelete}
      />
    </>
  );
}
