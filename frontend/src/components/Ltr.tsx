import type { ReactNode } from "react";

/**
 * @component Ltr
 * @purpose Keeps an identifier, number, code or date in left-to-right order inside an Arabic sentence
 * (sample numbers, lot numbers, equipment codes, versions, counts). Without it the browser's
 * bidirectional algorithm can reorder the characters or move punctuation across it.
 * @a11y A `<bdi>` isolates the run and adds no role, so screen readers read it in place.
 */
export function Ltr({ children }: { children: ReactNode }) {
  return <bdi dir="ltr">{children}</bdi>;
}
