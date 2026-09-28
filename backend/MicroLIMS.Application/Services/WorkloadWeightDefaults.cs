using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// The baseline workload weights: seeded at startup, and again by the KPI
// page if the table has been emptied.
public static class WorkloadWeightDefaults
{
    public static void Seed(IMicroLimsDbContext db)
    {
        if (db.WorkloadWeights.Any()) return;

        var defaultWeights = new List<WorkloadWeight>
        {
            new WorkloadWeight { TestCode = "TAMC", TestName = "Total Aerobic Microbial Count", Category = SampleCategory.FinishedProduct, Weight = 1.0m, ReasonForChange = "Initial baseline configuration", ChangedByName = "System Administrator" },
            new WorkloadWeight { TestCode = "TYMC", TestName = "Total Yeast & Mold Count", Category = SampleCategory.FinishedProduct, Weight = 1.0m, ReasonForChange = "Initial baseline configuration", ChangedByName = "System Administrator" },
            new WorkloadWeight { TestCode = "WATER_MICRO", TestName = "Water Microbiology (TAMC)", Category = SampleCategory.Water, Weight = 1.2m, ReasonForChange = "Filtration and multiple volume plating factor", ChangedByName = "System Administrator" },
            new WorkloadWeight { TestCode = "GPT", TestName = "Growth Promotion Test", Category = SampleCategory.GPT, Weight = 2.0m, ReasonForChange = "Multiple standard strain inoculations and 5-day verification", ChangedByName = "System Administrator" },
            new WorkloadWeight { TestCode = "STERILITY", TestName = "Sterility Test (Membrane Filtration)", Category = SampleCategory.FinishedProduct, Weight = 3.0m, ReasonForChange = "Cleanroom gowning, 14-day incubation, dual media manipulation", ChangedByName = "System Administrator" },
            new WorkloadWeight { TestCode = "MICROBIAL_ID", TestName = "Microbial Identification (Gram / Biochemical / Vitek)", Category = SampleCategory.FinishedProduct, Weight = 2.5m, ReasonForChange = "Staining, subculture, card preparation and confirmation", ChangedByName = "System Administrator" },
            new WorkloadWeight { TestCode = "INVESTIGATION", TestName = "Laboratory Investigation / OOS Retest", Category = SampleCategory.FinishedProduct, Weight = 3.0m, ReasonForChange = "Comprehensive phase 1 testing checklist and supervisor review", ChangedByName = "System Administrator" },
            new WorkloadWeight { TestCode = "PATHOGEN_ECOLI", TestName = "E. coli Screening", Category = SampleCategory.FinishedProduct, Weight = 1.5m, ReasonForChange = "Initial baseline configuration", ChangedByName = "System Administrator" },
            new WorkloadWeight { TestCode = "PATHOGEN_SALM", TestName = "Salmonella Screening", Category = SampleCategory.FinishedProduct, Weight = 1.5m, ReasonForChange = "Initial baseline configuration", ChangedByName = "System Administrator" },
            new WorkloadWeight { TestCode = "PATHOGEN_PAERUG", TestName = "P. aeruginosa Screening", Category = SampleCategory.FinishedProduct, Weight = 1.5m, ReasonForChange = "Initial baseline configuration", ChangedByName = "System Administrator" },
            new WorkloadWeight { TestCode = "PATHOGEN_SAUREUS", TestName = "S. aureus Screening", Category = SampleCategory.FinishedProduct, Weight = 1.5m, ReasonForChange = "Initial baseline configuration", ChangedByName = "System Administrator" }
        };

        db.WorkloadWeights.AddRange(defaultWeights);
        db.SaveChanges();
    }
}
