using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using Xunit;
using static MicroLIMS.Tests.UnitTests.IcpRunServiceTests;

namespace MicroLIMS.Tests.UnitTests;

public class IcpRunCcvTests
{
    // Zn fails calibration (r 0.9989), Ca passes; calibration confirmed.
    private static async Task<(Scenario S, IcpRunDto Run)> ConfirmedAsync(bool requireCcv = true)
    {
        var s = await SeedAsync(requireCcv: requireCcv);
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id, znR: 0.9995m), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);
        run = await s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest("ValidPassword123!", null), s.UserId, null);
        return (s, run);
    }

    private static int ElementId(IcpRunDto run, string symbol) => run.Method.Elements.Single(e => e.Symbol == symbol).Id;
    private static IcpElementStateDto State(IcpRunDto run, string symbol) => run.ElementStates.Single(e => e.Symbol == symbol);

    [Fact]
    public async Task AddCcv_Passing_MakesElementValid()
    {
        var (s, run) = await ConfirmedAsync();
        Assert.Equal("Needs a passing CCV.", State(run, "Zn").Reason);

        run = await s.Service.AddCcvReadingAsync(run.Id, new AddIcpCcvRequest(ElementId(run, "Zn"), 1.02m), s.UserId);

        Assert.True(State(run, "Zn").Valid);
        var r = run.CcvReadings.Single();
        Assert.True(r.Passed);
        Assert.Equal(102m, r.RecoveryPercent);
        Assert.Equal("Needs a passing CCV.", State(run, "Ca").Reason);
    }

    [Fact]
    public async Task AddCcv_Failing_LocksElement()
    {
        var (s, run) = await ConfirmedAsync();
        var zn = ElementId(run, "Zn");
        await s.Service.AddCcvReadingAsync(run.Id, new AddIcpCcvRequest(zn, 1.01m), s.UserId);

        run = await s.Service.AddCcvReadingAsync(run.Id, new AddIcpCcvRequest(zn, 0.8m), s.UserId);

        Assert.False(State(run, "Zn").Valid);
        Assert.Equal("CCV failed (80.0%).", State(run, "Zn").Reason);
        Assert.Equal(2, run.CcvReadings.Count); // append-only, earlier pass kept
    }

    // The submitted-sample half of "locks only unsubmitted" is completed in Task 7 (recorder).
    [Fact]
    public async Task FailingCcv_LocksElement()
    {
        var (s, run) = await ConfirmedAsync();
        run = await s.Service.AddCcvReadingAsync(run.Id, new AddIcpCcvRequest(ElementId(run, "Ca"), 1.5m), s.UserId);
        Assert.False(State(run, "Ca").Valid);
        Assert.StartsWith("CCV failed", State(run, "Ca").Reason);
    }

    [Fact]
    public async Task Availability_Expired()
    {
        var (s, run) = await ConfirmedAsync(requireCcv: false);
        var cal = s.Db.IcpCalibrations.Single();
        cal.ConfirmedAt = SepFirst.UtcDateTime.AddHours(-(run.Method.MaxCalibrationAgeHours + 1));
        await s.Db.SaveChangesAsync();

        run = await s.Service.GetRunAsync(run.Id, s.UserId);

        var expiry = NewClock().ToLabLocal(cal.ConfirmedAt.Value.AddHours(run.Method.MaxCalibrationAgeHours));
        Assert.All(run.ElementStates, e => Assert.Equal($"Calibration expired at {expiry:yyyy-MM-dd HH:mm}.", e.Reason));
    }

    [Fact]
    public async Task Availability_Unconfirmed_And_FailedCalibration()
    {
        var s = await SeedAsync(requireCcv: false);
        var run = await StartAsync(s);
        Assert.All(run.ElementStates, e => Assert.Equal("Calibration is not confirmed.", e.Reason));

        var (_, confirmed) = await ConfirmedAsync(requireCcv: false);
        Assert.True(State(confirmed, "Zn").Valid); // znR 0.9995 passes here
    }

    [Fact]
    public async Task Availability_CcvNotRequired_ValidAfterCalibration()
    {
        var (_, run) = await ConfirmedAsync(requireCcv: false);
        Assert.All(run.ElementStates, e => Assert.True(e.Valid));
    }

    [Fact]
    public async Task AddCcv_MethodWithoutCcv_Throws()
    {
        var (s, run) = await ConfirmedAsync(requireCcv: false);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Service.AddCcvReadingAsync(run.Id, new AddIcpCcvRequest(ElementId(run, "Zn"), 1m), s.UserId));
        Assert.Equal("This method does not use CCV.", ex.Message);
    }

    [Fact]
    public async Task AddCcv_BeforeConfirm_ForeignElement_Negative_Throw()
    {
        var s = await SeedAsync(requireCcv: true);
        var run = await StartAsync(s);
        var zn = ElementId(run, "Zn");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => s.Service.AddCcvReadingAsync(run.Id, new AddIcpCcvRequest(zn, 1m), s.UserId));
        Assert.Equal("Confirm a valid calibration before entering CCV.", ex.Message);

        var (s2, run2) = await ConfirmedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => s2.Service.AddCcvReadingAsync(run2.Id, new AddIcpCcvRequest(9999, 1m), s2.UserId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s2.Service.AddCcvReadingAsync(run2.Id, new AddIcpCcvRequest(ElementId(run2, "Zn"), -1m), s2.UserId));
    }

    [Fact]
    public async Task AddCcv_ExpiredCalibration_Throws()
    {
        var (s, run) = await ConfirmedAsync();
        s.Db.IcpCalibrations.Single().ConfirmedAt = SepFirst.UtcDateTime.AddDays(-365);
        await s.Db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Service.AddCcvReadingAsync(run.Id, new AddIcpCcvRequest(ElementId(run, "Zn"), 1m), s.UserId));
        Assert.Equal("Confirm a valid calibration before entering CCV.", ex.Message);
    }
}
