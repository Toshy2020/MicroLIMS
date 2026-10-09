# Migration chain findings (Task 16, measurement only)

Measured 2026-10-09 on the development PC (local Postgres 18, Release build of `MicroLIMS.API`, `ASPNETCORE_ENVIRONMENT=Development`, which runs `Database.Migrate()` and `DbSeeder.Seed` before Kestrel listens). Time is process start until the port accepts a connection. Throwaway database `microlims_scratch_mig`, dropped afterwards. No migration was changed.

| Measure | Value |
|---|---|
| Files in `backend/MicroLIMS.Persistence/Migrations/` | 306 (151 Designer files + 155 others including the snapshot) |
| Folder size | 40 MB |
| Model snapshot | 502,716 bytes |
| `__EFMigrationsHistory` rows after a full apply | 152 |
| Cold start, empty database (apply every migration + seed) | 41 s (one run) |
| Start with the database already at head | 8.6, 8.9, 8.9 s |
| Difference (cost of applying the chain from scratch) | about 32 s |

## Reading

- Applying the whole chain from empty costs about 32 s. That happens only on a brand-new database (a fresh clone, a CI database, a new Neon branch), never on a normal deploy where only the newest migrations are pending.
- A start at head still takes about 9 s on this PC. That figure includes the migration history check, seeding, DI and EF model construction, so it cannot be attributed to the number of migrations without profiling. The model snapshot is only read by `dotnet ef`, not at runtime.
- Not measured: the free-tier Render instance (0.1 CPU) or Neon. The 9 s will be several times higher there, but the contribution of the migration count to it is unknown.

## Recommendation

Do nothing. Nothing here shows the chain slowing a deploy. Squashing would only be worth discussing if a production start on Render is measured and the history check or model build turns out to dominate, and it would need a fresh baseline migration plus a plan for the existing Neon history table, so it should not be done without your decision. If you want the production number, the next step is to time a Render restart from its logs.

Side note: the plan's "154 migrations" was a file count; 151 migrations have a Designer file and the history table has 152 rows (one migration without a Designer file was not tracked down). Neither number changes the recommendation.
