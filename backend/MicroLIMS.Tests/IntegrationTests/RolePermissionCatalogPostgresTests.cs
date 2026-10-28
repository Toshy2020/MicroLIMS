using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// The Roles screen's permission list must load on PostgreSQL, where the hidden-code
// filter has to translate to SQL (the in-memory provider evaluates it client-side).
[Collection("PostgresDatabaseCollection")]
public class RolePermissionCatalogPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public RolePermissionCatalogPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task GetAllPermissions_LoadsOnPostgres_AndOmitsHiddenCodes()
    {
        await using var db = _fixture.CreateDbContext();
        var service = new RoleService(db, new PermissionService(db));

        var codes = (await service.GetAllPermissionsAsync()).Select(p => p.Code).ToList();

        Assert.NotEmpty(codes);
        Assert.Contains(PermissionConstants.UsersManage, codes);
        Assert.DoesNotContain(codes, c => PermissionConstants.Hidden.Contains(c));
    }
}
