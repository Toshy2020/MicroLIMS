# MicroLIMS — Connecting Frontend, Backend, API, and Database

This is the concrete, step-by-step version of "how does everything talk
to everything." Follow it in order — each layer depends on the one before it.

---

## 1. The chain, end to end

```
Browser (React, Vite dev server :5173)
   │  every request goes through ONE file: frontend/src/services/apiClient.ts
   │  it reads VITE_API_BASE_URL from frontend/.env
   ▼
ASP.NET Core API (:5000 or whatever you configure, Program.cs)
   │  CORS policy in Program.cs must list the frontend's exact origin
   │  JWT bearer auth validates every request except /api/auth/login
   ▼
Application services (business logic) → EF Core (MicroLimsDbContext)
   ▼
PostgreSQL (connection string in appsettings.json)
```

Nothing skips a layer. The frontend never talks to the database, and no
frontend code contains business rules — it only calls an endpoint and
renders what comes back (Frozen Principle #3, enforced throughout).

---

## 2. Database — first, because everything else depends on it

```bash
cd backend
```

Edit `MicroLIMS.API/appsettings.json`:
```json
"ConnectionStrings": { "Default": "Host=localhost;Database=microlims;Username=microlims_user;Password=YOUR_REAL_PASSWORD" }
```

Create the database and user in Postgres first (`psql` or a GUI), then:
```bash
cd MicroLIMS.API
dotnet ef migrations add InitialCreate --project ../MicroLIMS.Persistence --startup-project .
dotnet ef database update --project ../MicroLIMS.Persistence --startup-project .
```

This reads every `DbSet<T>` in `MicroLimsDbContext` and every
`IEntityTypeConfiguration<T>` in `Persistence/Configurations/` and
generates the actual SQL tables — this is the **only** place the schema
is defined. If you add a new entity later, you always run
`dotnet ef migrations add <Name>` again before it exists in the database.

**Seed data**: temporarily call `DbSeeder.Seed(db)` from `Program.cs`
after `app.Build()` (see backend README), run once, then remove it.
Without this you have no roles, no admin user, and no example master
data — nothing in the UI will have anything to select.

---

## 3. Backend — how a request actually flows through the code

Take `POST /api/samples` (receiving a Product/RM/PM sample) as the
concrete example — every other endpoint follows the same shape:

1. **`Program.cs`** wires up JWT auth, CORS, and calls
   `builder.Services.AddApplicationServices(config)` — this one method
   in `Extensions/ServiceCollectionExtensions.cs` registers every
   service, workflow engine, and repository. If you add a new service
   class, it does nothing until you add one line here.
2. **`SampleController.Receive`** — the only thing a controller is
   allowed to do: read the request, call a service, shape the response.
   It never contains business logic.
3. **`ReceivingService.ReceiveSampleAsync`** — validates via
   `ReceiveSampleValidator`, then calls the workflow engine.
4. **`ProductWorkflowEngine.ReceiveAsync`** — the actual frozen rule:
   reads the Item's configured tests, generates one `TestOrder` per
   test, assigns the reference number via `ReferenceNumberGenerator`.
5. **`MicroLimsDbContext.SaveChanges`** — this is where the automatic
   audit trail is captured (every insert/update/delete, no exceptions)
   and now also the searchable `AuditLog` reference columns.
6. Response flows back up as a `SampleDto`, wrapped in `ApiResponse<T>`.

**If you're adding a new feature**, follow this exact same chain:
Entity → EF configuration (if needed) → Application service/workflow →
Controller → DI registration → (only then) frontend service + page.

---

## 4. API contract — how the frontend knows what to send

Every backend endpoint has a C# `record` defining its request shape
(e.g. `ReceiveItemBasedSampleRequest` in `SampleController.cs`) and
returns `ApiResponse<T>` — `{ success, data, message, errors }`.

The frontend's matching TypeScript interface **must have the exact
same field names**, just camelCase instead of PascalCase — ASP.NET
Core's default JSON serialization does this conversion automatically,
and `Program.cs` also configures `JsonStringEnumConverter` so enums
arrive as readable strings (`"Pending"`) instead of numbers.

Example — backend:
```csharp
public record ReceiveWaterRequest(int WaterSamplingPointId, int CauseOfTestingId, string SampleQuantity, string SampledBy, string ControlNumber);
```
Frontend (must match exactly):
```typescript
export interface WaterReceiveRequest {
  waterSamplingPointId: number;
  causeOfTestingId: number;
  sampleQuantity: string;
  sampledBy: string;
  controlNumber: string;
}
```

**When backend and frontend types drift apart, this is the #1 source
of runtime bugs** — a silently-undefined field, not a build error,
since TypeScript can't check against C# records. Whenever you change a
backend request/response shape, grep the frontend for every file that
calls that endpoint and update all of them in the same pass — this is
exactly the kind of gap I had to sweep for repeatedly while rebuilding
this frontend, and it's worth being disciplined about going forward.

---

## 5. Frontend — how a page actually calls the API

Every module follows: **Page component → `services/*.ts` → `apiClient`**.

```
ReceiveSamplePage.tsx
   └─ calls ReceiveService.receiveWater(request)
        └─ apiClient.post("/water/receive", request)
             └─ baseURL (from .env) + "/water/receive"
             └─ interceptor attaches Authorization: Bearer <token>
```

`apiClient` (`frontend/src/services/apiClient.ts`) is the **only** file
that knows the API base URL or attaches auth headers — never call
`axios` or `fetch` directly from a page or service.

**Auth token flow**: `AuthContext` stores the JWT (and refresh token)
in `localStorage` after login. `apiClient`'s request interceptor reads
it on every call. Its response interceptor watches for `401` and
redirects to `/login` automatically — you don't need to handle expired
tokens per-page.

**Adding a new page**: create `services/XService.ts` (mirrors backend
DTOs exactly, per section 4), build the page component using the
shared design components (`PageHeader`, `SectionTitle`, `StatusBadge`,
etc. in `components/`), add it to `routes/AppRoutes.tsx`, and add its
label/path to `routes/menuConfig.ts` so it actually appears in
navigation for the right roles.

---

## 6. CORS — the thing that silently breaks everything if misconfigured

In `Program.cs`:
```csharp
policy.WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173")
```
`appsettings.json` → `Frontend:Origin` must be the **exact** origin the
browser sends (protocol + host + port, no trailing slash) — e.g.
`http://localhost:5173`, not `localhost:5173` or `http://127.0.0.1:5173`.
If these don't match exactly, every request fails in the browser
console with a CORS error, and it looks like nothing is happening —
this is the first thing to check if the frontend can't reach the backend at all.

---

## 7. Running both together for local development

**Terminal 1 — backend:**
```bash
cd backend/MicroLIMS.API
dotnet run
```
Confirm it's up: open `https://localhost:<port>/swagger` (shown in the
console on startup) and try `POST /api/auth/login`.

**Terminal 2 — frontend:**
```bash
cd frontend
npm install
cp .env.example .env
# edit .env: VITE_API_BASE_URL must match the backend's actual URL + /api
npm run dev
```
Open `http://localhost:5173`, log in with `admin` / `ChangeMe123!`
(change immediately), and you're connected end to end.

---

## 8. Order of operations for a genuinely fresh environment

This is the sequence that actually works, in order — skipping ahead
will hit "not found" or empty-dropdown errors because later steps
depend on earlier ones existing:

1. Create Postgres database → run migrations → seed
2. Start backend → confirm Swagger loads → log in as admin, change password
3. Create Equipment (autoclaves, incubators)
4. Create MediaType(s)
5. Prepare a Media lot → run through GPT → Release it
6. Create CauseOfTesting, Neutralizer, DiluentType entries
7. Create Items (with SOP Number, assigned tests, Specifications)
8. Create Water Sampling Points, EM Departments→Rooms→RoomTestConfigurations, Machines→Parts→MachinePartConfigurations
9. Start the frontend, log in, receive your first sample
10. For EM/After Cleaning: use the Preparation pages to generate TestOrders
11. For Product/RM/PM/Water: use Test Preparation before entering results
12. Work a sample through Testing Workspace → Review → Approval → Reports

---

## 9. What to check first when something doesn't work

| Symptom | Most likely cause |
|---|---|
| Frontend shows nothing, console has CORS errors | `Frontend:Origin` in appsettings.json doesn't exactly match the browser's origin |
| 401 on every request immediately after login | JWT `Key` in appsettings.json is empty or too short, or clock skew between servers |
| Dropdown is empty on a page | The master data for it hasn't been created yet (see section 8's order) |
| "not found" from a workflow engine | You're calling an endpoint before its prerequisite exists (e.g. receiving before Items are configured) |
| A field is `undefined` in the UI but present in the network response | Frontend interface field name doesn't exactly match the backend's camelCase JSON — see section 4 |
| `dotnet ef migrations add` fails | A new entity is missing a required foreign key target, or a circular reference needs `DeleteBehavior.Restrict` (already applied in `SampleConfiguration`, follow that pattern for new relationships) |
