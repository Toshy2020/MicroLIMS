import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import axios from "axios";
import {
  Alert,
  Box,
  Button,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Tab,
  Tabs,
  Typography
} from "@mui/material";
import DoneAllIcon from "@mui/icons-material/DoneAll";
import { toast } from "sonner";
import { EmptyState, FilterBar, LabPage, RegisterTable } from "../../components/lab";
import type { RegisterColumn } from "../../components/lab";
import { MetaBadge, ToneBadge } from "../../components/StatusBadge";
import type { StatusTone } from "../../theme/statusTokens";
import { getErrorMessage } from "../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import { NOTIFICATION_TYPE_OPTIONS, resolveNotificationTarget } from "./notificationRoutes";
import {
  notificationService,
  type NotificationDto,
  type NotificationStateFilter
} from "./services/notificationService";

const PAGE_SIZE = 25;

const STATE_TABS: readonly { value: NotificationStateFilter; label: string }[] = [
  { value: "all", label: "All" },
  { value: "unread", label: "Unread" },
  { value: "read", label: "Read" },
  { value: "done", label: "Done" }
];

function severityTone(severity: string): StatusTone {
  switch (severity.toLowerCase()) {
    case "error":
      return "detected";
    case "warning":
      return "inconclusive";
    default:
      return "info";
  }
}

function severityLabel(severity: string): string {
  switch (severity.toLowerCase()) {
    case "error":
      return "Error";
    case "warning":
      return "Warning";
    default:
      return "Info";
  }
}

export function NotificationsPage() {
  const navigate = useNavigate();

  const [stateFilter, setStateFilter] = useState<NotificationStateFilter>("all");
  const [typeFilter, setTypeFilter] = useState<string>("");
  const [searchQuery, setSearchQuery] = useState<string>("");
  const [page, setPage] = useState<number>(0);
  const [reloadSeq, setReloadSeq] = useState<number>(0);

  const [items, setItems] = useState<NotificationDto[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [markingAllRead, setMarkingAllRead] = useState<boolean>(false);
  const [busyRowId, setBusyRowId] = useState<number | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);

    notificationService
      .list(
        {
          state: stateFilter,
          type: typeFilter || undefined,
          q: searchQuery.trim() || undefined,
          page: page + 1,
          pageSize: PAGE_SIZE
        },
        controller.signal
      )
      .then((result) => {
        if (controller.signal.aborted) return;
        setItems(result.items);
        setTotalCount(result.totalCount);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted || axios.isCancel(err)) return;
        const message = getErrorMessage(err, "Could not load notifications.");
        setError(message);
        toast.error(message);
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      });

    return () => {
      controller.abort();
    };
  }, [stateFilter, typeFilter, searchQuery, page, reloadSeq]);

  const handleStateChange = (_: unknown, next: NotificationStateFilter) => {
    setStateFilter(next);
    setPage(0);
  };

  const handleTypeChange = (nextType: string) => {
    setTypeFilter(nextType);
    setPage(0);
  };

  const handleSearch = useCallback((nextQuery: string) => {
    setSearchQuery(nextQuery);
    setPage(0);
  }, []);

  const handleRefresh = useCallback(() => {
    setReloadSeq((s) => s + 1);
  }, []);

  const clearFilters = () => {
    setStateFilter("all");
    setTypeFilter("");
    setSearchQuery("");
    setPage(0);
  };

  const handleMarkAllRead = async () => {
    if (markingAllRead) return;
    setMarkingAllRead(true);
    try {
      await notificationService.markAllRead();
      toast.success("All notifications marked as read.");
      setReloadSeq((s) => s + 1);
    } catch (err: unknown) {
      toast.error(getErrorMessage(err, "Could not mark all notifications as read."));
    } finally {
      setMarkingAllRead(false);
    }
  };

  const handleRowClick = async (row: NotificationDto) => {
    if (busyRowId === row.id) return;
    if (!row.readAt) {
      setBusyRowId(row.id);
      try {
        await notificationService.markRead(row.id);
        const nowIso = new Date().toISOString();
        setItems((prev) =>
          stateFilter === "unread"
            ? prev.filter((item) => item.id !== row.id)
            : prev.map((item) => (item.id === row.id ? { ...item, readAt: nowIso } : item))
        );
      } catch (err: unknown) {
        toast.error(getErrorMessage(err, "Could not mark notification as read."));
        setBusyRowId(null);
        return;
      }
      setBusyRowId(null);
    }

    const target = resolveNotificationTarget(row);
    if (target) {
      navigate(target);
    }
  };

  const columns = useMemo<RegisterColumn<NotificationDto>[]>(
    () => [
      {
        key: "severity",
        label: "Severity",
        width: 110,
        render: (row) => <ToneBadge tone={severityTone(row.severity)} label={severityLabel(row.severity)} />
      },
      {
        key: "title",
        label: "Title",
        render: (row) => {
          const isUnread = !row.readAt && !row.resolvedAt;
          return (
            <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
              <Typography variant="body2" sx={{ fontWeight: isUnread ? 700 : 500 }}>
                {row.title}
              </Typography>
              {row.resolvedAt && <MetaBadge label="Done" />}
            </Stack>
          );
        }
      },
      {
        key: "message",
        label: "Message",
        render: (row) => (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {row.message}
          </Typography>
        )
      },
      {
        key: "sectionName",
        label: "Section",
        width: 180,
        render: (row) => row.sectionName ?? "—"
      },
      {
        key: "createdAt",
        label: "Created",
        nowrap: true,
        width: 210,
        render: (row) => (
          <Box component="span" sx={{ fontVariantNumeric: "tabular-nums", whiteSpace: "nowrap" }}>
            {formatDateTimeWithTimeZone(row.createdAt)}
          </Box>
        )
      }
    ],
    []
  );

  const hasFilters = stateFilter !== "all" || typeFilter !== "" || searchQuery.trim() !== "";

  return (
    <LabPage
      title="Notifications"
      subtitle="Alerts, assignments, escalations, and reminders across your sections."
      actions={
        <Button
          variant="outlined"
          startIcon={<DoneAllIcon />}
          onClick={() => void handleMarkAllRead()}
          disabled={loading || markingAllRead}
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          {markingAllRead ? "Marking all read..." : "Mark all read"}
        </Button>
      }
      filters={
        <Stack spacing={1.5}>
          <Box sx={{ borderBottom: 1, borderColor: "divider" }}>
            <Tabs
              value={stateFilter}
              onChange={handleStateChange}
              aria-label="Notification state"
            >
              {STATE_TABS.map((t) => (
                <Tab key={t.value} value={t.value} label={t.label} sx={{ textTransform: "none", fontWeight: 600 }} />
              ))}
            </Tabs>
          </Box>
          <FilterBar
            search={searchQuery}
            onSearch={handleSearch}
            placeholder="Search notifications"
            resultCount={loading || error ? undefined : totalCount}
            onRefresh={handleRefresh}
            refreshing={loading}
          >
            <FormControl size="small" sx={{ minWidth: 230 }}>
              <InputLabel id="notification-type-filter-label">Type</InputLabel>
              <Select
                labelId="notification-type-filter-label"
                label="Type"
                value={typeFilter}
                onChange={(e) => handleTypeChange(String(e.target.value))}
              >
                <MenuItem value="">All types</MenuItem>
                {NOTIFICATION_TYPE_OPTIONS.map((opt) => (
                  <MenuItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </FilterBar>
        </Stack>
      }
    >
      {error && (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={handleRefresh} sx={{ textTransform: "none", fontWeight: 600 }}>
              Retry
            </Button>
          }
        >
          {error}
        </Alert>
      )}
      {error && !loading && items.length === 0 ? (
        <EmptyState
          title="Could not load notifications"
          description="Try again or check your connection."
          action={
            <Button variant="outlined" onClick={handleRefresh} sx={{ textTransform: "none", fontWeight: 600 }}>
              Retry
            </Button>
          }
        />
      ) : (
        <RegisterTable
          columns={columns}
          rows={items}
          getRowId={(row) => row.id}
          loading={loading}
          onRowClick={(row) => void handleRowClick(row)}
          serverPaged={{
            totalCount,
            page,
            pageSize: PAGE_SIZE,
            pageSizeOptions: [PAGE_SIZE],
            onPageChange: setPage
          }}
          empty={
            hasFilters
              ? {
                  title: "No matching notifications",
                  description: "No notifications match the selected state, type, or search.",
                  action: (
                    <Button variant="outlined" onClick={clearFilters} sx={{ textTransform: "none", fontWeight: 600 }}>
                      Clear filters
                    </Button>
                  )
                }
              : {
                  title: "No notifications",
                  description: "You are up to date - nothing needs your attention."
                }
          }
        />
      )}
    </LabPage>
  );
}
