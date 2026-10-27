import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { backupStatusTone, DatabaseOverviewPage } from "./DatabaseOverviewPage";
import type { BackupRun, DatabaseOverview } from "./services/DatabaseOverviewService";

const get = vi.fn();
vi.mock("./services/DatabaseOverviewService", () => ({
  DatabaseOverviewService: { get: () => get() }
}));

afterEach(() => {
  cleanup();
  get.mockReset();
});

const NOW = new Date("2026-10-27T12:00:00Z");

function backup(overrides: Partial<BackupRun> = {}): BackupRun {
  return {
    id: 1,
    startedAtUtc: "2026-10-27T02:00:00Z",
    finishedAtUtc: "2026-10-27T02:10:00Z",
    succeeded: true,
    fileName: "microlims_20261027_020000.dump",
    sizeBytes: 1024,
    error: null,
    host: "db-host",
    ...overrides
  };
}

describe("backupStatusTone", () => {
  it("is green for a successful backup finished within 24 hours", () => {
    expect(backupStatusTone(backup(), NOW)).toBe("success");
  });

  it("is amber for a successful backup older than 24 hours", () => {
    expect(backupStatusTone(backup({ finishedAtUtc: "2026-10-25T23:59:00Z" }), NOW)).toBe("warning");
  });

  it("is amber for a failed backup, however recent", () => {
    expect(backupStatusTone(backup({ succeeded: false, error: "pg_dump failed with exit code 1" }), NOW)).toBe("warning");
  });

  it("is amber when no backup has ever been recorded", () => {
    expect(backupStatusTone(null, NOW)).toBe("warning");
  });
});

describe("DatabaseOverviewPage", () => {
  const overview: DatabaseOverview = {
    databaseSizeBytes: 5242880,
    databaseSizePretty: "5120 kB",
    topTables: [
      { schema: "public", name: "Samples", totalSizeBytes: 4096, totalSizePretty: "4096 bytes", estimatedRows: 1200 },
      { schema: "public", name: "Media", totalSizeBytes: 2048, totalSizePretty: "2048 bytes", estimatedRows: null }
    ],
    latestBackup: null
  };

  it("shows the database size, the largest tables and the backup status", async () => {
    get.mockResolvedValue(overview);
    render(<PinnedLightTheme><DatabaseOverviewPage /></PinnedLightTheme>);

    expect(await screen.findByText("5120 kB")).toBeTruthy();
    expect(screen.getByText("public.Samples")).toBeTruthy();
    expect(screen.getByText("1,200")).toBeTruthy();
    expect(screen.getByText("not analyzed yet")).toBeTruthy();
    expect(screen.getByText("No backup has been recorded yet.")).toBeTruthy();
  });

  it("reports a load failure", async () => {
    get.mockRejectedValue({ response: { data: { message: "Forbidden." } } });
    render(<PinnedLightTheme><DatabaseOverviewPage /></PinnedLightTheme>);

    await waitFor(() => expect(screen.getByText("Forbidden.")).toBeTruthy());
  });
});
