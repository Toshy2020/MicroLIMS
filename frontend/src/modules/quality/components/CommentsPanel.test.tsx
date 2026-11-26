import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn()
}));
vi.mock("../../../services/apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/apiClient")>();
  return { ...actual, apiClient: http };
});

import { CommentsPanel } from "./CommentsPanel";
import { comment, refusal } from "../testFixtures";
import type { QualityCommentDto } from "../../../services/qualityService";

function renderPanel(options: { comments?: QualityCommentDto[] | null; canEdit?: boolean; onChanged?: () => Promise<void> } = {}) {
  const onChanged = options.onChanged ?? vi.fn().mockResolvedValue(undefined);
  render(
    <PinnedLightTheme>
      <CommentsPanel
        recordId={5}
        recordVersion={8}
        comments={options.comments === undefined ? [] : options.comments}
        currentUserId={7}
        canEdit={options.canEdit ?? true}
        onChanged={onChanged}
      />
    </PinnedLightTheme>
  );
  return { onChanged };
}

describe("CommentsPanel", () => {
  beforeEach(() => {
    http.post.mockResolvedValue({ data: { data: comment() } });
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("lists comments with their author and time with the time zone, marking the signed-in user's own", () => {
    renderPanel({
      comments: [
        comment({ id: 1, text: "Probe replaced on the night shift.", authorUserId: 7, authorName: "Ann Analyst", createdAtUtc: "2026-11-21T11:00:00Z" }),
        comment({ id: 2, text: "Please attach the certificate.", authorUserId: 12, authorName: "Maya QA", createdAtUtc: "2026-11-22T08:30:00Z" })
      ]
    });

    expect(screen.getByText("Probe replaced on the night shift.")).toBeTruthy();
    expect(screen.getByText(/^You · .*21 Nov 2026.*\d{2}:\d{2}\s+\S+/)).toBeTruthy();
    expect(screen.getByText(/^Maya QA · .*22 Nov 2026/)).toBeTruthy();
    expect(screen.queryByText(/User 12/)).toBeNull();
  });

  it("posts a comment with the record version, clears the field and reloads", async () => {
    const user = userEvent.setup();
    const { onChanged } = renderPanel();
    const post = screen.getByRole("button", { name: "Post comment" }) as HTMLButtonElement;
    expect(post.disabled).toBe(true);

    const field = screen.getByLabelText("Add a comment") as HTMLTextAreaElement;
    await user.type(field, "Checked the log.");
    expect(screen.getByText("16 of 2000 characters")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Post comment" }));

    await waitFor(() => {
      expect(http.post).toHaveBeenCalledWith("/quality/5/comments", { text: "Checked the log." }, { headers: { "If-Match": '"8"' } });
    });
    await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    expect(field.value).toBe("");
  });

  it("keeps what was typed and shows the server's refusal when the comment is refused", async () => {
    const user = userEvent.setup();
    http.post.mockRejectedValue(refusal("This record was changed by someone else. Reload it and try again."));
    const { onChanged } = renderPanel();

    const field = screen.getByLabelText("Add a comment") as HTMLTextAreaElement;
    await user.type(field, "Checked the log.");
    await user.click(screen.getByRole("button", { name: "Post comment" }));

    expect((await screen.findByRole("alert")).textContent).toContain("changed by someone else");
    expect(field.value).toBe("Checked the log.");
    expect(onChanged).not.toHaveBeenCalled();
  });

  it("is read-only without edit permission", () => {
    renderPanel({ canEdit: false, comments: [comment()] });

    expect(screen.queryByLabelText("Add a comment")).toBeNull();
    expect(screen.queryByRole("button", { name: "Post comment" })).toBeNull();
    expect(screen.getByText("Probe replaced on the night shift.")).toBeTruthy();
  });

  it("shows the empty state and the unavailable state", () => {
    renderPanel({ comments: [] });
    expect(screen.getByText("No comments yet.")).toBeTruthy();
    cleanup();

    renderPanel({ comments: null });
    expect(screen.getByText("Comments could not be loaded.")).toBeTruthy();
  });

  it("marks comments added after closure as an addendum", () => {
    renderPanel({
      comments: [
        comment({ id: 1, text: "During investigation.", isAddendum: false }),
        comment({ id: 2, text: "Post-closure note.", isAddendum: true })
      ]
    });

    expect(screen.getAllByText("Added after closure")).toHaveLength(1);
  });
});
