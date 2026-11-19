import { lazy, Suspense } from "react";
import { useHelpPanel } from "./HelpPanelContext";
import type { HelpPanelServices } from "./HelpPanelContext";

// The panel, the guide loader and the markdown renderer are a separate chunk:
// nothing of them is fetched until help is first opened.
const HelpPanel = lazy(() => import("./HelpPanel").then((m) => ({ default: m.HelpPanel })));

export function HelpPanelSlot({ services }: { services?: HelpPanelServices }) {
  const { open } = useHelpPanel();
  if (!open) return null;
  return (
    <Suspense fallback={null}>
      <HelpPanel services={services} />
    </Suspense>
  );
}
