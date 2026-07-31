# MicroLIMS Backend

ASP.NET Core 8, Clean Architecture, EF Core + PostgreSQL, JWT auth.

## Projects (build order = dependency order)

| Project | Responsibility |
|---|---|
| `MicroLIMS.Domain` | Entities and enums only. No EF, no dependencies on anything else. |
| `MicroLIMS.Shared` | Cross-cutting helpers, constants, exceptions, the `ApiResponse<T>` envelope. |
| `MicroLIMS.Infrastructure` | External integrations: PDF generation, email, notifications, JWT issuing, file storage. |
| `MicroLIMS.Persistence` | `MicroLimsDbContext`, EF configurations, repositories, migrations, seed data. |
| `MicroLIMS.Application` | **All laboratory logic.** Services, workflows (Product/Water/EM/AfterCleaning/Pathogen/GPT), validators, DTOs, commands/queries. |
| `MicroLIMS.API` | Controllers, middleware, DI wiring (`Extensions/ServiceCollectionExtensions.cs`), `Program.cs`. No business logic. |
| `MicroLIMS.Tests` | xUnit tests — start with `WorkflowTests`, since those are the frozen rules. |

## Getting it running

Requires .NET 8 SDK and PostgreSQL (local or Docker).

```bash
cd backend

# 1. Restore all projects
dotnet restore

# 2. Point appsettings.json at your database and set a real Jwt:Key
#    (MicroLIMS.API/appsettings.json)

# 3. Add and apply the first migration
cd MicroLIMS.API
dotnet ef migrations add InitialCreate --project ../MicroLIMS.Persistence --startup-project .
dotnet ef database update --project ../MicroLIMS.Persistence --startup-project .

# 4. Run
dotnet run
```

Swagger UI is available at `/swagger` in Development.

To seed the first admin user + one example Item, call
`MicroLIMS.Persistence.Seed.DbSeeder.Seed(db)` once — easiest is a
temporary call right after `app.Build()` in `Program.cs` guarded by
`app.Environment.IsDevelopment()`, then remove it once you've logged in
and changed the password.

Default seeded login: `admin` / `ChangeMe123!` — **change this immediately.**

## Running tests

```bash
cd backend
dotnet test
```

## Extending safely

- New test type → add a `SampleTest` row via Item configuration, not a
  hardcoded field. Only add code for a genuinely new *rule* (like the
  Salmonella chain), and put it in `Application/Workflows` as its own
  static class or small workflow, following the existing pattern.
- New domain (e.g. a new sample category) → add to `SampleCategory`,
  add a workflow class in `Application/Workflows` if it needs multi-step
  logic, and a matching controller in `API/Controllers`.
- Never put business rules in a controller — controllers only:
  read request → call a service → shape the response.
- Never bypass `MicroLimsDbContext.SaveChanges` (e.g. raw SQL updates) —
  it's what captures the audit trail automatically.
