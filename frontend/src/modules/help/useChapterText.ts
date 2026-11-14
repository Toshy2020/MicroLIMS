import { useCallback, useEffect, useState } from "react";

export type ChapterState =
  | { status: "loading" }
  | { status: "ready"; markdown: string }
  | { status: "error" };

// Loads one chapter; a later id (or retry) supersedes an earlier request.
export function useChapterText(
  chapterId: string | null,
  load: (chapterId: string) => Promise<string>
): { state: ChapterState; retry(): void } {
  const [state, setState] = useState<ChapterState>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (chapterId === null) return;
    let current = true;
    setState({ status: "loading" });
    load(chapterId).then(
      (markdown) => {
        if (current) setState({ status: "ready", markdown });
      },
      () => {
        if (current) setState({ status: "error" });
      }
    );
    return () => {
      current = false;
    };
  }, [chapterId, load, attempt]);

  const retry = useCallback(() => setAttempt((n) => n + 1), []);
  return { state, retry };
}
