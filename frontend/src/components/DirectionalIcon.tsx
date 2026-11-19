import { useTheme } from "@mui/material/styles";
import type { ReactNode } from "react";

/**
 * @component DirectionalIcon
 * @purpose Icons that point along the reading direction (chevrons, arrows "next"/"back") must mirror in
 * right-to-left text. Pass the icon as it should look in English; it is flipped horizontally in Arabic.
 * Icons that depict a thing (a clock, a search glass, an upload arrow) are NOT directional: do not wrap them.
 * @a11y Always decorative here: the control that holds it carries the accessible name.
 */
export function DirectionalIcon({ children }: { children: ReactNode }) {
  const rtl = useTheme().direction === "rtl";
  return (
    <span aria-hidden style={{ display: "inline-flex", transform: rtl ? "scaleX(-1)" : undefined }}>
      {children}
    </span>
  );
}
