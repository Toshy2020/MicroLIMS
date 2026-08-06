using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

// Shared by CountTestWorkflowEngine and PathogenWorkflowEngine: validates
// the picked Media's MediaType is approved (via TestDefinitionMedia) for
// this TestCode/step, then creates an Incubation row with
// Temperature/Duration hard-locked from that MediaType - these are never
// accepted from the caller, always derived server-side.
public static class IncubationSetupHelper
{
    // matchStepName is what's looked up against TestDefinitionMedia's
    // StepName (null for Count Tests, which have a single unnamed step;
    // the step name itself for Pathogen chains). incubationStepName is
    // the label stored on the created Incubation row - for Count Tests
    // this is the fixed "CountIncubation" label, distinct from the null
    // approval-matching key.
    public static async Task<Incubation> LockAndCreateAsync(
        MicroLimsDbContext db, int testOrderId, string testCode, string? matchStepName, string incubationStepName,
        int mediaId, int incubatorEquipmentId)
    {
        var media = await db.Media.Include(m => m.MediaType).Include(m => m.Material).FirstOrDefaultAsync(m => m.Id == mediaId)
            ?? throw new InvalidOperationException($"Media lot {mediaId} not found.");
        if (media.MediaType is null)
            throw new InvalidOperationException($"Media lot {media.LotNumber} has no media type.");

        var testDefinition = await db.TestDefinitions.FirstOrDefaultAsync(t => t.Code == testCode)
            ?? throw new InvalidOperationException($"Test code \"{testCode}\" is not in the Test Master.");

        var approved = await db.TestDefinitionMedias.AnyAsync(m =>
            m.TestDefinitionId == testDefinition.Id && m.MediaTypeId == media.MediaTypeId && m.StepName == matchStepName);
        if (!approved)
            throw new InvalidOperationException(
                $"{media.Material?.MaterialName ?? media.MediaType.Class.ToString()} is not an approved media for {testCode}" +
                (matchStepName != null ? $" ({matchStepName})" : "") + ".");

        var incubation = new Incubation
        {
            TestOrderId = testOrderId,
            StepName = incubationStepName,
            MediaId = mediaId,
            IncubatorEquipmentId = incubatorEquipmentId,
            Temperature = $"{media.MediaType.RequiredTemperatureMin}-{media.MediaType.RequiredTemperatureMax}",
            Duration = $"{media.MediaType.IncubationMinHours}-{media.MediaType.IncubationMaxHours}"
        };
        db.Incubations.Add(incubation);
        return incubation;
    }
}
