import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { useEffect, useState } from "react";
import { recentRecordsService } from "./services/recentRecordsService";
import { useTrackRecent } from "./useTrackRecent";

vi.mock("./services/recentRecordsService", () => ({
  recentRecordsService: { list: vi.fn(), touch: vi.fn(), clear: vi.fn() }
}));

// A stand-in detail screen: it loads a record, then reports the opening.
function SampleScreen({ load }: { load: () => Promise<{ id: number; code: string }> }) {
  const [sample, setSample] = useState<{ id: number; code: string } | null>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    load().then(setSample).catch(() => setFailed(true));
  }, [load]);
  useTrackRecent("sample", sample?.id);
  return (
    <div>
      {sample && <h1>{sample.code}</h1>}
      {failed && <p role="alert">Could not load</p>}
    </div>
  );
}

beforeEach(() => {
  vi.mocked(recentRecordsService.touch).mockResolvedValue(undefined);
});
afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("useTrackRecent", () => {
  it("opening a sample posts once, after it has loaded", async () => {
    const load = vi.fn().mockResolvedValue({ id: 42, code: "S-2026-042" });
    const { rerender } = render(<SampleScreen load={load} />);
    expect(recentRecordsService.touch).not.toHaveBeenCalled();
    await screen.findByRole("heading", { name: "S-2026-042" });
    rerender(<SampleScreen load={load} />);
    rerender(<SampleScreen load={load} />);
    expect(recentRecordsService.touch).toHaveBeenCalledTimes(1);
    expect(recentRecordsService.touch).toHaveBeenCalledWith("sample", 42);
  });

  it("a record that fails to load is not posted", async () => {
    const load = vi.fn().mockRejectedValue(new Error("403"));
    render(<SampleScreen load={load} />);
    await screen.findByRole("alert");
    expect(recentRecordsService.touch).not.toHaveBeenCalled();
  });

  it("a failed post does not disturb the screen", async () => {
    vi.mocked(recentRecordsService.touch).mockRejectedValue(new Error("network"));
    render(<SampleScreen load={vi.fn().mockResolvedValue({ id: 5, code: "S-5" })} />);
    expect(await screen.findByRole("heading", { name: "S-5" })).toBeTruthy();
    expect(recentRecordsService.touch).toHaveBeenCalledTimes(1);
  });
});
