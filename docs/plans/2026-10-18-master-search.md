# Global Master Search Implementation Plan

> **For Antigravity:** REQUIRED WORKFLOW: Use `.agent/workflows/execute-plan.md` to execute this plan in single-flow mode.

**Goal:** Implement a unified Global Master Search accessible via the topbar and `Ctrl+K` / `Cmd+K` keyboard shortcut, enabling laboratory users to instantly discover samples, test orders, materials, equipment, culture media, and controlled documents with strict laboratory section isolation.

**Architecture:** A dedicated high-performance backend search service (`IGlobalSearchService`) executes scoped, parallel-safe Entity Framework queries against indexed database tables with laboratory section filtering (`IUserSectionScopeService`). The frontend leverages a command-palette dialog (`GlobalSearchDialog`) built with MUI Dialog, keyboard navigation, `localStorage` search history, and deep-linking into existing MicroLIMS module routes.

**Tech Stack:** .NET 10 (C#, Entity Framework Core, PostgreSQL/Npgsql), React 19, TypeScript, Material UI v9, Vitest, xUnit.

---

### Task 1: Database Indexing for High-Performance Search (Phase 2)

**Files:**
- Modify: `backend/MicroLIMS.Persistence/Configurations/SampleConfiguration.cs:11-20`
- Modify: `backend/MicroLIMS.Persistence/Configurations/MaterialConfiguration.cs:25-35`
- Create: Migration `AddGlobalSearchIndexes` via `dotnet ef migrations add`
- Test: `backend/MicroLIMS.Tests/UnitTests/GlobalSearchServiceTests.cs`

**Step 1: Write the failing test**

Add an architectural/model test verifying the index presence on the EF Core entity model.

```csharp
[Fact]
public void DbModel_SampleAndMaterial_ContainSearchIndexes()
{
    var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;
    using var db = new MicroLimsDbContext(options);

    var sampleType = db.Model.FindEntityType(typeof(Sample));
    Assert.NotNull(sampleType);
    var sampleIndexes = sampleType.GetIndexes().Select(i => string.Join(",", i.Properties.Select(p => p.Name))).ToList();
    Assert.Contains("BatchNumber", sampleIndexes);
    Assert.Contains("ControlNumber", sampleIndexes);

    var materialType = db.Model.FindEntityType(typeof(Material));
    Assert.NotNull(materialType);
    var materialIndexes = materialType.GetIndexes().Select(i => string.Join(",", i.Properties.Select(p => p.Name))).ToList();
    Assert.Contains("BatchNumber", materialIndexes);
    Assert.Contains("MaterialName", materialIndexes);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test backend/MicroLIMS.Tests --filter FullyQualifiedName~DbModel_SampleAndMaterial_ContainSearchIndexes`
Expected: FAIL with missing index assertions for `BatchNumber`.

**Step 3: Write minimal implementation**

In `backend/MicroLIMS.Persistence/Configurations/SampleConfiguration.cs`:
```csharp
builder.HasIndex(s => s.BatchNumber);
builder.HasIndex(s => s.ControlNumber);
```

In `backend/MicroLIMS.Persistence/Configurations/MaterialConfiguration.cs`:
```csharp
builder.HasIndex(m => m.BatchNumber);
builder.HasIndex(m => m.MaterialName);
```

Run EF Migration command:
```bash
dotnet ef migrations add AddGlobalSearchIndexes --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API
```

**Step 4: Run test to verify it passes**

Run: `dotnet test backend/MicroLIMS.Tests --filter FullyQualifiedName~DbModel_SampleAndMaterial_ContainSearchIndexes`
Expected: PASS

**Step 5: Commit**

```bash
git add backend/MicroLIMS.Persistence/Configurations/ backend/MicroLIMS.Persistence/Migrations/ backend/MicroLIMS.Tests/
git commit -m "feat(search): add database indexes for sample and material global search"
```

---

### Task 2: Frontend Global Search API Client & TypeScript Types (Phase 3.1)

**Files:**
- Create: `frontend/src/api/globalSearchApi.ts`
- Create: `frontend/src/api/globalSearchApi.test.ts`
- Modify: `frontend/src/services/apiClient.ts` (if needed for exports)

**Step 1: Write the failing test**

In `frontend/src/api/globalSearchApi.test.ts`:
```typescript
import { describe, expect, it, vi, beforeEach } from "vitest";
import { globalSearchApi, GlobalSearchCategoryDto } from "./globalSearchApi";
import { apiClient } from "../services/apiClient";

vi.mock("../services/apiClient", () => ({
  apiClient: {
    get: vi.fn()
  }
}));

describe("globalSearchApi", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("calls /api/search/global with encoded query parameters", async () => {
    const mockResponse = {
      data: {
        success: true,
        data: {
          query: "SMP-2026",
          totalMatches: 1,
          executionTimeMs: 12,
          categories: [
            {
              categoryName: "Samples",
              count: 1,
              items: [
                {
                  id: "1",
                  title: "SMP-2026-001",
                  subtitle: "Paracetamol 500mg",
                  category: "Samples",
                  badge: "InTesting",
                  route: "/receiving-testing?sampleId=1",
                  matchedField: "ReferenceNumber"
                }
              ]
            }
          ]
        }
      }
    };

    vi.mocked(apiClient.get).mockResolvedValueOnce(mockResponse);

    const result = await globalSearchApi.search("SMP-2026", "Samples", 10);

    expect(apiClient.get).toHaveBeenCalledWith("/search/global", {
      params: { q: "SMP-2026", category: "Samples", limit: 10 },
      signal: undefined
    });
    expect(result.totalMatches).toBe(1);
    expect(result.categories[0].categoryName).toBe("Samples");
  });

  it("passes abort signal to apiClient", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ data: { success: true, data: { query: "", totalMatches: 0, executionTimeMs: 0, categories: [] } } });
    const controller = new AbortController();

    await globalSearchApi.search("test", undefined, 5, controller.signal);

    expect(apiClient.get).toHaveBeenCalledWith("/search/global", {
      params: { q: "test", category: undefined, limit: 5 },
      signal: controller.signal
    });
  });
});
```

**Step 2: Run test to verify it fails**

Run: `npm test -- frontend/src/api/globalSearchApi.test.ts`
Expected: FAIL with "Cannot find module './globalSearchApi'"

**Step 3: Write minimal implementation**

In `frontend/src/api/globalSearchApi.ts`:
```typescript
import { apiClient } from "../services/apiClient";

export interface GlobalSearchItemDto {
  id: string;
  title: string;
  subtitle: string;
  category: string;
  badge?: string;
  route: string;
  matchedField: string;
  additionalInfo?: string;
}

export interface GlobalSearchCategoryDto {
  categoryName: string;
  count: number;
  items: GlobalSearchItemDto[];
}

export interface GlobalSearchResultDto {
  query: string;
  totalMatches: number;
  categories: GlobalSearchCategoryDto[];
  executionTimeMs: number;
}

export const globalSearchApi = {
  search: async (
    query: string,
    category?: string,
    limit: number = 5,
    signal?: AbortSignal
  ): Promise<GlobalSearchResultDto> => {
    const res = await apiClient.get<{ success: boolean; data: GlobalSearchResultDto }>("/search/global", {
      params: { q: query, category, limit },
      signal
    });
    return res.data?.data ?? { query, totalMatches: 0, categories: [], executionTimeMs: 0 };
  }
};
```

**Step 4: Run test to verify it passes**

Run: `npm test -- frontend/src/api/globalSearchApi.test.ts`
Expected: PASS

**Step 5: Commit**

```bash
git add frontend/src/api/globalSearchApi.ts frontend/src/api/globalSearchApi.test.ts
git commit -m "feat(search): add global search frontend api client and tests"
```

---

### Task 3: Command Palette Dialog Component (Phase 3.2)

**Files:**
- Create: `frontend/src/components/search/GlobalSearchDialog.tsx`
- Create: `frontend/src/components/search/GlobalSearchDialog.test.tsx`
- Create: `frontend/src/components/search/useRecentSearches.ts`

**Step 1: Write the failing test**

In `frontend/src/components/search/GlobalSearchDialog.test.tsx`:
```typescript
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { GlobalSearchDialog } from "./GlobalSearchDialog";
import { globalSearchApi } from "../../api/globalSearchApi";

vi.mock("../../api/globalSearchApi", () => ({
  globalSearchApi: {
    search: vi.fn()
  }
}));

describe("GlobalSearchDialog", () => {
  afterEach(cleanup);
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("renders search input when open", () => {
    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <GlobalSearchDialog open={true} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    expect(screen.getByPlaceholderText(/search microlims/i)).toBeInTheDocument();
  });

  it("executes search when input changes and renders results", async () => {
    vi.mocked(globalSearchApi.search).mockResolvedValueOnce({
      query: "SMP",
      totalMatches: 1,
      executionTimeMs: 10,
      categories: [
        {
          categoryName: "Samples",
          count: 1,
          items: [
            {
              id: "101",
              title: "SMP-2026-0001",
              subtitle: "Raw Material • Testing",
              category: "Samples",
              badge: "InTesting",
              route: "/receiving-testing?sampleId=101",
              matchedField: "ReferenceNumber"
            }
          ]
        }
      ]
    });

    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <GlobalSearchDialog open={true} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    const input = screen.getByPlaceholderText(/search microlims/i);
    fireEvent.change(input, { target: { value: "SMP" } });

    await waitFor(() => {
      expect(screen.getByText("SMP-2026-0001")).toBeInTheDocument();
      expect(screen.getByText("Raw Material • Testing")).toBeInTheDocument();
    });
  });

  it("stores and displays recent searches from localStorage", async () => {
    localStorage.setItem("microlims_recent_searches", JSON.stringify(["PARACETAMOL", "BATCH-99"]));

    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <GlobalSearchDialog open={true} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    expect(screen.getByText("PARACETAMOL")).toBeInTheDocument();
    expect(screen.getByText("BATCH-99")).toBeInTheDocument();
  });
});
```

**Step 2: Run test to verify it fails**

Run: `npm test -- frontend/src/components/search/GlobalSearchDialog.test.tsx`
Expected: FAIL with "Cannot find module './GlobalSearchDialog'"

**Step 3: Write minimal implementation**

Create `frontend/src/components/search/useRecentSearches.ts`:
```typescript
import { useState, useEffect } from "react";

const STORAGE_KEY = "microlims_recent_searches";
const MAX_RECENT = 5;

export function useRecentSearches() {
  const [recentSearches, setRecentSearches] = useState<string[]>([]);

  useEffect(() => {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) setRecentSearches(JSON.parse(stored));
    } catch {
      // Ignore localStorage read failures
    }
  }, []);

  const addSearch = (term: string) => {
    const trimmed = term.trim();
    if (!trimmed || trimmed.length < 2) return;
    setRecentSearches(prev => {
      const updated = [trimmed, ...prev.filter(t => t.toLowerCase() !== trimmed.toLowerCase())].slice(0, MAX_RECENT);
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
      } catch {
        // Ignore localStorage write failures
      }
      return updated;
    });
  };

  const clearSearches = () => {
    setRecentSearches([]);
    localStorage.removeItem(STORAGE_KEY);
  };

  return { recentSearches, addSearch, clearSearches };
}
```

Create `frontend/src/components/search/GlobalSearchDialog.tsx`:
```tsx
import React, { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import {
  Dialog, DialogContent, Box, InputBase, Typography, Chip, CircularProgress,
  List, ListItemButton, ListItemText, Divider, IconButton
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import ClearIcon from "@mui/icons-material/Clear";
import HistoryIcon from "@mui/icons-material/History";
import ArrowForwardIosIcon from "@mui/icons-material/ArrowForwardIos";
import { globalSearchApi, GlobalSearchResultDto, GlobalSearchItemDto } from "../../api/globalSearchApi";
import { useRecentSearches } from "./useRecentSearches";

interface GlobalSearchDialogProps {
  open: boolean;
  onClose: () => void;
}

export function GlobalSearchDialog({ open, onClose }: GlobalSearchDialogProps) {
  const navigate = useNavigate();
  const [query, setQuery] = useState("");
  const [activeCategory, setActiveCategory] = useState<string>("All");
  const [loading, setLoading] = useState(false);
  const [results, setResults] = useState<GlobalSearchResultDto | null>(null);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const { recentSearches, addSearch, clearSearches } = useRecentSearches();

  const abortControllerRef = useRef<AbortController | null>(null);

  // Flattened items for keyboard navigation
  const flatItems: GlobalSearchItemDto[] = (results?.categories ?? []).flatMap(c => c.items);

  useEffect(() => {
    if (!open) {
      setQuery("");
      setResults(null);
      setSelectedIndex(0);
      return;
    }
  }, [open]);

  useEffect(() => {
    if (query.trim().length < 2) {
      setResults(null);
      setLoading(false);
      return;
    }

    setLoading(true);
    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;

    const timer = setTimeout(async () => {
      try {
        const cat = activeCategory === "All" ? undefined : activeCategory;
        const res = await globalSearchApi.search(query, cat, 5, controller.signal);
        setResults(res);
        setSelectedIndex(0);
      } catch (err: unknown) {
        if ((err as Error)?.name !== "CanceledError" && (err as Error)?.name !== "AbortError") {
          setResults(null);
        }
      } finally {
        setLoading(false);
      }
    }, 300);

    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [query, activeCategory]);

  const handleSelectItem = (item: GlobalSearchItemDto) => {
    addSearch(query);
    onClose();
    navigate(item.route);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setSelectedIndex(prev => (prev + 1 < flatItems.length ? prev + 1 : prev));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setSelectedIndex(prev => (prev > 0 ? prev - 1 : 0));
    } else if (e.key === "Enter" && flatItems[selectedIndex]) {
      e.preventDefault();
      handleSelectItem(flatItems[selectedIndex]);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      fullWidth
      slotProps={{
        paper: {
          sx: {
            borderRadius: 3,
            overflow: "hidden",
            maxHeight: "80vh",
            position: "relative",
            top: { xs: 0, sm: -80 }
          }
        }
      }}
    >
      <Box sx={{ display: "flex", alignItems: "center", px: 2.5, py: 1.5, borderBottom: 1, borderColor: "divider", gap: 1.5 }}>
        <SearchIcon color="action" />
        <InputBase
          autoFocus
          fullWidth
          placeholder="Search MicroLIMS (samples, orders, materials, equipment, docs)..."
          value={query}
          onChange={e => setQuery(e.target.value)}
          onKeyDown={handleKeyDown}
          sx={{ fontSize: 16 }}
        />
        {loading && <CircularProgress size={20} />}
        {query && !loading && (
          <IconButton size="small" onClick={() => setQuery("")}>
            <ClearIcon fontSize="small" />
          </IconButton>
        )}
      </Box>

      <DialogContent sx={{ p: 2 }}>
        {query.trim().length < 2 && recentSearches.length > 0 && (
          <Box sx={{ mb: 2 }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
              <Typography variant="caption" color="text.secondary" sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                <HistoryIcon sx={{ fontSize: 14 }} /> RECENT SEARCHES
              </Typography>
              <Typography variant="caption" color="primary" sx={{ cursor: "pointer" }} onClick={clearSearches}>
                Clear
              </Typography>
            </Box>
            <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
              {recentSearches.map(term => (
                <Chip key={term} label={term} size="small" onClick={() => setQuery(term)} />
              ))}
            </Box>
          </Box>
        )}

        {results && results.categories.length === 0 && !loading && (
          <Box sx={{ py: 6, textAlign: "center", color: "text.secondary" }}>
            <Typography variant="body1">No matches found for "{query}"</Typography>
            <Typography variant="caption">Try searching by sample reference, batch number, material name, or equipment code.</Typography>
          </Box>
        )}

        {results && results.categories.length > 0 && (
          <List disablePadding>
            {results.categories.map(category => (
              <React.Fragment key={category.categoryName}>
                <Typography variant="caption" sx={{ fontWeight: 700, color: "text.secondary", px: 1, py: 0.5, display: "block" }}>
                  {category.categoryName.toUpperCase()} ({category.count})
                </Typography>
                {category.items.map(item => {
                  const itemIndex = flatItems.indexOf(item);
                  const isSelected = itemIndex === selectedIndex;
                  return (
                    <ListItemButton
                      key={item.id}
                      selected={isSelected}
                      onClick={() => handleSelectItem(item)}
                      sx={{ borderRadius: 1.5, my: 0.5, display: "flex", justifyContent: "space-between" }}
                    >
                      <ListItemText
                        primary={item.title}
                        secondary={item.subtitle}
                        primaryTypographyProps={{ fontWeight: 600, fontSize: 14 }}
                        secondaryTypographyProps={{ fontSize: 12 }}
                      />
                      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                        {item.badge && <Chip label={item.badge} size="small" variant="outlined" />}
                        <ArrowForwardIosIcon sx={{ fontSize: 12, color: "action.disabled" }} />
                      </Box>
                    </ListItemButton>
                  );
                })}
                <Divider sx={{ my: 1 }} />
              </React.Fragment>
            ))}
          </List>
        )}
      </DialogContent>
    </Dialog>
  );
}
```

**Step 4: Run test to verify it passes**

Run: `npm test -- frontend/src/components/search/GlobalSearchDialog.test.tsx`
Expected: PASS

**Step 5: Commit**

```bash
git add frontend/src/components/search/
git commit -m "feat(search): create global search command palette dialog"
```

---

### Task 4: Global Search Shortcut Hook & Header Integration (Phase 4)

**Files:**
- Create: `frontend/src/hooks/useGlobalSearchShortcut.ts`
- Create: `frontend/src/hooks/useGlobalSearchShortcut.test.ts`
- Modify: `frontend/src/components/Header.tsx:170-205`

**Step 1: Write the failing test**

In `frontend/src/hooks/useGlobalSearchShortcut.test.ts`:
```typescript
import { describe, expect, it, vi, afterEach } from "vitest";
import { renderHook, act, cleanup } from "@testing-library/react";
import { useGlobalSearchShortcut } from "./useGlobalSearchShortcut";

describe("useGlobalSearchShortcut", () => {
  afterEach(cleanup);

  it("calls callback when Ctrl+K is pressed", () => {
    const onTrigger = vi.fn();
    renderHook(() => useGlobalSearchShortcut(onTrigger));

    act(() => {
      window.dispatchEvent(new KeyboardEvent("keydown", { key: "k", ctrlKey: true }));
    });

    expect(onTrigger).toHaveBeenCalledTimes(1);
  });

  it("calls callback when Cmd+K is pressed (macOS)", () => {
    const onTrigger = vi.fn();
    renderHook(() => useGlobalSearchShortcut(onTrigger));

    act(() => {
      window.dispatchEvent(new KeyboardEvent("keydown", { key: "k", metaKey: true }));
    });

    expect(onTrigger).toHaveBeenCalledTimes(1);
  });

  it("does not trigger on plain k press", () => {
    const onTrigger = vi.fn();
    renderHook(() => useGlobalSearchShortcut(onTrigger));

    act(() => {
      window.dispatchEvent(new KeyboardEvent("keydown", { key: "k" }));
    });

    expect(onTrigger).not.toHaveBeenCalled();
  });
});
```

**Step 2: Run test to verify it fails**

Run: `npm test -- frontend/src/hooks/useGlobalSearchShortcut.test.ts`
Expected: FAIL with "Cannot find module './useGlobalSearchShortcut'"

**Step 3: Write minimal implementation**

Create `frontend/src/hooks/useGlobalSearchShortcut.ts`:
```typescript
import { useEffect } from "react";

export function useGlobalSearchShortcut(onTrigger: () => void) {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        onTrigger();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onTrigger]);
}
```

In `frontend/src/components/Header.tsx`:
Add state and shortcut hook:
```tsx
const [searchOpen, setSearchOpen] = useState(false);
useGlobalSearchShortcut(() => setSearchOpen(prev => !prev));
```
In JSX topbar:
```tsx
<ButtonBase
  onClick={() => setSearchOpen(true)}
  sx={{
    display: { xs: "none", sm: "flex" },
    alignItems: "center",
    gap: 1.5,
    px: 2,
    py: 0.75,
    bgcolor: "rgba(255,255,255,0.15)",
    borderRadius: 2,
    color: "common.white",
    width: { sm: 240, md: 360 },
    justifyContent: "space-between",
    "&:hover": { bgcolor: "rgba(255,255,255,0.22)" }
  }}
  aria-label="Search MicroLIMS (Ctrl+K)"
>
  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
    <SearchIcon sx={{ fontSize: 18, opacity: 0.8 }} />
    <Typography sx={{ fontSize: 13, opacity: 0.85 }}>Search MicroLIMS...</Typography>
  </Box>
  <Box sx={{ fontSize: 11, bgcolor: "rgba(0,0,0,0.2)", px: 0.8, py: 0.2, borderRadius: 1 }}>
    Ctrl+K
  </Box>
</ButtonBase>
```
And on mobile (`xs`), add search icon in right icon row:
```tsx
<IconButton
  onClick={() => setSearchOpen(true)}
  sx={{ color: "common.white", display: { sm: "none" } }}
  aria-label="Search MicroLIMS"
>
  <SearchIcon />
</IconButton>
```
And mount `<GlobalSearchDialog open={searchOpen} onClose={() => setSearchOpen(false)} />`.

**Step 4: Run test to verify it passes**

Run: `npm test -- frontend/src/hooks/useGlobalSearchShortcut.test.ts`
Expected: PASS

**Step 5: Commit**

```bash
git add frontend/src/hooks/useGlobalSearchShortcut.ts frontend/src/hooks/useGlobalSearchShortcut.test.ts frontend/src/components/Header.tsx
git commit -m "feat(search): integrate global search trigger and Ctrl+K shortcut in topbar"
```

---

### Task 5: Verification & Full Regression Testing (Phase 5)

**Files:**
- Test: `backend/MicroLIMS.Tests/`
- Test: `frontend/src/`

**Step 1: Run Backend Tests**

Run command:
```bash
dotnet test backend/MicroLIMS.Tests --filter FullyQualifiedName~GlobalSearch
```
Expected: PASS (All GlobalSearchServiceTests pass)

Run command:
```bash
dotnet test backend/MicroLIMS.Tests --filter FullyQualifiedName~ArchitectureTests
```
Expected: PASS (All 99 architecture tests pass with authorization matrix validation)

**Step 2: Run Frontend Tests**

Run command:
```bash
npm test --prefix frontend
```
Expected: PASS (All vitest suites run and pass without regressions)

**Step 3: Commit Tracker and Validation Evidence**

Update `<project-root>/docs/plans/task.md` with completed statuses and evidence.

```bash
git add docs/plans/task.md
git commit -m "docs(search): complete master search implementation tracking"
```

---

## Execution Handoff

Plan complete and saved to `docs/plans/2026-10-18-master-search.md`.  
Next step: run `.agent/workflows/execute-plan.md` to execute this plan task-by-task in single-flow mode.
