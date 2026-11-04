using Microsoft.EntityFrameworkCore;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

public class RefreshTokenSessionMetadataMigrationPostgresTests
{
    [PostgresFact]
    public async Task Migration_AddsBoundedNullableSessionColumns()
    {
        await ScratchPostgresDatabase.RunAsync(async connectionString =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(connectionString);
            await db.Database.MigrateAsync();

            var columns = await db.Database.SqlQuery<string>($@"
                SELECT column_name || ':' || is_nullable || ':' || COALESCE(character_maximum_length::text, '') AS ""Value""
                FROM information_schema.columns
                WHERE table_name = 'RefreshTokens' AND column_name IN ('CreatedIp','UserAgent','LastUsedAt')").ToListAsync();

            Assert.Contains("CreatedIp:YES:64", columns);
            Assert.Contains("UserAgent:YES:256", columns);
            Assert.Contains("LastUsedAt:YES:", columns);
        });
    }
}
