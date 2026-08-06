using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Persistence.Seed;

// Bootstraps roles, a first System Administrator, and enough baseline
// master data (Cause of Testing, an example Item, a starter MediaType +
// one released Media lot) so every receiving flow works immediately
// after the first migration - including the GPT bootstrapping
// requirement (a qualified media lot must exist before the first
// Reference Strain can ever be received).
public static class DbSeeder
{
    public static void Seed(MicroLimsDbContext db)
    {
        if (!db.Roles.Any())
        {
            db.Roles.AddRange(
                new Role { Type = RoleType.SystemAdministrator, Name = "System Administrator" },
                new Role { Type = RoleType.SectionHead, Name = "Section Head" },
                new Role { Type = RoleType.Reviewer, Name = "Reviewer" },
                new Role { Type = RoleType.Analyst, Name = "Analyst" }
            );
            db.SaveChanges();
        }

        if (!db.Users.Any())
        {
            var adminRole = db.Roles.First(r => r.Type == RoleType.SystemAdministrator);
            db.Users.Add(new User
            {
                FullName = "System Administrator",
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("ChangeMe123!"),
                RoleId = adminRole.Id
            });
            db.SaveChanges();
        }

        if (!db.CausesOfTesting.Any())
        {
            db.CausesOfTesting.AddRange(
                new CauseOfTesting { Name = "Routine" },
                new CauseOfTesting { Name = "Investigation" },
                new CauseOfTesting { Name = "Retest" }
            );
        }

        if (!db.Neutralizers.Any())
        {
            db.Neutralizers.AddRange(
                new Neutralizer { Name = "Tween" },
                new Neutralizer { Name = "Lecithin" }
            );
        }

        if (!db.Equipment.Any())
        {
            db.Equipment.AddRange(
                new Equipment { Name = "Incubator 03", Code = "INC-03", Type = EquipmentType.Incubator, SetPointTemperature = 32.5m, CalibrationDueDate = DateTime.UtcNow.AddMonths(6) },
                new Equipment { Name = "Autoclave 1", Code = "AUT-01", Type = EquipmentType.Autoclave }
            );
        }

        // Fixed set - exactly one row per MediaClass, enforced by a
        // unique index. Values below are placeholders; Section Head
        // corrects them for real via the Media Types admin page.
        if (!db.MediaTypes.Any())
        {
            db.MediaTypes.AddRange(
                new MediaType
                {
                    Class = MediaClass.GeneralAgar,
                    IncubationMinHours = 24, IncubationMaxHours = 48,
                    RequiredTemperatureMin = 30, RequiredTemperatureMax = 35,
                    ApprovedTestCodes = new List<string> { "TAMC" },
                    RecoveryPercentMin = 70, RecoveryPercentMax = 200
                },
                new MediaType
                {
                    Class = MediaClass.GeneralBroth,
                    IncubationMinHours = 24, IncubationMaxHours = 48,
                    RequiredTemperatureMin = 30, RequiredTemperatureMax = 35,
                    ApprovedTestCodes = new List<string> { "Sterility" }
                },
                new MediaType
                {
                    Class = MediaClass.SelectiveAgar,
                    IncubationMinHours = 18, IncubationMaxHours = 24,
                    RequiredTemperatureMin = 32.5m, RequiredTemperatureMax = 35,
                    ApprovedTestCodes = new List<string> { "PATHOGEN_ECOLI" }
                },
                new MediaType
                {
                    Class = MediaClass.SelectiveBroth,
                    IncubationMinHours = 18, IncubationMaxHours = 24,
                    RequiredTemperatureMin = 32.5m, RequiredTemperatureMax = 35,
                    ApprovedTestCodes = new List<string> { "PATHOGEN_ECOLI" }
                }
            );
        }

        if (!db.DiluentTypes.Any())
        {
            db.DiluentTypes.Add(new DiluentType { Name = "Buffer PhB 7.2", RequiresBatchTracking = false });
        }

        db.SaveChanges();

        // Bootstrap: mark one starter General Agar lot as already
        // released so the very first Cryovial batch can be prepared
        // (its identity-confirmation panel needs a released media lot -
        // this breaks that chicken-and-egg only for the initial seed,
        // bypassing the normal MediaEvaluationEngine release path).
        // Media.MaterialId is required, so this also seeds the one
        // dehydrated-media stock row it's prepared from.
        if (!db.Media.Any())
        {
            var admin = db.Users.First(u => u.Username == "admin");
            var tsaMaterial = new Material
            {
                MaterialType = MaterialType.DehydratedMedia,
                MaterialName = "Tryptic Soy Agar (Dehydrated)",
                ManufacturerName = "Seed Data",
                BatchNumber = "SEED-0001",
                ReceivingDate = DateTime.UtcNow,
                Code = "TSA",
                Location = "Micro Lab",
                QuantityReceived = 1000,
                QuantityRemaining = 900,
                Unit = MaterialUnit.Gram,
                CreatedByUserId = admin.Id,
                LastModifiedByUserId = admin.Id
            };
            db.Materials.Add(tsaMaterial);
            db.SaveChanges();

            var generalAgar = db.MediaTypes.First(m => m.Class == MediaClass.GeneralAgar);
            db.Media.Add(new Media
            {
                MediaTypeId = generalAgar.Id,
                MaterialId = tsaMaterial.Id,
                LotNumber = $"{tsaMaterial.Code}/01/{DateTime.UtcNow:yy}",
                ManufacturerLot = tsaMaterial.BatchNumber,
                ManufacturerName = tsaMaterial.ManufacturerName,
                ExpiryDate = DateTime.UtcNow.AddYears(1),
                Status = MediaStatus.Active,
                IsReleasedForUse = true,
                // Stamped Approved to match IsReleasedForUse - this seed lot
                // bypasses both the evaluation and the release gate on
                // purpose (see the comment above), and leaving it
                // PendingReview would show a released lot sitting in the
                // Section Head's approval queue forever.
                ApprovalStatus = ApprovalGateStatus.Approved,
                ApprovedByUserId = admin.Id,
                ApprovedAt = DateTime.UtcNow,
                PreparedByUserId = admin.Id
            });
            db.SaveChanges();
        }

        if (!db.Items.Any())
        {
            db.Items.Add(new Item
            {
                Name = "Finished Product - Example Tablet",
                Code = "FP-0001",
                Category = SampleCategory.FinishedProduct,
                SopNumber = "C2I-91-001",
                AssignedTests = new List<SampleTest>
                {
                    new() { TestCode = "TAMC", DisplayName = "Total Aerobic Microbial Count" },
                    new() { TestCode = "TYMC", DisplayName = "Total Yeast & Mold Count" },
                    new() { TestCode = "PATHOGEN_ECOLI", DisplayName = "E. coli" }
                }
            });
            db.SaveChanges();
        }

        SeedWorkflowTemplates(db);
    }

    // The three core TestWorkflowEngine templates (TAMC/TYMC's single
    // count step, a generic 2-step pathogen chain, Salmonella's 3-step
    // dual-plate chain) - only for TestDefinition codes that already
    // exist (the analyst adds those via Test Master; this never creates
    // a TestDefinition row itself). Safe to call on every startup - each
    // template is only inserted once (TestWorkflowSteps is empty check).
    private static void SeedWorkflowTemplates(MicroLimsDbContext db)
    {
        if (db.TestWorkflowSteps.Any()) return;

        var generalAgar = db.MediaTypes.First(m => m.Class == MediaClass.GeneralAgar);
        var generalBroth = db.MediaTypes.First(m => m.Class == MediaClass.GeneralBroth);
        var selectiveAgar = db.MediaTypes.First(m => m.Class == MediaClass.SelectiveAgar);
        var selectiveBroth = db.MediaTypes.First(m => m.Class == MediaClass.SelectiveBroth);

        void SeedCountTestTemplate(string code)
        {
            var test = db.TestDefinitions.FirstOrDefault(t => t.Code == code);
            if (test is null)
            {
                Console.WriteLine($"[DbSeeder] Skipping workflow template for \"{code}\" - not in Test Master yet.");
                return;
            }
            test.WorkflowType = WorkflowType.CountTest;
            db.TestWorkflowSteps.Add(new TestWorkflowStep
            {
                TestDefinitionId = test.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = generalAgar.Id,
                IncubationMinHours = 72, IncubationMaxHours = 120, TemperatureMin = 30, TemperatureMax = 35,
                IsFinalStep = true, IsDualPlate = false
            });
        }

        SeedCountTestTemplate("TAMC");
        SeedCountTestTemplate("TYMC");

        var ecoli = db.TestDefinitions.FirstOrDefault(t => t.Code == "PATHOGEN_ECOLI");
        if (ecoli is null)
        {
            Console.WriteLine("[DbSeeder] Skipping workflow template for \"PATHOGEN_ECOLI\" - not in Test Master yet.");
        }
        else
        {
            ecoli.WorkflowType = WorkflowType.Observation;
            db.TestWorkflowSteps.AddRange(
                new TestWorkflowStep
                {
                    TestDefinitionId = ecoli.Id, StepOrder = 1, StepName = "TSB", MediaTypeId = generalBroth.Id,
                    IncubationMinHours = 24, IncubationMaxHours = 72, TemperatureMin = 35, TemperatureMax = 37,
                    IsFinalStep = false, IsDualPlate = false
                },
                new TestWorkflowStep
                {
                    TestDefinitionId = ecoli.Id, StepOrder = 2, StepName = "Detection", MediaTypeId = selectiveAgar.Id,
                    IncubationMinHours = 24, IncubationMaxHours = 72, TemperatureMin = 35, TemperatureMax = 37,
                    IsFinalStep = true, IsDualPlate = false
                });
        }

        var salmonella = db.TestDefinitions.FirstOrDefault(t => t.Code == "PATHOGEN_SALMONELLA");
        if (salmonella is null)
        {
            Console.WriteLine("[DbSeeder] Skipping workflow template for \"PATHOGEN_SALMONELLA\" - not in Test Master yet.");
        }
        else
        {
            salmonella.WorkflowType = WorkflowType.DualPlate;
            db.TestWorkflowSteps.AddRange(
                new TestWorkflowStep
                {
                    TestDefinitionId = salmonella.Id, StepOrder = 1, StepName = "TSB", MediaTypeId = generalBroth.Id,
                    IncubationMinHours = 24, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37,
                    IsFinalStep = false, IsDualPlate = false
                },
                new TestWorkflowStep
                {
                    TestDefinitionId = salmonella.Id, StepOrder = 2, StepName = "RVS", MediaTypeId = selectiveBroth.Id,
                    IncubationMinHours = 24, IncubationMaxHours = 24, TemperatureMin = 42, TemperatureMax = 43,
                    IsFinalStep = false, IsDualPlate = false
                },
                new TestWorkflowStep
                {
                    TestDefinitionId = salmonella.Id, StepOrder = 3, StepName = "XLD_TSI", MediaTypeId = selectiveAgar.Id,
                    IncubationMinHours = 24, IncubationMaxHours = 48, TemperatureMin = 35, TemperatureMax = 37,
                    IsFinalStep = true, IsDualPlate = true
                });
        }

        db.SaveChanges();
    }
}
