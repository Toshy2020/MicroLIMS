import type { ReactElement } from "react";
import { render } from "@testing-library/react";
import { BenchModeProvider } from "../../theme/BenchModeContext";
import { ThemeModeProvider } from "../../theme/ThemeModeContext";

// Mounts the real bench-mode and theme providers; bench mode is chosen the way
// the device chooses it, through the stored preference.
function withProviders(ui: ReactElement) {
  return (
    <BenchModeProvider>
      <ThemeModeProvider>{ui}</ThemeModeProvider>
    </BenchModeProvider>
  );
}

export function renderWithTheme(ui: ReactElement, options: { bench: boolean }) {
  localStorage.setItem("microlims_bench_mode", options.bench ? "on" : "off");
  const result = render(withProviders(ui));
  return { ...result, rerender: (next: ReactElement) => result.rerender(withProviders(next)) };
}
