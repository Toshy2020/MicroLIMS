using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.MyWork.Sources;

// Unassigned tests the caller may take: holders of Tests.SelfAssign, only in sections they are a member of
// (membership, not read access: Take is a write), excluding sections where self-assignment is switched off.
public sealed class AvailableTestsSource : UnassignedTestsSourceBase
{
    public AvailableTestsSource(IMicroLimsDbContext db) : base(db) { }

    public override string Key => "availableTests";
    protected override string GroupKey => "available";
    protected override string Verb => "Take";
    protected override string Action => "take";
    protected override string TargetType => "test-step";
    protected override bool Holds(MyWorkContext ctx) => ctx.Permissions.Contains(PermissionConstants.TestsSelfAssign);

    protected override IQueryable<TestOrder> InScope(IQueryable<TestOrder> query, MyWorkContext ctx)
    {
        var sections = ctx.MemberSectionIds.ToArray();
        var disabled = SectionSelfAssignmentService.DisabledSectionIds(_db);
        return query
            .Where(t => sections.Contains(t.SectionId) && !disabled.Contains(t.SectionId))
            .InPhyschemArea(ctx, t => t.SectionId, t => t.Sample!.Category);
    }
}
