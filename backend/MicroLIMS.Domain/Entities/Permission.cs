namespace MicroLIMS.Domain.Entities;

// Fine-grained permission a Role can be granted, e.g. "samples.receive",
// "results.approve". Kept separate from RoleType so System Administration
// and Laboratory Administration can be composed independently (Frozen
// Principle #4 - Role separation).
public class Permission
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    // True once some controller actually checks this permission via
    // [Authorize(Policy=<Code>)]. Every permission is legitimately false
    // today - Phase 1 wired the mechanism but deliberately left every
    // existing [Authorize(Roles=...)] attribute untouched. Flip to true
    // by hand as part of migrating a controller, so the frontend's
    // Enforced/Legacy-only indicator stays correct with no separate
    // bookkeeping.
    public bool IsEnforced { get; set; }
}
