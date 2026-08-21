# Document Control Module (Release 1a) Integration Report

**Target Module:** Document Control (Release 1a)  
**Inspection Type:** READ-ONLY Reconnaissance & Integration Assessment  
**Date:** August 21, 2026  
**Target Output File:** `./docs/DocumentControl_Integration_Report.md`

---

## 1. Summary — Five Core Infrastructure Principles for Document Control

Before writing any code for the Document Control module (Release 1a), developers must align with five foundational architectural realities established in the MicroLIMS codebase:

1. **Audit Trail Capturing is Automatic in EF Core, but Lacks Database-Level Protection:**  
   Audit logs (`AuditLog` entity) are generated automatically via `MicroLimsDbContext.SaveChanges()` using ChangeTracker diffs. However, **there are no database-level triggers, rules, or user privilege restrictions preventing `UPDATE` or `DELETE` on `AuditLogs`**. Audit log protection relies entirely on application-level read-only services ([AuditService.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Application/Services/AuditService.cs)). *Document Control requirements demanding DB-level immutable protection require explicit PostgreSQL database triggers/permissions.*

2. **File Storage & Cryptographic Verification Blueprint Already Exists:**  
   File I/O is decoupled via `IFileStorageService` and `LocalFileStorageService` saving to `./storage`. The exact pattern required for controlled files—server-generated storage keys, mandatory SHA-256 computation, SHA-256 integrity verification on retrieval, supersession chains, and access logging—is already fully implemented in [MaterialDocumentService.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Application/Services/MaterialDocumentService.cs).

3. **Part 11 Electronic Signature Infrastructure is Directly Reusable:**  
   [ElectronicSignatureService.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs) and [ReviewGateService.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Application/Services/ReviewGateService.cs) provide 21 CFR Part 11 append-only electronic signatures. Re-authentication via BCrypt password check is mandatory on signature creation. Signatures capture immutable snapshots of user full name, username, and role at execution time.

4. **Authorization Uses Global Roles, Not Per-Record Workflow Assignments:**  
   The API enforces authorization via coarse ASP.NET Core `[Authorize(Roles = "...")]` attributes. Fine-grained permissions ([PermissionService.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Application/Services/PermissionService.cs)) exist in the DB but are un-enforced. **Per-record authorization** (e.g. restricting document approval to an assigned Document Approver) does not exist in backend filters and must be implemented inside Document Control domain/application services.

5. **Soft Delete and Record Lifecycle Convention:**  
   The codebase shuns generic `IsDeleted` boolean flags. For controlled records ([MaterialDocument.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Domain/Entities/MaterialDocument.cs)), removal from active use is achieved via explicit `Status` enums (`Current`, `Superseded`, `Voided`), forward supersession links (`SupersededByDocumentId`), mandatory user justifications (`VoidReason`, `SupersessionReason`), and retention of physical underlying files.

---

## 2. Findings

### 2.1 Solution and Project Structure

#### Project Organization and Dependency Flow
The backend solution ([MicroLIMS.sln](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.sln)) comprises six core C# projects adhering to Clean Architecture principles:

```
           +---------------------------------------------+
           |               MicroLIMS.API                 |
           +------+---------------+---------------+------+
                  |               |               |
                  v               v               v
   +--------------+--+   +--------+-----+   +-----+--------+
   | MicroLIMS.      |   | MicroLIMS.   |   | MicroLIMS.   |
   | Infrastructure  |   | Application  |   | Persistence  |
   +--------------+--+   +--------+-----+   +-----+--------+
                  |               |               |
                  +-------+       v       +-------+
                          |  +----+----+  |
                          +->|MicroLIMS|<-+
                             | Domain  |
                             +----+----+
                                  ^
                                  |
                             +----+----+
                             |MicroLIMS|
                             | Shared  |
                             +---------+
```

- **`MicroLIMS.Domain`** (`namespace MicroLIMS.Domain.*`): Contains domain entities, business enums, and domain rules. Zero project dependencies.
- **`MicroLIMS.Shared`** (`namespace MicroLIMS.Shared.*`): Holds DTOs, custom API responses (`ApiResponse<T>`), validation rules, and constants (`RoleConstants`). Zero dependencies.
- **`MicroLIMS.Application`** (`namespace MicroLIMS.Application.*`): Contains application services, workflow engines, validators, and interfaces (`IReceivingService`, `IFileStorageService`). Depends on `MicroLIMS.Domain`, `MicroLIMS.Shared`, and `MicroLIMS.Persistence` (for DbContext LINQ queries).
- **`MicroLIMS.Infrastructure`** (`namespace MicroLIMS.Infrastructure.*`): Implements infrastructure interfaces (`JwtTokenService`, `EmailSender`, `PdfGenerator`, `LocalFileStorageService`). Depends on `MicroLIMS.Application`, `MicroLIMS.Domain`, `MicroLIMS.Shared`.
- **`MicroLIMS.Persistence`** (`namespace MicroLIMS.Persistence.*`): EF Core `MicroLimsDbContext`, table configurations, DB seeders, and EF migrations. Depends on `MicroLIMS.Domain` and `MicroLIMS.Shared`.
- **`MicroLIMS.API`** (`namespace MicroLIMS.API.*`): Controllers, middleware (`AuditMiddleware`), DI extensions (`ServiceCollectionExtensions.cs`), and `Program.cs`. Depends on all backend projects.

#### Feature Layout Blueprint: Material Document Subsystem
Taking the **Material Document** subsystem as an end-to-end blueprint:

1. **Domain Entities:**  
   [MaterialDocument.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Domain/Entities/MaterialDocument.cs) and [MaterialDocumentAccessLog.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Domain/Entities/MaterialDocumentAccessLog.cs) in `namespace MicroLIMS.Domain.Entities`. Enums: `MaterialDocumentStatus`, `MaterialDocumentType`, `MaterialDocumentAccessAction` in `MicroLIMS.Domain.Enums`.
2. **EF Core Persistence:**  
   [MaterialDocumentConfiguration.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Persistence/Configurations/MaterialDocumentConfiguration.cs) in `MicroLIMS.Persistence.Configurations`. `DbSet<MaterialDocument>` in `MicroLimsDbContext`.
3. **Application Service & Validator:**  
   [MaterialDocumentService.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Application/Services/MaterialDocumentService.cs) and `MaterialDocumentFileValidator` in `MicroLIMS.Application.Services`.
4. **API Controller:**  
   [MaterialDocumentController.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.API/Controllers/MaterialDocumentController.cs) (`[Route("api/materials")]`).
5. **Frontend Service & Components:**  
   `materialDocumentService.ts` in `frontend/src/modules/inventory/materials/services/`, `MaterialDocumentsDialog.tsx` in `frontend/src/modules/inventory/materials/`.

---

### 2.2 Audit Trail Infrastructure

#### Entities, Attributes, and Database Mapping
Audit logs are represented by the `AuditLog` entity ([AuditLog.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Domain/Entities/AuditLog.cs)) mapped to table `"AuditLogs"`.

```csharp
namespace MicroLIMS.Domain.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // "Create", "Update", "Delete"
    public string? PreviousValue { get; set; } // Serialized JSON string of old property values
    public string? NewValue { get; set; }      // Serialized JSON string of new property values
    public int UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Cross-reference index columns
    public string? BatchNumber { get; set; }
    public string? ControlNumber { get; set; }
    public string? SampleReferenceNumber { get; set; }
    public string? MediaLotNumber { get; set; }
    public string? ReferenceStrainCode { get; set; }
    public string? CryovialCode { get; set; }
    public int? SampleId { get; set; }
    public int? TestOrderId { get; set; }
}
```

#### How Audit Events Are Raised
Audit logs are generated automatically via EF Core ChangeTracker interception inside `MicroLimsDbContext` ([MicroLimsDbContext.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs#L108-L179)):

```csharp
public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    CaptureAuditEntries();
    return base.SaveChangesAsync(cancellationToken);
}

private void CaptureAuditEntries()
{
    var entries = ChangeTracker.Entries()
        .Where(e => e.Entity is not AuditLog &&
                    e.Entity is not MaterialDocumentAccessLog &&
                    e.Entity is not EquipmentDocumentAccessLog &&
                    (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
        .ToList();

    foreach (var entry in entries)
    {
        // Extracts property diffs into JSON string dictionaries
        if (entry.State == EntityState.Modified) {
            previousValue = JsonSerializer.Serialize(entry.OriginalValues.Properties.ToDictionary(p => p.Name, p => entry.OriginalValues[p]));
            newValue = JsonSerializer.Serialize(entry.CurrentValues.Properties.ToDictionary(p => p.Name, p => entry.CurrentValues[p]));
        }
        AuditLogs.Add(new AuditLog { ... });
    }
}
```

#### Protection Mechanism
- **Application Level:** [AuditService.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Application/Services/AuditService.cs) contains read-only methods (`GetForEntityAsync`, `SearchAsync`). There are no API endpoints or service methods to update or delete audit logs.
- **Database Level:** **NO DATABASE TRIGGERS, RULES, OR RLS POLICIES EXIST.** The database table `"AuditLogs"` is not protected against direct SQL `UPDATE` or `DELETE` statements executed outside EF Core.

#### System-Initiated Actions
If an unauthenticated background task or system operation triggers `DbContext.SaveChangesAsync()`, `CurrentUserId` is `null` and `UserId` is recorded as `0`. In `AuditService.cs`, `UserId = 0` is mapped to display name `"System"`:
```csharp
var name = user?.FullName ?? (l.UserId == 0 ? "System" : $"User #{l.UserId}");
```

#### Querying and Display
Audit logs are queried via `AuditSearchService.cs` and `AuditTraceabilityService.cs`, exposed by [AuditController.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.API/Controllers/AuditController.cs) (`POST /api/admin/audit/search`), and displayed in the frontend via `AuditSearchPage.tsx` and `AuditHistoryDialog.tsx`.

---

### 2.3 Authentication, Session and Electronic Signature

#### Authentication & User Identity Retrieval
Authentication relies on JWT Bearer Tokens issued by `JwtTokenService.cs` ([JwtTokenService.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Infrastructure/Authentication/JwtTokenService.cs)):
```csharp
public string IssueToken(string userId, string role)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, userId),
        new Claim(ClaimTypes.Role, role)
    };
    // Signed via SymmetricSecurityKey (Min 32 chars)
}
```
In API controllers, the authenticated user's ID is retrieved via:
```csharp
private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
```
In EF Core DbContext, [AuditMiddleware.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.API/Middleware/AuditMiddleware.cs) sets `db.CurrentUserId`:
```csharp
if (context.User.Identity?.IsAuthenticated == true)
{
    var idClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (int.TryParse(idClaim, out var userId)) db.CurrentUserId = userId;
}
```

#### Session & Idle Timeout
Session timeout is managed on the frontend by `useIdleTimeout.ts` ([useIdleTimeout.ts](file:///E:/MicroLIMS/MicroLIMS/frontend/src/hooks/useIdleTimeout.ts)), which tracks user events (`mousemove`, `keydown`, `click`) and automatically triggers `logout()` after inactivity. Backend JWT tokens expire according to `TokenValidationParameters` in `Program.cs`.

#### Electronic Signatures
Electronic signatures implement 21 CFR Part 11 compliance using `ElectronicSignature` entity ([ElectronicSignature.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Domain/Entities/ElectronicSignature.cs)) and `ElectronicSignatureService.cs` ([ElectronicSignatureService.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs)):

```csharp
public async Task<ElectronicSignature> SignAsync(
    int userId, string password, SignatureMeaning meaning,
    string entityType, int entityId, string? comment, string? ipAddress)
{
    var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId)
        ?? throw new InvalidOperationException($"User {userId} not found.");

    if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        throw new InvalidOperationException("Invalid password for electronic signature.");

    var signature = new ElectronicSignature
    {
        UserId = userId,
        UserFullNameSnapshot = user.FullName,
        UsernameSnapshot = user.Username,
        RoleSnapshot = user.Role?.Name ?? "Unknown",
        MeaningOfSignature = meaning,
        EntityType = entityType,
        EntityId = entityId,
        SignedAt = DateTime.UtcNow,
        Comment = comment,
        IpAddress = ipAddress
    };

    _db.ElectronicSignatures.Add(signature);
    return signature;
}
```

---

### 2.4 Authorisation and Roles

#### Role Definition and Storage
Roles are defined by the enum `RoleType` ([RoleType.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Domain/Enums/RoleType.cs)) and string constants ([RoleConstants.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Shared/Constants/RoleConstants.cs)):
```csharp
public enum RoleType
{
    SystemAdministrator,
    SectionHead,
    Reviewer,
    Analyst
}
```
Roles are stored in the DB table `"Roles"` and linked to `User` via `User.RoleId` (1:1 relation).

#### Endpoint Permission Enforcement
Endpoints are protected using ASP.NET Core role attributes:
```csharp
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
```
`PermissionService.cs` exists with methods like `HasPermissionAsync(int roleId, string permissionCode)`, but **it is never called by any controller or authorization filter**.

#### Per-Record Authorization
**NOT IMPLEMENTED.** No per-record policy or handler (e.g. checking whether `CurrentUserId` matches `Document.AuthorId`) exists in any API controller or filter.

#### Segregation of Duties (SoD) Enforcement
Enforced in application services via [SegregationOfDutiesGuard.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs):
```csharp
public async Task<bool> DidUserPerformTestAsync(int testOrderId, int userId)
{
    var assignedAnalystId = await _db.TestOrders.Where(t => t.Id == testOrderId).Select(t => t.AssignedAnalystId).FirstOrDefaultAsync();
    if (assignedAnalystId == userId) return true;
    if (await _db.Results.AnyAsync(r => r.TestOrderId == testOrderId && r.EnteredByUserId == userId)) return true;
    return false;
}
```
In `ReviewService.cs` and `ApprovalService.cs`:
```csharp
if (await _segregationOfDuties.DidUserPerformTestAsync(testOrderId, reviewerId))
    throw new InvalidOperationException("You cannot review a test you performed. Review must be done by a different person.");
```

---

### 2.5 File and Document Handling

#### Storage Implementation
File storage is abstracted by `IFileStorageService` ([IFileStorageService.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Application/Interfaces/IFileStorageService.cs)) and implemented by `LocalFileStorageService` ([LocalFileStorageService.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Infrastructure/Storage/LocalFileStorageService.cs)):

```csharp
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    public LocalFileStorageService(string basePath) { _basePath = basePath; }

    public async Task<string> SaveAsync(string storageKey, byte[] content)
    {
        var fullPath = Path.Combine(_basePath, storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content);
        return storageKey;
    }

    public async Task<byte[]> ReadAsync(string storageKey)
    {
        var fullPath = Path.Combine(_basePath, storageKey);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"File key '{storageKey}' not found.");
        return await File.ReadAllBytesAsync(fullPath);
    }
}
```

#### Metadata, Storage Keys, and Integrity Checks
In `MaterialDocumentService.cs`:
- **StorageKey Derivation:** Server-generated path segment: `material-documents/{materialId}/{guid}{ext}`. Client-provided filenames are stored only in `OriginalFileName`.
- **SHA-256 Integrity Verification on Retrieval:**
```csharp
var computedHash = Convert.ToHexString(SHA256.HashData(content));
if (!string.Equals(computedHash, document.ContentSha256, StringComparison.OrdinalIgnoreCase))
{
    _logger.LogError("INTEGRITY FAILURE: MaterialDocument {Id} stored hash {Stored} != computed hash {Computed}.", document.Id, document.ContentSha256, computedHash);
    throw new InvalidOperationException("Document integrity check failed. The file may have been altered.");
}
```

#### PDF Viewing & Generation
- **Generation:** `PdfGenerator.cs` ([PdfGenerator.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Infrastructure/Pdf/PdfGenerator.cs)) uses QuestPDF to generate controlled PDFs.
- **Viewing:** Frontend pages (`SampleReportPage.tsx`) open PDF views in a separate browser tab without app navigation chrome.

---

### 2.6 Configuration and Master Data

#### Master Data Modeling
Master data (e.g. `TestDefinition`, `Organism`, `Item`, `MediaType`, `Specification`) are standard EF Core entities managed via [MasterDataController.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.API/Controllers/MasterDataController.cs).

#### Key/Value Store
**NOT FOUND.** There is no generic key/value configuration table or store in the codebase.

#### Active/Inactive and Soft-Delete Pattern
- `User.cs` has `IsActive` (bool).
- `Media.cs` has `MediaStatus` (`Active`, `Disposed`).
- `MaterialDocument.cs` has `Status` (`Current`, `Superseded`, `Voided`).
- **Soft-Delete (`IsDeleted` boolean): NOT FOUND.** Master data entities use **hard deletion** (`_db.Remove(entity)`), protected by foreign key reference checks before deletion.

---

### 2.7 Persistence Conventions

#### Primary Keys and ID Generation
- `int` primary keys using auto-increment identity columns in PostgreSQL (`UseNpgsql`).

#### Timestamp Conventions
- Generated server-side using `DateTime.UtcNow` in property initializers or service constructors.
- Stored as UTC (`timestamp with time zone` or UTC `timestamp`).
- JSON serialization handled by `UtcDateTimeConverter.cs` ([UtcDateTimeConverter.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.API/Json/UtcDateTimeConverters.cs)).

#### Migration Workflow
EF Core migrations located in `MicroLIMS.Persistence/Migrations`. Migrations applied automatically on app startup in `Program.cs` when `APPLY_MIGRATIONS=true` or in development mode (`db.Database.Migrate()`).

#### DB Triggers, Sequences, and Check Constraints
**NONE FOUND.** No custom PostgreSQL triggers, sequences, or raw SQL check constraints are configured in `OnModelCreating`.

---

### 2.8 Frontend Conventions

#### Routing & Navigation
- Routes declared in [AppRoutes.tsx](file:///E:/MicroLIMS/MicroLIMS/frontend/src/routes/AppRoutes.tsx) using `react-router-dom` 6.
- Sidebar menu constructed by `menuConfig.ts` ([menuConfig.ts](file:///E:/MicroLIMS/MicroLIMS/frontend/src/routes/menuConfig.ts)) based on current user role string.

#### Shared Components
- Data Grids: `DataTable.tsx` ([DataTable.tsx](file:///E:/MicroLIMS/MicroLIMS/frontend/src/components/DataTable.tsx))
- Headers & Badges: `PageHeader.tsx`, `StatusBadge.tsx`, `SearchBar.tsx`, `Toolbar.tsx`
- Dialogs: `ConfirmationDialog.tsx`, `FloatingDialog.tsx`, `SignatureDialog.tsx`, `AuditHistoryDialog.tsx`

#### API Calls & Error Handling
- `apiClient.ts` ([apiClient.ts](file:///E:/MicroLIMS/MicroLIMS/frontend/src/services/apiClient.ts)): Axios instance with Bearer token interceptor.
- Responses wrapped in `ApiResponse<T>`: `{ success: boolean, data: T, message?: string, errors?: string[] }`.

---

### 2.9 Testing

#### Test Projects and Coverage
- **`MicroLIMS.Tests`**: xUnit testing project covering:
  - Workflow Engines (`TestWorkflowEngineTests`, `ProductWorkflowEngineTests`)
  - Electronic Signatures ([ElectronicSignatureTests.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Tests/WorkflowTests/ElectronicSignatureTests.cs))
  - Segregation of Duties ([SegregationOfDutiesTests.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Tests/WorkflowTests/SegregationOfDutiesTests.cs))
  - Material Document Management ([MaterialDocumentTests.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Tests/MaterialDocumentTests.cs))

#### Pattern for Testing Services that Write Audit Records
[TestServiceFactory.cs](file:///E:/MicroLIMS/MicroLIMS/backend/MicroLIMS.Tests/TestServiceFactory.cs) builds an in-memory `MicroLimsDbContext`, executes service operations, calls `db.SaveChangesAsync()`, and asserts directly against `db.AuditLogs.ToList()`:

```csharp
var db = TestServiceFactory.CreateInMemoryDbContext();
var service = new MaterialDocumentService(db, storageMock.Object, validator, logger);

await service.UploadAsync(materialId, request, userId);

var auditLog = db.AuditLogs.FirstOrDefault(a => a.EntityName == nameof(MaterialDocument));
Assert.NotNull(auditLog);
Assert.Equal("Create", auditLog.Action);
```

---

## 3. Reuse Register

| Infrastructure Capability | Implementation Found? | Location (File Paths / Classes) | Reusability Assessment |
| :--- | :--- | :--- | :--- |
| **Audit Trail (EF Core)** | **Yes** | `MicroLimsDbContext.cs`, `AuditLog.cs`, `AuditService.cs` | **Directly Reusable.** Automatically logs EF entity diffs to `AuditLog`. |
| **Audit Protection (DB Level)** | **No** | N/A | **Absent.** No DB-level triggers/privileges protect `AuditLogs` from `UPDATE`/`DELETE`. |
| **Authentication** | **Yes** | `JwtTokenService.cs`, `AuthenticationService.cs`, `AuditMiddleware.cs` | **Directly Reusable.** Uses standard JWT Bearer token & `CurrentUserId` on DbContext. |
| **Electronic Signature** | **Yes** | `ElectronicSignature.cs`, `ElectronicSignatureService.cs`, `ReviewGateService.cs` | **Directly Reusable.** Part 11 append-only e-signatures with BCrypt password re-auth. |
| **Global Authorisation** | **Yes** | `RoleConstants.cs`, `[Authorize(Roles = "...")]` | **Directly Reusable.** Coarse role checks on endpoints. |
| **Per-Document Authorisation**| **No** | N/A | **Absent.** Must be implemented inside Document Control domain/application logic. |
| **Segregation of Duties** | **Yes** | `SegregationOfDutiesGuard.cs` | **Reusable with Extension.** Add document author/approver checks to SoD guard. |
| **File Storage** | **Yes** | `IFileStorageService.cs`, `LocalFileStorageService.cs` | **Directly Reusable.** File I/O abstraction saving to `./storage`. |
| **SHA-256 Hash Verification**| **Yes** | `MaterialDocumentService.cs` (`GetContentAsync`), `ArchivedRecordsController.cs` | **Directly Reusable.** Exact SHA-256 calculation & retrieval verification pattern. |
| **Document Lifecycle / Supersession**| **Yes** | `MaterialDocument.cs`, `MaterialDocumentService.cs` | **Directly Reusable Blueprint.** Status enum (`Current`, `Superseded`, `Voided`), `SupersededByDocumentId`, `VoidReason`. |
| **Configuration (Generic K/V)**| **No** | N/A | **Absent.** No key/value configuration table exists in the codebase. |
| **Timestamps (UTC Server)** | **Yes** | `DateTime.UtcNow`, `UtcDateTimeConverter.cs` | **Directly Reusable.** Server-side UTC timestamps across entities. |
| **Soft Delete / Voiding** | **Yes** | `MaterialDocument.cs` (`Status = Voided`, `VoidReason`) | **Directly Reusable Pattern.** Physical files retained, record status set to Voided. |

---

## 4. Gaps (Scope for WP1)

The following capabilities required by the Document Control module **do not currently exist** in the repository and must be built as part of Work Package 1 (WP1):

1. **Database-Level Immutable Audit Trail Protection:**  
   No PostgreSQL database triggers or DB user privilege restrictions currently block `UPDATE` or `DELETE` operations on the `AuditLogs` or `ElectronicSignatures` tables.
2. **Controlled Document Domain Entities & Metadata Schema:**  
   Entities for Controlled Documents, Document Types, Document Lifecycle Workflows, Document Change Requests (DCR), and Document Periodic Reviews do not exist.
3. **Per-Document Workflow Assignment Authorization:**  
   No mechanism exists to evaluate whether the current user is specifically assigned as the Author, Reviewer, or Approver for a *particular* document instance.
4. **Generic System Configuration Store:**  
   No key/value app configuration store exists to configure Document Control module settings (e.g. periodic review interval defaults, document numbering templates).
5. **Controlled Document PDF Watermarking (Draft/Effective/Obsolete):**  
   While `PdfGenerator.cs` generates sample reports, it does not support dynamic background watermarking ("DRAFT", "EFFECTIVE", "OBSOLETE") or header/footer controlled distribution stamps.

---

## 5. Conventions to Follow

Any code written for the Document Control module must strictly adhere to the established project patterns:

1. **Clean Architecture Boundary:**  
   - Place entities in `MicroLIMS.Domain/Entities/`.
   - Place enums in `MicroLIMS.Domain/Enums/`.
   - Place DTOs and contracts in `MicroLIMS.Shared/DTOs/` or `MicroLIMS.Application/DTOs/`.
   - Place business logic, state transitions, and file validation in `MicroLIMS.Application/Services/`.
   - Place EF Core configurations in `MicroLIMS.Persistence/Configurations/`.
   - Keep controllers thin in `MicroLIMS.API/Controllers/` using `ApiResponse<T>`.

2. **Controlled Document Lifecycle & Voiding:**  
   - Do **NOT** add an `IsDeleted` boolean property.
   - Use a `Status` enum (`Draft`, `UnderReview`, `Approved`, `Effective`, `Superseded`, `Voided`).
   - Requiring a non-empty `Reason` string for all cancellation, voiding, or supersession operations.
   - Retain physical files on disk when a document is voided or superseded.

3. **File Storage & Cryptographic Verification:**  
   - Inject `IFileStorageService` into services; never perform direct `System.IO.File` operations in controllers.
   - Generate `StorageKey` on the server (e.g. `documents/{id}/{guid}.pdf`); never use user-uploaded filenames as storage paths.
   - Calculate SHA-256 hash at upload (`SHA256.HashData`), store in `ContentSha256`, and verify hash on every file retrieval.

4. **Electronic Signatures & SoD:**  
   - Route all formal review/approval transitions through `ElectronicSignatureService.SignAsync` or `ReviewGateService.SignAndLogAsync`.
   - Always require password re-authentication (`BCrypt.Verify`).
   - Enforce that a document's author cannot review or approve their own document.

5. **Timestamps & Audit Trail:**  
   - Use `DateTime.UtcNow` for all created/modified timestamps.
   - Rely on `MicroLimsDbContext.SaveChangesAsync()` for automatic EF Core audit capturing.

---

## 6. Risks and Concerns

| Risk / Concern Area | Description & Requirement Conflict | Severity | Mitigation Strategy |
| :--- | :--- | :--- | :--- |
| **1. Database-Level Audit Protection Conflict** | Requirement states: *"Audit records must be append-only and protected at the persistence layer, not by application logic alone."* Currently, `AuditLogs` table has **no database triggers or DB user restrictions**. | **HIGH** | Write a database migration adding PostgreSQL triggers or rule definitions that block `UPDATE` and `DELETE` on `AuditLogs` and `ElectronicSignatures`. |
| **2. Coarse Role Attributes vs Per-Document Assignments** | Requirement states: *"Workflow authority must be evaluated from both a global role and a per-document assignment."* Controller `[Authorize(Roles = "...")]` attributes cannot evaluate per-document assignments. | **HIGH** | Implement per-document assignment validation inside `DocumentControlService` methods before executing state transitions or signatures. |
| **3. Lack of Audit Reason for Edits in DbContext** | `MicroLimsDbContext.CaptureAuditEntries()` automatically records JSON state diffs, but does not capture a user-prompted "Reason for Change" for generic edits. | **MEDIUM** | Extend audit capture mechanism or pass `Reason` explicitly in service methods for controlled document edits. |
| **4. Hard Deletion Pattern in Master Data Controllers** | Several existing master data controllers use `_db.Remove(entity)`. If Document Control master data (e.g. Document Types) uses hard deletion, audit trails and historical references will break. | **MEDIUM** | Enforce soft deletion / voiding pattern strictly for all Document Control entities. |

---
*Report generated via read-only inspection of the MicroLIMS codebase.*
