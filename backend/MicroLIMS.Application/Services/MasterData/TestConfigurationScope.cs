using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.Application.Services.MasterData;

// The one laboratory-scope rule for changing a test's configuration (the test
// itself, its workflow, its frozen state): a user may configure a test only in
// a laboratory section they are assigned to, unless their role holds
// Sections.ConfigureAll. Shared by TestDefinitionMasterDataService and
// TestWorkflowStepMasterDataService so the rule and its refusal stay identical.
internal static class TestConfigurationScope
{
    public static async Task EnsureCanConfigureAsync(
        IUserSectionScopeService scope, int currentUserId, int testSectionId, CancellationToken cancellationToken = default)
    {
        var configurable = await scope.GetConfigurableSectionIdsAsync(currentUserId, cancellationToken);
        if (configurable is not null && !configurable.Contains(testSectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
    }
}
