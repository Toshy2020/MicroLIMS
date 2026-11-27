using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers.Quality;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// The change-control detail lists the affected records with the Unchanged flag the close step uses, and
// GET quality/config/templates/{type} answers success with null data when no checklist exists.
[Collection("PostgresDatabaseCollection")]
public sealed class QualityChangeControlAffectedPostgresTests(PostgresTestFixture fixture)
{
    private static readonly DateTime Now = new(2087, 4, 2, 9, 0, 0, DateTimeKind.Utc);

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

    private sealed record World(int Head, int OtherHead, int SectionA, int RecordId, string Number,
        int SpecLink, int TestLink, int NewCodeLink, int ItemLink, int RestrictedSpecLink, int RestrictedTestLink,
        int PlainLink, int SpecId, int TestId, string TestCode, string RestrictedCode, int ItemId);

    private static readonly TimeProvider Clock = new FixedClock(Now);

    private MicroLimsDbContext Context(int? actor = null)
    {
        var db = fixture.CreateDbContext(Clock);
        db.CurrentUserId = actor;
        return db;
    }

    private static async Task<(int UserId, int SectionId)> HeadAsync(MicroLimsDbContext db, string suffix)
    {
        var dept = new DocumentDepartment { Code = $"D-{suffix}", Name = $"Dept {suffix}", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();
        var section = new DocumentSection { DepartmentId = dept.Id, Code = $"S-{suffix}", Name = $"Section {suffix}", IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();
        var key = $"{suffix}-{Guid.NewGuid():N}";
        var user = new User
        {
            FullName = $"Head {suffix}", Username = key, IsActive = true,
            Role = new Role { Name = key, Type = RoleType.SectionHead, IsActive = true },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Affected-pw", 4)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var codes = new[] { PermissionConstants.MasterDataManage, PermissionConstants.QualityView };
        var ids = await db.Permissions.AsNoTracking().Where(p => codes.Contains(p.Code)).Select(p => p.Id).ToListAsync();
        db.RolePermissions.AddRange(ids.Select(id => new RolePermission { RoleId = user.RoleId, PermissionId = id }));
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, SectionId = section.Id, DepartmentId = dept.Id });
        await db.SaveChangesAsync();
        return (user.Id, section.Id);
    }

    private async Task<World> ArrangeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var db = Context();
        var (head, sectionA) = await HeadAsync(db, "a" + suffix);
        var (otherHead, sectionB) = await HeadAsync(db, "b" + suffix);
        db.QualitySectionSettings.Add(new QualitySectionSetting { SectionId = sectionA, ChangeControlRequired = true });

        var item = new Item { Code = "ITM-" + suffix, Name = "Affected item", IsActive = true };
        db.Items.Add(item);
        var codeA = "TST-" + suffix.ToUpperInvariant();
        var codeB = "TSB-" + suffix.ToUpperInvariant();
        var testA = new TestDefinition { Code = codeA, DisplayName = "Affected test", SectionId = sectionA, IsActive = true };
        var testB = new TestDefinition { Code = codeB, DisplayName = "Restricted test", SectionId = sectionB, IsActive = true };
        db.TestDefinitions.AddRange(testA, testB);
        await db.SaveChangesAsync();
        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = codeA });

        Specification Spec(string code, string parameter) => new()
        {
            ItemId = item.Id, TestCode = code, ParameterName = parameter, LimitType = LimitType.Qualitative,
            ExpectedResultText = "Clear", SpecLimit = "Clear"
        };
        var specA = Spec(codeA, "ParamA");
        var specB = Spec(codeB, "ParamB");
        db.Specifications.AddRange(specA, specB);

        var number = "CC-2087-" + suffix[..4].ToUpperInvariant();
        var record = new QualityRecord
        {
            Number = number, Type = QualityRecordType.ChangeControl, Title = "Affected CC", Description = "d",
            SectionId = sectionA, RaisedByUserId = head, OwnerUserId = head, RaisedAtUtc = Now
        };
        db.QualityRecords.Add(record);
        await db.SaveChangesAsync();
        db.ChangeControlDetails.Add(new ChangeControlDetail
        {
            RecordId = record.Id, Status = ChangeControlStatus.ApprovedForImplementation, Reason = "Affected"
        });

        QualityRecordLink Link(QualityLinkTargetType type, int? id, string? code = null, bool affected = true) => new()
        {
            RecordId = record.Id, TargetType = type, TargetId = id, NewRecordCode = code,
            IsAffectedByChange = affected, AddedByUserId = head, AddedAtUtc = Now
        };
        var specLink = Link(QualityLinkTargetType.Specification, specA.Id);
        var testLink = Link(QualityLinkTargetType.TestDefinition, testA.Id);
        var newCodeLink = Link(QualityLinkTargetType.TestDefinition, null, "NEW-" + suffix);
        var itemLink = Link(QualityLinkTargetType.Item, item.Id);
        var restrictedSpec = Link(QualityLinkTargetType.Specification, specB.Id);
        var restrictedTest = Link(QualityLinkTargetType.TestDefinition, testB.Id);
        var plain = Link(QualityLinkTargetType.Specification, specB.Id + 100000, affected: false);
        plain.TargetId = null;
        plain.NewRecordCode = "PLAIN";
        db.QualityRecordLinks.AddRange(specLink, testLink, newCodeLink, itemLink, restrictedSpec, restrictedTest, plain);
        await db.SaveChangesAsync();
        return new World(head, otherHead, sectionA, record.Id, number, specLink.Id, testLink.Id, newCodeLink.Id,
            itemLink.Id, restrictedSpec.Id, restrictedTest.Id, plain.Id, specA.Id, testA.Id, codeA, codeB, item.Id);
    }

    private async Task<QualityChangeControlDetailDto> DetailAsync(World w, int userId)
    {
        await using var db = Context(userId);
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, Clock),
            new ElectronicSignatureService(db, Clock), Clock);
        var links = new QualityLinkService(db, core, permissions, scope, Clock);
        var detail = await new QualityQueryService(db, core, links, new LabClock(Clock, TimeZoneInfo.Utc))
            .GetDetailAsync(w.RecordId, userId);
        return Assert.IsType<QualityChangeControlDetailDto>(detail.ChangeControl);
    }

    [PostgresFact]
    public async Task Affected_list_labels_restricted_records_by_code_only_and_flags_unchanged_gated_targets()
    {
        var w = await ArrangeAsync();
        var cc = await DetailAsync(w, w.Head);

        Assert.Equal(6, cc.Affected.Count);
        Assert.DoesNotContain(cc.Affected, a => a.LinkId == w.PlainLink);

        var spec = Assert.Single(cc.Affected, a => a.LinkId == w.SpecLink);
        Assert.Equal(QualityLinkTargetType.Specification, spec.TargetType);
        Assert.Equal(w.SpecId, spec.TargetId);
        Assert.Equal(w.TestCode, spec.Number);
        Assert.Equal("ParamA", spec.Label);
        Assert.False(spec.IsRestricted);
        Assert.True(spec.Unchanged);
        Assert.Null(spec.ChangedAtUtc);
        Assert.Null(spec.ChangeAuditLogId);

        var test = Assert.Single(cc.Affected, a => a.LinkId == w.TestLink);
        Assert.Equal("Affected test", test.Label);
        Assert.True(test.Unchanged);

        var newCode = Assert.Single(cc.Affected, a => a.LinkId == w.NewCodeLink);
        Assert.Null(newCode.TargetId);
        Assert.StartsWith("NEW-", newCode.Number);
        Assert.Null(newCode.Label);
        Assert.False(newCode.IsRestricted);
        Assert.True(newCode.Unchanged);

        var item = Assert.Single(cc.Affected, a => a.LinkId == w.ItemLink);
        Assert.False(item.Unchanged);
        Assert.Equal("Affected item", item.Label);

        var hiddenSpec = Assert.Single(cc.Affected, a => a.LinkId == w.RestrictedSpecLink);
        Assert.True(hiddenSpec.IsRestricted);
        Assert.Null(hiddenSpec.Label);
        Assert.Equal(w.RestrictedCode, hiddenSpec.Number);
        Assert.True(hiddenSpec.Unchanged);
        var hiddenTest = Assert.Single(cc.Affected, a => a.LinkId == w.RestrictedTestLink);
        Assert.True(hiddenTest.IsRestricted);
        Assert.Null(hiddenTest.Label);
        Assert.Equal(w.RestrictedCode, hiddenTest.Number);
    }

    [PostgresFact]
    public async Task Unchanged_flag_clears_for_the_listed_record_after_a_gated_write_and_keeps_the_others()
    {
        var w = await ArrangeAsync();

        await using (var act = Context(w.Head))
        {
            var gate = new ChangeControlGate(act, new ChangeControlContext { Number = w.Number }, Clock);
            var specs = new SpecificationMasterDataService(act, new UserSectionScopeService(act), timeProvider: Clock, changeControlGate: gate);
            await specs.UpdateSpecificationAsync(w.Head, w.SpecId, new UpdateSpecificationRequest(
                TestCode: w.TestCode, ParameterName: "ParamChanged", LimitType: LimitType.Qualitative, ExpectedResultText: "Changed"));
        }

        var cc = await DetailAsync(w, w.Head);
        var spec = Assert.Single(cc.Affected, a => a.LinkId == w.SpecLink);
        Assert.False(spec.Unchanged);
        Assert.Equal(Now, spec.ChangedAtUtc);
        Assert.NotNull(spec.ChangeAuditLogId);
        Assert.Equal("ParamChanged", spec.Label);
        Assert.True(Assert.Single(cc.Affected, a => a.LinkId == w.TestLink).Unchanged);
        Assert.True(Assert.Single(cc.Affected, a => a.LinkId == w.NewCodeLink).Unchanged);

        await using var assertDb = Context();
        var persisted = await assertDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == w.SpecLink);
        Assert.Equal(persisted.ChangeAuditLogId, spec.ChangeAuditLogId);
        Assert.True(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.Id == spec.ChangeAuditLogId && a.EntityName == nameof(Specification)));
    }

    [PostgresFact]
    public async Task Another_laboratory_head_cannot_read_the_record_or_its_affected_list()
    {
        var w = await ArrangeAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => DetailAsync(w, w.OtherHead));
    }

    [PostgresFact]
    public async Task Missing_checklist_template_answers_success_with_null_data()
    {
        int reader;
        await using (var arrange = Context())
        {
            (reader, _) = await HeadAsync(arrange, "t" + Guid.NewGuid().ToString("N")[..8]);
            await arrange.QualityChecklistTemplates.Where(t => t.Type == QualityRecordType.Complaint).ExecuteDeleteAsync();
        }

        await using var act = Context(reader);
        var permissions = new PermissionService(act);
        var scope = new UserSectionScopeService(act);
        var core = new QualityRecordCore(act, permissions, scope, new ReferenceNumberGenerator(act, Clock),
            new ElectronicSignatureService(act, Clock), Clock);
        var controller = new QualityConfigController(new QualityConfigService(act, core, permissions, scope, Clock),
            new LabClock(Clock, TimeZoneInfo.Utc))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, reader.ToString())], "test"))
                }
            }
        };

        var result = await controller.GetTemplate(QualityRecordType.Complaint, null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<QualityChecklistTemplateDto?>>(ok.Value);
        Assert.True(body.Success);
        Assert.Null(body.Data);
    }
}
