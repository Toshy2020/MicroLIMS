using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// What each gated action refuses, and with which exception and sentence (C8 Task 5). Written against the code as
// it stood before the guards were moved into Check... methods, and unchanged by that move: a failure here means
// a refusal changed its type, its wording or its place in the order of checks.
// The arrangements are ActionGuardScenarios; each helper there uses its own DbContext.
[Collection("PostgresDatabaseCollection")]
public class ActionGuardCharacterisationPostgresTests
{
    private readonly ActionGuardScenarios _scenarios;

    public ActionGuardCharacterisationPostgresTests(PostgresTestFixture fixture) => _scenarios = new ActionGuardScenarios(fixture);

    private ActionGuardScenarios.ActionCase Case(string action) => _scenarios.Actions().Single(a => a.Action == action);

    private static Task RefusesAsync(ActionGuardScenarios.ActionCase action) =>
        PostgresCases.RunAsync(action.Refusals, async refusal =>
        {
            var arranged = await refusal.Arrange();
            var ex = await Record.ExceptionAsync(() => action.Attempt(arranged.Target));
            Assert.NotNull(ex);
            Assert.Equal(refusal.ExceptionType, ex.GetType());
            Assert.Equal(arranged.Message, ex.Message);
        });

    private static async Task SucceedsAsync(ActionGuardScenarios.ActionCase action)
    {
        var target = await action.Allowed();
        await action.Attempt(target);
        await action.AssertDone(target);
    }

    [PostgresFact] public Task ReviewComplete_Refusals() => RefusesAsync(Case("review.complete"));
    [PostgresFact] public Task ReviewComplete_Allowed() => SucceedsAsync(Case("review.complete"));
    [PostgresFact] public Task ReviewReturn_Refusals() => RefusesAsync(Case("review.return"));
    [PostgresFact] public Task ReviewReturn_Allowed() => SucceedsAsync(Case("review.return"));
    [PostgresFact] public Task ApprovalApprove_Refusals() => RefusesAsync(Case("approval.approve"));
    [PostgresFact] public Task ApprovalApprove_Allowed() => SucceedsAsync(Case("approval.approve"));
    [PostgresFact] public Task ApprovalReject_Refusals() => RefusesAsync(Case("approval.reject"));
    [PostgresFact] public Task ApprovalReject_Allowed() => SucceedsAsync(Case("approval.reject"));
    [PostgresFact] public Task SampleVoid_Refusals() => RefusesAsync(Case("sample.void"));
    [PostgresFact] public Task SampleVoid_Allowed() => SucceedsAsync(Case("sample.void"));
    [PostgresFact] public Task SampleAssign_Refusals() => RefusesAsync(Case("sample.assign"));
    [PostgresFact] public Task SampleAssign_Allowed() => SucceedsAsync(Case("sample.assign"));
    [PostgresFact] public Task TestTake_Refusals() => RefusesAsync(Case("test.take"));
    [PostgresFact] public Task TestTake_Allowed() => SucceedsAsync(Case("test.take"));
    [PostgresFact] public Task TestRelease_Refusals() => RefusesAsync(Case("test.release"));
    [PostgresFact] public Task TestRelease_Allowed() => SucceedsAsync(Case("test.release"));
    [PostgresFact] public Task MediaRelease_Refusals() => RefusesAsync(Case("media.release"));
    [PostgresFact] public Task MediaRelease_Allowed() => SucceedsAsync(Case("media.release"));
    [PostgresFact] public Task DocumentApprove_Refusals() => RefusesAsync(Case("document.approve"));
    [PostgresFact] public Task DocumentApprove_Allowed() => SucceedsAsync(Case("document.approve"));

    // The approval prerequisites hold back an approval only: the same sample can still be rejected.
    [PostgresFact]
    public Task ApprovalReject_IsNotHeldBackByApprovalPrerequisites() =>
        PostgresCases.RunAsync(_scenarios.ApproveOnlyRefusals(), async refusal =>
        {
            var arranged = await refusal.Arrange();
            await _scenarios.RejectAsync(arranged.Target);
            await Case("approval.reject").AssertDone(arranged.Target);
        });
}
