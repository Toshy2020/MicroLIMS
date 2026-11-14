using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.MyWork.Sources;

// Unassigned tests the caller may take: holders of Tests.SelfAssign, only in sections they are a member of
// (membership, not read access: Take is a write), and only while workflow.selfAssignment.enabled is on.
public sealed class AvailableTestsSource : UnassignedTestsSourceBase
{
    private readonly ISystemSettingsReader _settings;

    public AvailableTestsSource(IMicroLimsDbContext db, ISystemSettingsReader settings) : base(db) => _settings = settings;

    public override string Key => "availableTests";
    protected override string GroupKey => "available";
    protected override string Verb => "Take";
    protected override string Action => "take";
    protected override string TargetType => "test-step";
    protected override bool Holds(MyWorkContext ctx) => ctx.Permissions.Contains(PermissionConstants.TestsSelfAssign);
    protected override Task<bool> EnabledAsync(CancellationToken ct) => _settings.GetBoolAsync(SystemSettingKeys.SelfAssignmentEnabled, ct);

    protected override IQueryable<TestOrder> InScope(IQueryable<TestOrder> query, MyWorkContext ctx)
    {
        var sections = ctx.MemberSectionIds.ToArray();
        return query.Where(t => sections.Contains(t.SectionId));
    }
}
