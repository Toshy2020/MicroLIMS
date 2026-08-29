---
description: Trigger an autonomous code review, quality check, and test verification with Antigravity CLI (agy)
argument-hint: [scope or diff to review]
---

Perform a comprehensive code review and quality verification using Antigravity CLI (`agy`):

$ARGUMENTS

### Execution Instructions
Execute this review by invoking `agy` in your terminal:

```bash
agy -p "Review code changes for GMP compliance, Clean Architecture patterns, error handling, security, and test coverage: $ARGUMENTS"
```

### Review Criteria
- **Clean Architecture Adherence:** Verify boundaries (Domain -> Application -> Persistence / Infrastructure -> API).
- **GMP & 21 CFR Part 11 Compliance:** Verify audit trails, electronic signatures, and data integrity safeguards.
- **Business Logic Placement:** Ensure laboratory logic remains strictly in backend Application services, never in frontend UI.
- **Robustness & Tests:** Verify exception handling, validation filters, and unit/integration test coverage.
- **Summary:** Categorize findings into Critical, Warning, and Suggestion.
