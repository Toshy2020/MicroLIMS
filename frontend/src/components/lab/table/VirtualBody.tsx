import {
  Fragment,
  useCallback,
  useLayoutEffect,
  useRef,
  useState,
  type KeyboardEvent,
  type ReactNode,
} from "react";
import { TableBody, TableCell, TableRow, useTheme } from "@mui/material";
import {
  defaultRangeExtractor,
  useVirtualizer,
  type Range,
  type Virtualizer,
} from "@tanstack/react-virtual";
import type { StatusTone } from "../../../theme/statusTokens";

const DEFAULT_VIEWPORT_WIDTH = 800;
const DEFAULT_VIEWPORT_HEIGHT = 320;
const DEFAULT_ROW_HEIGHT = 40;
const DEFAULT_OVERSCAN = 3;

export interface VirtualBodyProps<T> {
  rows: T[];
  getRowId: (row: T) => string | number;
  // Held in state by the parent (callback ref) so the virtualizer re-attaches
  // once the container has mounted.
  scrollElement: HTMLDivElement | null;
  colSpan: number;
  estimateRowHeight?: number;
  overscan?: number;
  expandedIds?: ReadonlySet<string | number>;
  renderExpanded?: (row: T) => ReactNode;
  renderRowCells: (
    row: T,
    rowId: string | number,
    isExpanded: boolean
  ) => ReactNode;
  onRowClick?: (row: T) => void;
  rowTone?: (row: T) => StatusTone | undefined;
}

function getElementHeight(element: Element, fallback: number): number {
  const rect = element.getBoundingClientRect();
  if (rect.height > 0) {
    return rect.height;
  }
  if (element instanceof HTMLElement && element.offsetHeight > 0) {
    return element.offsetHeight;
  }
  return fallback;
}

function observeScrollContainerRect(
  instance: Virtualizer<HTMLDivElement, Element>,
  cb: (rect: { width: number; height: number }) => void
): (() => void) | undefined {
  const element = instance.scrollElement;
  if (!element) return undefined;

  const readRect = () => {
    const rect = element.getBoundingClientRect();
    const width =
      element.clientWidth || rect.width || DEFAULT_VIEWPORT_WIDTH;
    const height =
      element.clientHeight || rect.height || DEFAULT_VIEWPORT_HEIGHT;
    cb({ width: Math.round(width), height: Math.round(height) });
  };

  readRect();

  const targetWindow = instance.targetWindow;
  if (!targetWindow || typeof targetWindow.ResizeObserver === "undefined") {
    return undefined;
  }

  const observer = new targetWindow.ResizeObserver(() => {
    readRect();
  });
  observer.observe(element);
  return () => {
    observer.disconnect();
  };
}

export function VirtualBody<T>({
  rows,
  getRowId,
  scrollElement,
  colSpan,
  estimateRowHeight = DEFAULT_ROW_HEIGHT,
  overscan = DEFAULT_OVERSCAN,
  expandedIds,
  renderExpanded,
  renderRowCells,
  onRowClick,
  rowTone,
}: VirtualBodyProps<T>) {
  const theme = useTheme();
  const tbodyRef = useRef<HTMLTableSectionElement | null>(null);
  const [focusedIndex, setFocusedIndex] = useState<number | null>(null);
  const pendingFocusIndexRef = useRef<number | null>(null);

  const rangeExtractor = useCallback(
    (range: Range) => {
      const next = defaultRangeExtractor(range);
      if (
        focusedIndex !== null &&
        focusedIndex >= 0 &&
        focusedIndex < range.count &&
        !next.includes(focusedIndex)
      ) {
        next.push(focusedIndex);
        next.sort((a, b) => a - b);
      }
      return next;
    },
    [focusedIndex]
  );

  const measureRowElement = useCallback(
    (element: Element) => {
      const baseHeight = getElementHeight(element, estimateRowHeight);
      const rowId = element.getAttribute("data-row-id");
      const next = element.nextElementSibling;
      if (
        rowId !== null &&
        next !== null &&
        next.getAttribute("data-expanded-for") === rowId
      ) {
        return baseHeight + getElementHeight(next, 0);
      }
      return baseHeight;
    },
    [estimateRowHeight]
  );

  const virtualizer = useVirtualizer<HTMLDivElement, Element>({
    count: rows.length,
    getScrollElement: () => scrollElement,
    estimateSize: () => estimateRowHeight,
    overscan,
    getItemKey: (index) => getRowId(rows[index]),
    initialRect: {
      width: DEFAULT_VIEWPORT_WIDTH,
      height: DEFAULT_VIEWPORT_HEIGHT,
    },
    observeElementRect: observeScrollContainerRect,
    measureElement: measureRowElement,
    rangeExtractor,
  });

  virtualizer.shouldAdjustScrollPositionOnItemSizeChange = () => false;

  const virtualItems = virtualizer.getVirtualItems();
  const totalSize = virtualizer.getTotalSize();

  useLayoutEffect(() => {
    const tbody = tbodyRef.current;
    if (!tbody) return;
    const rowNodes = tbody.querySelectorAll<HTMLElement>("tr[data-index]");
    rowNodes.forEach((node) => {
      virtualizer.measureElement(node);
    });
  }, [expandedIds, rows, virtualizer]);

  useLayoutEffect(() => {
    const targetIndex = pendingFocusIndexRef.current;
    if (targetIndex === null) return;
    const tbody = tbodyRef.current;
    if (!tbody) return;
    const rowEl = tbody.querySelector<HTMLElement>(
      `tr[data-index="${targetIndex}"]`
    );
    if (rowEl) {
      pendingFocusIndexRef.current = null;
      rowEl.focus();
    }
  }, [focusedIndex, virtualItems]);

  const handleRowKeyDown = (
    e: KeyboardEvent<HTMLTableRowElement>,
    index: number,
    row: T
  ) => {
    if (e.target !== e.currentTarget) return;

    if (onRowClick && (e.key === "Enter" || e.key === " ")) {
      e.preventDefault();
      onRowClick(row);
      return;
    }

    if (e.key !== "ArrowDown" && e.key !== "ArrowUp") return;

    const nextIndex =
      e.key === "ArrowDown"
        ? Math.min(index + 1, rows.length - 1)
        : Math.max(index - 1, 0);

    if (nextIndex === index) return;

    e.preventDefault();
    pendingFocusIndexRef.current = nextIndex;
    setFocusedIndex(nextIndex);

    const scrollEl = scrollElement;
    const targetItem = virtualizer.measurementsCache[nextIndex];
    if (scrollEl && targetItem) {
      const viewportHeight =
        scrollEl.clientHeight ||
        scrollEl.getBoundingClientRect().height ||
        DEFAULT_VIEWPORT_HEIGHT;
      const currentTop = scrollEl.scrollTop;
      let nextTop = currentTop;
      if (targetItem.end > currentTop + viewportHeight) {
        nextTop = targetItem.end - viewportHeight;
      } else if (targetItem.start < currentTop) {
        nextTop = targetItem.start;
      }
      if (nextTop !== currentTop) {
        scrollEl.scrollTop = Math.max(0, nextTop);
        scrollEl.dispatchEvent(new Event("scroll"));
      }
    } else {
      virtualizer.scrollToIndex(nextIndex, { align: "auto" });
    }
  };

  const lastEnd =
    virtualItems.length > 0 ? virtualItems[virtualItems.length - 1].end : 0;
  const paddingBottom = Math.max(0, totalSize - lastEnd);

  return (
    <TableBody ref={tbodyRef}>
      {virtualItems.map((virtualItem, k) => {
        const row = rows[virtualItem.index];
        const rowId = getRowId(row);
        const isExpanded = Boolean(expandedIds?.has(rowId) && renderExpanded);
        const prevEnd = k === 0 ? 0 : virtualItems[k - 1].end;
        const gapBefore = Math.max(0, virtualItem.start - prevEnd);
        const tone = rowTone?.(row);
        const toneTokens = tone ? theme.custom.status[tone] : undefined;

        return (
          <Fragment key={rowId}>
            {gapBefore > 0 && (
              <TableRow
                role="presentation"
                aria-hidden="true"
                data-virtual-spacer="before"
                style={{ height: gapBefore }}
              >
                <TableCell
                  role="presentation"
                  colSpan={colSpan}
                  sx={{ p: 0, border: 0 }}
                />
              </TableRow>
            )}
            <TableRow
              data-index={virtualItem.index}
              data-row-id={String(rowId)}
              data-expanded={isExpanded ? "true" : undefined}
              ref={virtualizer.measureElement}
              aria-rowindex={virtualItem.index + 2}
              hover={!!onRowClick}
              tabIndex={0}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
              onFocus={(e) => {
                if (e.target === e.currentTarget) {
                  setFocusedIndex(virtualItem.index);
                }
              }}
              onBlur={(e) => {
                if (
                  e.target === e.currentTarget &&
                  pendingFocusIndexRef.current === null
                ) {
                  setFocusedIndex((prev) =>
                    prev === virtualItem.index ? null : prev
                  );
                }
              }}
              onKeyDown={(e) => handleRowKeyDown(e, virtualItem.index, row)}
              sx={{
                cursor: onRowClick ? "pointer" : undefined,
                "&:focus-visible": {
                  outline: "2px solid",
                  outlineColor: "primary.main",
                  outlineOffset: -2,
                },
                ...(toneTokens
                  ? {
                      bgcolor: toneTokens.bg,
                      "& > td:first-of-type": {
                        borderLeft: `3px solid ${toneTokens.border}`,
                      },
                      "&.MuiTableRow-hover:hover": {
                        filter: "brightness(0.97)",
                      },
                    }
                  : null),
              }}
            >
              {renderRowCells(row, rowId, isExpanded)}
            </TableRow>
            {isExpanded && renderExpanded && (
              <TableRow
                role="presentation"
                data-expanded-for={String(rowId)}
              >
                <TableCell
                  colSpan={colSpan}
                  sx={{ py: 1.5, px: 2, bgcolor: "action.hover" }}
                >
                  {renderExpanded(row)}
                </TableCell>
              </TableRow>
            )}
          </Fragment>
        );
      })}
      {paddingBottom > 0 && (
        <TableRow
          role="presentation"
          aria-hidden="true"
          data-virtual-spacer="after"
          style={{ height: paddingBottom }}
        >
          <TableCell
            role="presentation"
            colSpan={colSpan}
            sx={{ p: 0, border: 0 }}
          />
        </TableRow>
      )}
    </TableBody>
  );
}
