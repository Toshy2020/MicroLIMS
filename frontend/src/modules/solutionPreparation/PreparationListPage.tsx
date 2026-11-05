import { useState, useEffect, useMemo, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { Button, Chip, FormControl, InputLabel, Select, MenuItem, Alert, Stack, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { LabPage, FilterBar, RegisterTable } from "../../components/lab";
import type { RegisterColumn } from "../../components/lab";
import { StatusBadge } from "../../components/StatusBadge";
import { monospaceFontFamily } from "../../theme/palette";
import { SolutionPreparationService } from "./services/SolutionPreparationService";
import { formatLabDate, formatLabDateTime } from "../../utils/formatDate";
import type {
  SolutionType,
  SolutionPreparationStatus,
  SolutionPreparationListItem
} from "./types";
import { solutionTypeLabel } from "./types";

export function PreparationListPage() {
  const navigate = useNavigate();

  const [items, setItems] = useState<SolutionPreparationListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<"ALL" | SolutionPreparationStatus>("ALL");
  const [typeFilter, setTypeFilter] = useState<"ALL" | SolutionType>("ALL");
  const [releaseFilter, setReleaseFilter] = useState<"ALL" | "AWAITING">("ALL");

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await SolutionPreparationService.getAll(
        statusFilter === "ALL" ? undefined : statusFilter,
        typeFilter === "ALL" ? undefined : typeFilter
      );
      setItems(data);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not load solution preparations.");
    } finally {
      setLoading(false);
    }
  }, [statusFilter, typeFilter]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Client-side search filtering
  const filteredItems = useMemo(() => {
    const base = releaseFilter === "AWAITING" ? items.filter((i) => i.approvalStatus === "PendingReview") : items;
    if (!searchQuery.trim()) return base;
    const q = searchQuery.toLowerCase().trim();
    return base.filter((item) => {
      const codeMatch = item.code?.toLowerCase().includes(q) ?? false;
      const nameMatch = item.solutionMasterName.toLowerCase().includes(q);
      const methodMatch = item.hplcMethodAbbreviation?.toLowerCase().includes(q) ?? false;
      const preparerMatch = item.preparedByUserName?.toLowerCase().includes(q) ?? false;
      return codeMatch || nameMatch || methodMatch || preparerMatch;
    });
  }, [items, searchQuery, releaseFilter]);

  const openRecord = (item: SolutionPreparationListItem) =>
    navigate(item.effectiveStatus === "InProgress" ? `/preparation/${item.id}/edit` : `/preparation/${item.id}`);

  const columns: RegisterColumn<SolutionPreparationListItem>[] = [
    {
      key: "code",
      label: "Code",
      sortable: true,
      sortValue: (i) => i.code ?? "",
      render: (i) => i.code ? (
        <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}>{i.code}</Typography>
      ) : (
        <Typography variant="caption" sx={{ color: "text.secondary", fontStyle: "italic" }}>(In Progress)</Typography>
      )
    },
    { key: "type", label: "Type", sortable: true, render: (i) => solutionTypeLabel(i.type) },
    {
      key: "solutionMasterName",
      label: "Solution Recipe",
      sortable: true,
      render: (i) => <Typography variant="body2" sx={{ fontWeight: 600 }}>{i.solutionMasterName}</Typography>
    },
    { key: "hplcMethodAbbreviation", label: "HPLC Method", sortable: true, render: (i) => i.hplcMethodAbbreviation || "—" },
    {
      key: "effectiveStatus",
      label: "Status",
      sortable: true,
      render: (i) => (
        <Stack direction="row" spacing={0.75} sx={{ alignItems: "center", flexWrap: "wrap" }} useFlexGap>
          <StatusBadge status={i.effectiveStatus} />
          {i.approvalStatus === "PendingReview" && (
            <Chip label="Awaiting release" size="small" color="warning" variant="outlined" sx={{ fontSize: 11, height: 22 }} />
          )}
          {i.approvalStatus === "Rejected" && (
            <Chip label="Rejected" size="small" color="error" variant="outlined" sx={{ fontSize: 11, height: 22 }} />
          )}
        </Stack>
      )
    },
    {
      key: "preparedAt",
      label: "Prepared At",
      sortable: true,
      sortValue: (i) => i.preparedAt ?? "",
      render: (i) => (i.preparedAt ? formatLabDateTime(i.preparedAt) : "—")
    },
    {
      key: "expiresAt",
      label: "Expires At",
      sortable: true,
      sortValue: (i) => i.expiresAt ?? "",
      render: (i) => (i.expiresAt ? formatLabDate(i.expiresAt) : "—")
    },
    {
      key: "preparedByUserName",
      label: "Prepared By",
      sortable: true,
      sortValue: (i) => i.preparedByUserName ?? "",
      render: (i) => i.preparedByUserName || "—"
    }
  ];

  return (
    <LabPage
      title="Solution Preparation"
      subtitle="Manage and execute preparations for mobile phases, diluents, reagents, neutralizers, and volumetric titrants"
      actions={
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate("/preparation/new")} sx={{ textTransform: "none", fontWeight: 700 }}>
          Start Preparation
        </Button>
      }
      filters={
        <FilterBar
          search={searchQuery}
          onSearch={setSearchQuery}
          placeholder="Search code, recipe, method, preparer..."
          resultCount={filteredItems.length}
          onRefresh={loadData}
          refreshing={loading}
        >
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="status-filter-label">Status</InputLabel>
            <Select
              labelId="status-filter-label"
              value={statusFilter}
              label="Status"
              onChange={(e) => setStatusFilter(e.target.value as "ALL" | SolutionPreparationStatus)}
            >
              <MenuItem value="ALL">All Statuses</MenuItem>
              <MenuItem value="InProgress">In Progress</MenuItem>
              <MenuItem value="Prepared">Prepared</MenuItem>
              <MenuItem value="Expired">Expired</MenuItem>
              <MenuItem value="Discarded">Discarded</MenuItem>
              <MenuItem value="Cancelled">Cancelled</MenuItem>
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="type-filter-label">Type</InputLabel>
            <Select
              labelId="type-filter-label"
              value={typeFilter}
              label="Type"
              onChange={(e) => setTypeFilter(e.target.value as "ALL" | SolutionType)}
            >
              <MenuItem value="ALL">All Types</MenuItem>
              <MenuItem value="MobilePhase">Mobile Phase</MenuItem>
              <MenuItem value="Diluent">Diluent</MenuItem>
              <MenuItem value="Reagent">Reagent</MenuItem>
              <MenuItem value="Neutralizer">Neutralizer</MenuItem>
              <MenuItem value="Titrant">Titrant</MenuItem>
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 170 }}>
            <InputLabel id="release-filter-label">Release</InputLabel>
            <Select
              labelId="release-filter-label"
              value={releaseFilter}
              label="Release"
              onChange={(e) => setReleaseFilter(e.target.value as "ALL" | "AWAITING")}
            >
              <MenuItem value="ALL">All</MenuItem>
              <MenuItem value="AWAITING">Awaiting release</MenuItem>
            </Select>
          </FormControl>
        </FilterBar>
      }
    >
      {error && <Alert severity="error">{error}</Alert>}
      <RegisterTable
        columns={columns}
        rows={filteredItems}
        getRowId={(i) => i.id}
        loading={loading}
        onRowClick={openRecord}
        rowActions={(i) => [
          i.effectiveStatus === "InProgress"
            ? { label: "Resume Preparation", onClick: () => navigate(`/preparation/${i.id}/edit`) }
            : { label: "View Record", onClick: () => navigate(`/preparation/${i.id}`) }
        ]}
        empty={{
          title: "No solution preparations found",
          description: "Start a preparation or adjust the filters.",
          action: <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate("/preparation/new")}>Start Preparation</Button>
        }}
      />
    </LabPage>
  );
}
