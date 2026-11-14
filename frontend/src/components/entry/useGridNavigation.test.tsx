import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { useGridNavigation } from "./useGridNavigation";

interface TestGridProps {
  rows?: number;
  cols?: number;
  initialValues?: string[][];
  isEditable?: (r: number, c: number) => boolean;
  onCopyDown?: (r: number, c: number) => void;
  onSave?: () => void;
  onFormSubmit?: () => void;
  expandedCell?: string | null;
  selectCell?: string | null;
}

function TestGrid({
  rows = 3,
  cols = 3,
  initialValues,
  isEditable,
  onCopyDown,
  onSave,
  onFormSubmit,
  expandedCell = null,
  selectCell = null
}: TestGridProps) {
  const [values, setValues] = useState<string[][]>(
    () =>
      initialValues ??
      Array.from({ length: rows }, () => Array.from({ length: cols }, () => ""))
  );

  const { cellProps } = useGridNavigation({
    rows,
    cols,
    isEditable,
    onCopyDown: (r, c) => {
      onCopyDown?.(r, c);
      if (r > 0) {
        setValues((prev) => {
          const next = prev.map((row) => [...row]);
          next[r][c] = prev[r - 1][c];
          return next;
        });
      }
    },
    onSave
  });

  const grid = (
    <table>
      <tbody>
        {Array.from({ length: rows }, (_, r) => (
          <tr key={r}>
            {Array.from({ length: cols }, (_, c) => {
              const key = `${r}:${c}`;
              const props = cellProps(r, c);
              const editable = isEditable ? isEditable(r, c) : true;
              if (selectCell === key) {
                return (
                  <td key={c}>
                    <select
                      aria-label={`Cell ${r},${c}`}
                      value={values[r]?.[c] ?? ""}
                      onChange={(e) => {
                        const val = e.target.value;
                        setValues((prev) => {
                          const next = prev.map((row) => [...row]);
                          next[r][c] = val;
                          return next;
                        });
                      }}
                      {...props}
                    >
                      <option value="">--</option>
                      <option value="A">A</option>
                    </select>
                  </td>
                );
              }
              return (
                <td key={c}>
                  <input
                    aria-label={`Cell ${r},${c}`}
                    value={values[r]?.[c] ?? ""}
                    readOnly={!editable}
                    aria-expanded={expandedCell === key ? "true" : undefined}
                    onChange={(e) => {
                      const val = e.target.value;
                      setValues((prev) => {
                        const next = prev.map((row) => [...row]);
                        next[r][c] = val;
                        return next;
                      });
                    }}
                    {...props}
                  />
                </td>
              );
            })}
          </tr>
        ))}
      </tbody>
    </table>
  );

  return (
    <PinnedLightTheme>
      <MemoryRouter>
        {onFormSubmit ? (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              onFormSubmit();
            }}
          >
            {grid}
            <button type="submit">Submit</button>
          </form>
        ) : (
          grid
        )}
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("useGridNavigation", () => {
  it("moves with Enter, ArrowDown, ArrowUp, ArrowRight, ArrowLeft and stays put at edges", async () => {
    const user = userEvent.setup();
    render(<TestGrid rows={3} cols={3} />);

    const cell = (r: number, c: number) => screen.getByLabelText(`Cell ${r},${c}`);
    expect(cell(0, 0).getAttribute("data-grid-cell")).toBe("0:0");

    await user.click(cell(0, 0));
    expect(document.activeElement).toBe(cell(0, 0));

    // Up at row 0 stays put
    await user.keyboard("{ArrowUp}");
    expect(document.activeElement).toBe(cell(0, 0));

    // Left at col 0 stays put
    await user.keyboard("{ArrowLeft}");
    expect(document.activeElement).toBe(cell(0, 0));

    // Enter moves down
    await user.keyboard("{Enter}");
    expect(document.activeElement).toBe(cell(1, 0));

    // ArrowDown moves down
    await user.keyboard("{ArrowDown}");
    expect(document.activeElement).toBe(cell(2, 0));

    // Enter at last row stays put
    await user.keyboard("{Enter}");
    expect(document.activeElement).toBe(cell(2, 0));

    // ArrowDown at last row stays put
    await user.keyboard("{ArrowDown}");
    expect(document.activeElement).toBe(cell(2, 0));

    // ArrowUp moves up
    await user.keyboard("{ArrowUp}");
    expect(document.activeElement).toBe(cell(1, 0));

    // ArrowRight moves right
    await user.keyboard("{ArrowRight}");
    expect(document.activeElement).toBe(cell(1, 1));
    await user.keyboard("{ArrowRight}");
    expect(document.activeElement).toBe(cell(1, 2));

    // ArrowRight at last col stays put
    await user.keyboard("{ArrowRight}");
    expect(document.activeElement).toBe(cell(1, 2));

    // ArrowLeft moves left
    await user.keyboard("{ArrowLeft}");
    expect(document.activeElement).toBe(cell(1, 1));
  });

  it("Left in the middle of text moves the caret, not the cell", async () => {
    const user = userEvent.setup();
    render(
      <TestGrid
        rows={1}
        cols={3}
        initialValues={[["abc", "def", ""]]}
      />
    );

    const c0 = screen.getByLabelText("Cell 0,0") as HTMLInputElement;
    const c1 = screen.getByLabelText("Cell 0,1") as HTMLInputElement;

    await user.click(c1);
    // Click puts caret at end of "def" (index 3)
    expect(c1.selectionStart).toBe(3);

    // ArrowLeft from end of "def" moves caret to 2, stays in c1
    await user.keyboard("{ArrowLeft}");
    expect(document.activeElement).toBe(c1);
    expect(c1.selectionStart).toBe(2);

    // Move caret to 1, then 0
    await user.keyboard("{ArrowLeft}{ArrowLeft}");
    expect(document.activeElement).toBe(c1);
    expect(c1.selectionStart).toBe(0);

    // Now at start (0), ArrowLeft moves to c0
    await user.keyboard("{ArrowLeft}");
    expect(document.activeElement).toBe(c0);

    // Put caret in middle of "abc" (index 1) and press ArrowRight -> moves caret to 2, stays in c0
    c0.setSelectionRange(1, 1);
    await user.keyboard("{ArrowRight}");
    expect(document.activeElement).toBe(c0);
    expect(c0.selectionStart).toBe(2);

    // Move to end (3) then ArrowRight -> moves to c1
    await user.keyboard("{ArrowRight}");
    expect(document.activeElement).toBe(c0);
    await user.keyboard("{ArrowRight}");
    expect(document.activeElement).toBe(c1);
  });

  it("Left and Right move immediately on non-text elements like select", async () => {
    const user = userEvent.setup();
    render(<TestGrid rows={1} cols={3} selectCell="0:1" />);

    const c0 = screen.getByLabelText("Cell 0,0");
    const c1 = screen.getByLabelText("Cell 0,1");
    const c2 = screen.getByLabelText("Cell 0,2");

    await user.click(c1);
    await user.keyboard("{ArrowRight}");
    expect(document.activeElement).toBe(c2);

    await user.keyboard("{ArrowLeft}");
    expect(document.activeElement).toBe(c1);

    await user.keyboard("{ArrowLeft}");
    expect(document.activeElement).toBe(c0);
  });

  it("Enter does not submit the form; Ctrl+Enter calls onSave", async () => {
    const user = userEvent.setup();
    const onFormSubmit = vi.fn();
    const onSave = vi.fn();
    render(<TestGrid rows={2} cols={2} onFormSubmit={onFormSubmit} onSave={onSave} />);

    const c0 = screen.getByLabelText("Cell 0,0");
    await user.click(c0);
    await user.keyboard("{Enter}");
    expect(onFormSubmit).not.toHaveBeenCalled();
    expect(document.activeElement).toBe(screen.getByLabelText("Cell 1,0"));

    // Also on last row, Enter does not submit the form
    await user.keyboard("{Enter}");
    expect(onFormSubmit).not.toHaveBeenCalled();

    // Ctrl+Enter calls onSave
    await user.keyboard("{Control>}{Enter}{/Control}");
    expect(onSave).toHaveBeenCalledTimes(1);
    expect(onFormSubmit).not.toHaveBeenCalled();
  });

  it("Ctrl+D calls onCopyDown and is ignored on row 0", async () => {
    const user = userEvent.setup();
    const onCopyDown = vi.fn();
    render(
      <TestGrid
        rows={2}
        cols={2}
        initialValues={[["42", "99"], ["", ""]]}
        onCopyDown={onCopyDown}
      />
    );

    const r0c0 = screen.getByLabelText("Cell 0,0") as HTMLInputElement;
    const r1c0 = screen.getByLabelText("Cell 1,0") as HTMLInputElement;

    await user.click(r0c0);
    await user.keyboard("{Control>}d{/Control}");
    expect(onCopyDown).not.toHaveBeenCalled();
    expect(r0c0.value).toBe("42");

    await user.click(r1c0);
    await user.keyboard("{Control>}d{/Control}");
    expect(onCopyDown).toHaveBeenCalledWith(1, 0);
    expect(r1c0.value).toBe("42");
  });

  it("read-only cells are skipped", async () => {
    const user = userEvent.setup();
    render(
      <TestGrid
        rows={3}
        cols={3}
        isEditable={(r, c) => !(r === 1 && c === 0) && !(r === 0 && c === 1)}
      />
    );

    const cell = (r: number, c: number) => screen.getByLabelText(`Cell ${r},${c}`);
    await user.click(cell(0, 0));

    // ArrowRight skips (0, 1) and lands on (0, 2)
    await user.keyboard("{ArrowRight}");
    expect(document.activeElement).toBe(cell(0, 2));

    // ArrowLeft skips (0, 1) and lands on (0, 0)
    await user.keyboard("{ArrowLeft}");
    expect(document.activeElement).toBe(cell(0, 0));

    // ArrowDown skips (1, 0) and lands on (2, 0)
    await user.keyboard("{ArrowDown}");
    expect(document.activeElement).toBe(cell(2, 0));

    // ArrowUp skips (1, 0) and lands on (0, 0)
    await user.keyboard("{ArrowUp}");
    expect(document.activeElement).toBe(cell(0, 0));
  });

  it("open Autocomplete keeps arrow keys", async () => {
    const user = userEvent.setup();
    const onCopyDown = vi.fn();
    render(
      <TestGrid
        rows={2}
        cols={2}
        expandedCell="1:0"
        onCopyDown={onCopyDown}
      />
    );

    const r1c0 = screen.getByLabelText("Cell 1,0");
    await user.click(r1c0);

    await user.keyboard("{ArrowDown}{ArrowUp}{ArrowLeft}{ArrowRight}{Enter}{Control>}d{/Control}");
    expect(document.activeElement).toBe(r1c0);
    expect(onCopyDown).not.toHaveBeenCalled();
  });
});
