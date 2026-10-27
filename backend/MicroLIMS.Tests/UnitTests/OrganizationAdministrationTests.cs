using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Persistence.Seed;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The organisation (departments, sections, lab memberships) decides who sees and acts on which documents, so
// every change carries a stated reason that reaches AuditLog.Reason, and a department or section that is in use
// is deactivated - never deleted - and only knowingly.
public class OrganizationAdministrationTests
{
    private readonly MicroLimsDbContext _db;
    private readonly DocumentConfigurationService _config;
    private readonly LaboratoryOrganizationService _org;
    private readonly DocumentMasterService _master;
    private readonly User _admin, _analyst, _member, _inactiveMember, _owner;
    private readonly int _deptId, _sectionId, _typeId;

    public OrganizationAdministrationTests()
    {
        _db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var seq = new DatabaseSequenceHelper(_db);
        var audit = new AuditEventService(_db, seq);
        var auth = new DocumentAuthorizationService(_db);
        _config = new DocumentConfigurationService(_db, audit, auth);
        _org = new LaboratoryOrganizationService(_db, new UserSectionScopeService(_db));
        _master = new DocumentMasterService(_db, seq, audit, auth);

        var adminRole = new Role { Type = RoleType.SystemAdministrator, Name = "Admin", IsSystemRole = true, IsActive = true };
        var headRole = new Role { Type = RoleType.SectionHead, Name = "Section Head", IsSystemRole = true, IsActive = true };
        var analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsSystemRole = true, IsActive = true };
        _db.Roles.AddRange(adminRole, headRole, analystRole);
        _db.SaveChanges();
        DbSeeder.SeedPermissionsAndGrants(_db);

        User U(string n, Role r, bool active = true) => new() { Username = n, FullName = n, Email = $"{n}@lims.local", PasswordHash = "x", RoleId = r.Id, IsActive = active };
        _admin = U("admin", adminRole);
        _analyst = U("analyst", analystRole);
        _member = U("member", analystRole);
        _inactiveMember = U("leaver", analystRole, active: false);
        _owner = U("owner", headRole);
        _db.Users.AddRange(_admin, _analyst, _member, _inactiveMember, _owner);
        var type = new DocumentType { Code = "SOP", Name = "SOP", DefaultReviewCycleMonths = 24, IsActive = true };
        var dept = new DocumentDepartment { Code = "QC", Name = "Quality Control", IsActive = true };
        _db.DocumentTypes.Add(type);
        _db.DocumentDepartments.Add(dept);
        _db.SaveChanges();
        var section = new DocumentSection { DepartmentId = dept.Id, Name = "Micro", IsActive = true };
        _db.DocumentSections.Add(section);
        _db.SaveChanges();
        (_deptId, _sectionId, _typeId) = (dept.Id, section.Id, type.Id);
    }

    private async Task<DocumentMaster> AddDocumentAsync(DocumentRevisionStatus? revision = DocumentRevisionStatus.Draft)
    {
        var m = new DocumentMaster
        {
            MicroLimsDocumentId = "DOC-1", CompanyDocumentCode = "SOP-1", Title = "T", DocumentTypeId = _typeId, DepartmentId = _deptId, SectionId = _sectionId,
            DocumentOwnerUserId = _owner.Id, CreatedByUserId = _owner.Id, Confidentiality = DocumentConfidentiality.Internal
        };
        _db.DocumentMasters.Add(m);
        await _db.SaveChangesAsync();
        _db.DocumentMasterSections.Add(new DocumentMasterSection { DocumentMasterId = m.Id, SectionId = _sectionId });
        if (revision is { } status)
            _db.DocumentRevisions.Add(new DocumentRevision { DocumentMasterId = m.Id, RevisionNumber = "01", RevisionSequence = 1, RevisionStatus = status, CreatedByUserId = _owner.Id });
        await _db.SaveChangesAsync();
        return m;
    }

    private async Task AddMembershipAsync(User user, int? sectionId = null)
    {
        _db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = _deptId, SectionId = sectionId });
        await _db.SaveChangesAsync();
    }

    // ---- a reason on every change ---------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAndUpdate_OfDepartmentsAndSections_RequireAReason(string? reason)
    {
        var dept = await _db.DocumentDepartments.AsNoTracking().SingleAsync();
        var section = await _db.DocumentSections.AsNoTracking().SingleAsync();

        await AssertReasonRequired(() => _config.CreateDepartmentAsync(new CreateDocumentDepartmentRequest("NEW", "New", reason), _admin.Id));
        await AssertReasonRequired(() => _config.UpdateDepartmentAsync(dept.Id, new UpdateDocumentDepartmentRequest("Renamed", true, reason), _admin.Id));
        await AssertReasonRequired(() => _config.CreateSectionAsync(new CreateDocumentSectionRequest(dept.Id, "Chem", reason), _admin.Id));
        await AssertReasonRequired(() => _config.UpdateSectionAsync(section.Id, new UpdateDocumentSectionRequest("Renamed", true, reason), _admin.Id));

        Assert.Equal(1, await _db.DocumentDepartments.CountAsync());
        Assert.Equal("Quality Control", (await _db.DocumentDepartments.AsNoTracking().SingleAsync()).Name);
    }

    private static async Task AssertReasonRequired(Func<Task> act)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("reason", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheReason_ReachesTheAuditTrail()
    {
        var created = await _config.CreateDepartmentAsync(new CreateDocumentDepartmentRequest("pd", "Production", " New plant opened "), _admin.Id);
        await _config.UpdateDepartmentAsync(created.Id, new UpdateDocumentDepartmentRequest("Production Ops", true, "Renamed after reorganisation"), _admin.Id);
        var section = await _config.CreateSectionAsync(new CreateDocumentSectionRequest(created.Id, "Packing", "Packing line added"), _admin.Id);
        await _config.UpdateSectionAsync(section.Id, new UpdateDocumentSectionRequest("Packing A", true, "Line renamed"), _admin.Id);

        var reasons = await _db.AuditLogs.AsNoTracking().Where(a => a.ActionCode != null && a.ActionCode.StartsWith("Document") && a.Reason != null)
            .ToDictionaryAsync(a => a.ActionCode!, a => a.Reason!);
        Assert.Equal("New plant opened", reasons["DocumentDepartmentCreated"]);
        Assert.Equal("Renamed after reorganisation", reasons["DocumentDepartmentUpdated"]);
        Assert.Equal("Packing line added", reasons["DocumentSectionCreated"]);
        Assert.Equal("Line renamed", reasons["DocumentSectionUpdated"]);
        // ...and on the entity-level audit rows the DbContext writes (AuditLog.Reason via CurrentReason).
        Assert.Contains(_db.AuditLogs, a => a.EntityName == nameof(DocumentDepartment) && a.Reason == "Renamed after reorganisation");
    }

    [Fact]
    public async Task OnlyConfigManageHoldersChangeTheOrganisation()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _config.CreateDepartmentAsync(new CreateDocumentDepartmentRequest("X", "X", "because"), _analyst.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _config.GetDepartmentImpactAsync(_deptId, _analyst.Id));
    }

    [Fact]
    public async Task MembershipReplace_RequiresAReason_WhichReachesTheAuditTrail()
    {
        var wanted = new[] { new UserOrgMembershipDto(_deptId, null) };
        await AssertReasonRequired(() => _org.ReplaceMembershipsAsync(_member.Id, wanted, null, _admin.Id));
        await AssertReasonRequired(() => _org.ReplaceMembershipsAsync(_member.Id, wanted, "  ", _admin.Id));
        Assert.Empty(_db.UserOrgMemberships);

        await _org.ReplaceMembershipsAsync(_member.Id, wanted, "Moved to Quality Control", _admin.Id);

        Assert.Single(_db.UserOrgMemberships);
        Assert.Contains(_db.AuditLogs, a => a.EntityName == nameof(UserOrgMembership) && a.Reason == "Moved to Quality Control");
    }

    // ---- deactivate, never delete ------------------------------------------------------------------

    [Fact]
    public void ThereIsNoDelete_ADepartmentOrSectionInUseCanOnlyBeDeactivated()
    {
        var methods = typeof(IDocumentConfigurationService).GetMethods().Select(m => m.Name)
            .Concat(typeof(LaboratoryOrganizationService).GetMethods().Select(m => m.Name));
        Assert.DoesNotContain(methods, n => n.Contains("Delete", StringComparison.OrdinalIgnoreCase) || n.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Impact_CountsActiveDocuments_ActiveMembers_AndOpenWork()
    {
        await AddDocumentAsync(DocumentRevisionStatus.InReview);
        await AddMembershipAsync(_member);
        await AddMembershipAsync(_inactiveMember); // kept for history, not an active member

        var dept = await _config.GetDepartmentImpactAsync(_deptId, _admin.Id);
        Assert.Equal(new OrganizationImpactDto(1, 1, 1), dept);
        Assert.True(dept.HasReferences);

        var section = await _config.GetSectionImpactAsync(_sectionId, _admin.Id);
        Assert.Equal(1, section.ActiveDocuments);
        Assert.Equal(0, section.ActiveMemberships); // the member belongs to the whole department, not to this section
        Assert.Equal(1, section.OpenWork);
    }

    [Fact]
    public async Task Impact_IgnoresVoidDocumentsAndFinishedRevisions()
    {
        var voided = await AddDocumentAsync(DocumentRevisionStatus.Effective);
        voided.RecordStatus = DocumentRecordStatus.Void;
        await _db.SaveChangesAsync();
        Assert.False((await _config.GetDepartmentImpactAsync(_deptId, _admin.Id)).HasReferences);
    }

    [Fact]
    public async Task Deactivating_ADepartmentInUse_NeedsConfirmation_AndNamesTheCounts()
    {
        await AddDocumentAsync();
        await AddMembershipAsync(_member);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _config.UpdateDepartmentAsync(_deptId, new UpdateDocumentDepartmentRequest("Quality Control", false, "Closing down"), _admin.Id));
        Assert.Contains("1 active document(s), 1 active member(s) and 1 open work item(s)", ex.Message);
        Assert.True((await _db.DocumentDepartments.AsNoTracking().SingleAsync()).IsActive);

        var done = await _config.UpdateDepartmentAsync(_deptId, new UpdateDocumentDepartmentRequest("Quality Control", false, "Closing down", ConfirmDeactivation: true), _admin.Id);
        Assert.False(done.IsActive);
        // Nothing the department owned was deleted or moved.
        Assert.Equal(1, await _db.DocumentMasters.CountAsync(m => m.DepartmentId == _deptId));
        Assert.Equal(1, await _db.UserOrgMemberships.CountAsync(m => m.DepartmentId == _deptId));
    }

    [Fact]
    public async Task Deactivating_ASectionInUse_NeedsConfirmation()
    {
        await AddDocumentAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _config.UpdateSectionAsync(_sectionId, new UpdateDocumentSectionRequest("Micro", false, "No longer used"), _admin.Id));
        Assert.Contains("section", ex.Message);
        Assert.Contains("1 active document(s)", ex.Message);

        var done = await _config.UpdateSectionAsync(_sectionId, new UpdateDocumentSectionRequest("Micro", false, "No longer used", ConfirmDeactivation: true), _admin.Id);
        Assert.False(done.IsActive);
    }

    [Fact]
    public async Task Deactivating_AnUnusedDepartmentOrSection_NeedsNoConfirmation_AndReactivatingNeverDoes()
    {
        var section = await _config.UpdateSectionAsync(_sectionId, new UpdateDocumentSectionRequest("Micro", false, "Unused"), _admin.Id);
        Assert.False(section.IsActive);
        var dept = await _config.UpdateDepartmentAsync(_deptId, new UpdateDocumentDepartmentRequest("Quality Control", false, "Unused"), _admin.Id);
        Assert.False(dept.IsActive);

        Assert.True((await _config.UpdateDepartmentAsync(_deptId, new UpdateDocumentDepartmentRequest("Quality Control", true, "Reopened"), _admin.Id)).IsActive);
    }

    [Fact]
    public async Task Renaming_ADepartmentInUse_IsNotAnImpact()
    {
        await AddDocumentAsync();
        var renamed = await _config.UpdateDepartmentAsync(_deptId, new UpdateDocumentDepartmentRequest("QC Lab", true, "Naming"), _admin.Id);
        Assert.Equal("QC Lab", renamed.Name);
    }

    // ---- editing a document whose custodian was deactivated ----------------------------------------

    [Fact]
    public async Task ADocumentWhoseCustodianWasDeactivated_StaysEditable_WithTheCustodianUnchanged()
    {
        var doc = await AddDocumentAsync();
        _owner.IsActive = false;
        var editor = new User { Username = "head", FullName = "head", PasswordHash = "x", RoleId = (await _db.Roles.SingleAsync(r => r.Type == RoleType.SectionHead)).Id, IsActive = true };
        _db.Users.Add(editor);
        await _db.SaveChangesAsync();

        var updated = await _master.UpdateDraftMetadataAsync(doc.Id,
            new UpdateDocumentMasterDraftRequest("SOP-1", "A better title", _typeId, _deptId, _sectionId, _owner.Id, DocumentConfidentiality.Internal), editor.Id);
        Assert.Equal("A better title", updated.Title);

        // Changing the custodian TO an inactive user is still refused; hand-over targets are checked by the reassignment rules.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _master.UpdateDraftMetadataAsync(doc.Id,
            new UpdateDocumentMasterDraftRequest("SOP-1", "A better title", _typeId, _deptId, _sectionId, _inactiveMember.Id, DocumentConfidentiality.Internal), editor.Id));
        Assert.Contains("inactive", ex.Message);
    }
}
