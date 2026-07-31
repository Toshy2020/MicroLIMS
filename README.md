# MicroLIMS v2

GMP-compliant Laboratory Information Management System for a pharmaceutical
microbiology laboratory. Rebuilt from the `MicroLIMS_v2` architecture
document after the original project data was lost — **back this up
immediately** (git remote, or synced cloud storage).

## Structure

```
MicroLIMS/
├── backend/       # ASP.NET Core, Clean Architecture (API/Application/Domain/Infrastructure/Persistence/Shared/Tests)
├── frontend/       # React + TypeScript + Material UI
├── database/       # ERD, backups, raw SQL, seed data
├── documents/      # URS, Functional/Design Specs, Validation, SOPs, User Manual, Screenshots
├── scripts/        # backup, restore, migration, deployment scripts
└── README.md
```

See `backend/README.md` and `frontend/README.md` for how to run each half.

## Guiding Design Principles (Frozen)

1. **Configuration drives behavior.** Section Head configuration determines workflows and test orders.
2. **Workflow before implementation.** Every feature is discussed, prototyped, approved, and frozen before coding.
3. **Backend owns laboratory logic.** The frontend never implements GMP or microbiology rules.
4. **Role separation.** System administration and laboratory administration remain separate.
5. **Traceability.** Every action is auditable.
6. **Consistency.** Product, Water, EM, and After Cleaning share common architectural principles while keeping domain-specific workflows.
7. **Automation.** Repetitive laboratory tasks are automated wherever possible.

## How everything connects

```
React Frontend
    ↓
REST API Controllers      (MicroLIMS.API)
    ↓
Application Services       (MicroLIMS.Application/Services)
    ↓
Workflow Engine             (MicroLIMS.Application/Workflows)
    ↓
Repositories                (MicroLIMS.Persistence/Repositories)
    ↓
Entity Framework Core       (MicroLIMS.Persistence/DbContext)
    ↓
PostgreSQL
```
