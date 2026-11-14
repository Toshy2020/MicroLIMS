import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { useRowSelection } from "./useRowSelection";

function SelectionHarness() {
  const [filterKey, setFilterKey] = useState("status=Active|q=");
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState("code:asc");
  const sel = useRowSelection<string>(filterKey);

  return (
    <div>
      <div data-testid="count">{sel.count}</div>
      <div data-testid="all-matching">{String(sel.allMatching)}</div>
      <div data-testid="selected-ids">{[...sel.selected].join(",")}</div>
      <div data-testid="page-sort">{`${page}|${sort}`}</div>
      <button type="button" onClick={() => sel.toggle("EQ-001")}>
        Toggle 1
      </button>
      <button
        type="button"
        onClick={() => sel.togglePage(["EQ-001", "EQ-002", "EQ-003"])}
      >
        Toggle Page 1
      </button>
      <button type="button" onClick={() => sel.selectAllMatching(312)}>
        Select All Matching
      </button>
      <button type="button" onClick={() => sel.clear()}>
        Clear
      </button>
      <button type="button" onClick={() => setPage(2)}>
        Next Page
      </button>
      <button type="button" onClick={() => setSort("code:desc")}>
        Change Sort
      </button>
      <button type="button" onClick={() => setFilterKey("status=Due|q=")}>
        Change Filter
      </button>
    </div>
  );
}

afterEach(() => {
  cleanup();
});

describe("useRowSelection", () => {
  it("toggle, togglePage, selectAllMatching; survives page and sort changes; cleared when filters change", async () => {
    const user = userEvent.setup();
    render(<SelectionHarness />);

    expect(screen.getByTestId("count").textContent).toBe("0");

    await user.click(screen.getByRole("button", { name: "Toggle 1" }));
    expect(screen.getByTestId("count").textContent).toBe("1");
    expect(screen.getByTestId("selected-ids").textContent).toBe("EQ-001");

    // togglePage when partially selected selects the rest of the page
    await user.click(screen.getByRole("button", { name: "Toggle Page 1" }));
    expect(screen.getByTestId("count").textContent).toBe("3");
    expect(screen.getByTestId("selected-ids").textContent).toBe(
      "EQ-001,EQ-002,EQ-003"
    );

    // togglePage when all on page are selected deselects the page
    await user.click(screen.getByRole("button", { name: "Toggle Page 1" }));
    expect(screen.getByTestId("count").textContent).toBe("0");

    await user.click(screen.getByRole("button", { name: "Select All Matching" }));
    expect(screen.getByTestId("count").textContent).toBe("312");
    expect(screen.getByTestId("all-matching").textContent).toBe("true");

    // Survives page and sort changes
    await user.click(screen.getByRole("button", { name: "Next Page" }));
    await user.click(screen.getByRole("button", { name: "Change Sort" }));
    expect(screen.getByTestId("count").textContent).toBe("312");
    expect(screen.getByTestId("all-matching").textContent).toBe("true");

    // Cleared when filters/search (resetKey) change
    await user.click(screen.getByRole("button", { name: "Change Filter" }));
    expect(screen.getByTestId("count").textContent).toBe("0");
    expect(screen.getByTestId("all-matching").textContent).toBe("false");
    expect(screen.getByTestId("selected-ids").textContent).toBe("");
  });
});
