using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Groundwork pin: SignatureMeaning values are stored as numbers in signed records.
// These values are stored as numbers in signed records; never renumber, rename or
// insert. Append new values at the end and extend this list in the same commit.
public class SignatureMeaningPinTests
{
    [Fact]
    public void Every_member_keeps_its_name_and_number()
    {
        var expected = new (string Name, int Number)[]
        {
            ("Reviewed", 0),
            ("Approved", 1),
            ("Rejected", 2),
            ("RetestRequested", 3),
            ("InvestigationOrdered", 4),
            ("PreparationConfirmed", 5),
            ("SampleCorrected", 6),
            ("SampleVoided", 7),
            ("MasterDataChanged", 8),
            ("SuitabilityRunPerformed", 9),
            ("ResultRecorded", 10),
            ("CalibrationRunPerformed", 11),
            ("CalibrationRunWithdrawn", 12),
            ("TestingClosed", 13),
            ("LaboratoryAdded", 14),
            ("TitrantStandardized", 15),
            ("TitrantDueAcknowledged", 16),
            ("BiologicalWasteDestroyed", 17),
            ("MonitoringPlanApproved", 18),
            ("MonitoringOccurrenceSkipped", 19),
            ("MonitoringOccurrenceWindowChanged", 20),
            ("MonitoringDeviationClosed", 21),
            ("EquipmentPlanApproved", 22),
            ("CalibrationRecorded", 23),
            ("MaintenanceEntryRecorded", 24),
            ("MaintenanceEntryVoided", 25),
            ("UserDeleted", 26),
            ("UserRoleChanged", 27),
            ("RolePermissionsChanged", 28),
            ("RoleDeleted", 29),
            ("SystemSettingChanged", 30),
            ("AccessReviewSigned", 31),
            ("TestsSelected", 32),
            ("NotApplicableHeadApproved", 33),
            ("NotApplicableFinalApproved", 34),
            ("NotApplicableWithdrawn", 35),
            ("TestOutsourced", 36),
            ("SampleAssignedToRun", 37),
        };

        var actual = Enum.GetValues<SignatureMeaning>()
            .Select(v => (v.ToString(), (int)v))
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
