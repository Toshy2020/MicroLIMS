---
description: Delegate heavy scaffolding, complex migrations, or multi-file features to Antigravity CLI (agy)
argument-hint: [task description]
---

Delegate the following task to Antigravity CLI (`agy`):

$ARGUMENTS

### Execution Instructions
Execute this task by invoking `agy` in your terminal:

```bash
agy -p "$ARGUMENTS"
```

### Delegation Guidelines
- **Scaffolding:** Multi-file features, full Clean Architecture slices, DTOs, controllers, and React UI components.
- **Testing:** Unit test suites (xUnit, Moq), integration tests, frontend test coverage (Vitest/React Testing Library).
- **Database:** EF Core migrations, database schemas, and seed data scripts.
- **Verification:** After `agy` completes, review changes and run `dotnet build` or `npm test` to verify.
