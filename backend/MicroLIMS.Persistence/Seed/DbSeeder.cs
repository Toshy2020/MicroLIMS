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

        // 1. Reconcile Equipment (Laboratory Configuration Equipment)
        var incucellEq = db.Equipment.FirstOrDefault(e => e.Code == "INC-F-ML-F-01-002" || e.Code == "INC-03");
        if (incucellEq != null)
        {
            incucellEq.Code = "INC-F-ML-F-01-002";
            incucellEq.Name = "INCUCELL";
            incucellEq.Type = EquipmentType.Incubator;
            if (incucellEq.SetPointTemperature == null || incucellEq.SetPointTemperature == 0)
            {
                incucellEq.SetPointTemperature = 36.5m;
            }
        }
        else
        {
            db.Equipment.Add(new Equipment
            {
                Name = "INCUCELL",
                Code = "INC-F-ML-F-01-002",
                Type = EquipmentType.Incubator,
                SetPointTemperature = 36.5m,
                CalibrationDueDate = DateTime.UtcNow.AddMonths(6)
            });
        }

        var hirayamaEq = db.Equipment.FirstOrDefault(e => e.Code == "AUT-F-ML-F-03-045" || e.Code == "AUT-01" || e.Code == "ATC-F-ML-F-03-045");
        if (hirayamaEq != null)
        {
            hirayamaEq.Code = "AUT-F-ML-F-03-045";
            hirayamaEq.Name = "Hirayama";
            hirayamaEq.Type = EquipmentType.Autoclave;
        }
        else
        {
            hirayamaEq = new Equipment
            {
                Name = "Hirayama",
                Code = "AUT-F-ML-F-03-045",
                Type = EquipmentType.Autoclave
            };
            db.Equipment.Add(hirayamaEq);
        }

        db.SaveChanges();

        // 2. Reconcile EquipmentInventories (Physical Inventory Assets)
        var incucellInv = db.EquipmentInventories.FirstOrDefault(i => i.Code == "INC-F-ML-F-01-002" || i.SerialNumber == "D,141445");
        if (incucellInv != null)
        {
            incucellInv.Code = "INC-F-ML-F-01-002";
            incucellInv.InstrumentType = "INCUCELL";
            incucellInv.ManufacturerName = "INCUCELL";
            incucellInv.SerialNumber = "D,141445";
            incucellInv.Status = EquipmentOperationalStatus.InService;
        }
        else
        {
            db.EquipmentInventories.Add(new EquipmentInventory
            {
                Code = "INC-F-ML-F-01-002",
                InstrumentType = "INCUCELL",
                ManufacturerName = "INCUCELL",
                SerialNumber = "D,141445",
                FirmwareVersion = "v1.0.4",
                Location = "Instruments room F-ML-F-01",
                Status = EquipmentOperationalStatus.InService,
                CalibrationDueDate = DateTime.UtcNow.AddMonths(6),
                CreatedByUserId = 1,
                LastModifiedByUserId = 1
            });
        }

        var hirayamaInv = db.EquipmentInventories.FirstOrDefault(i => i.Code == "AUT-F-ML-F-03-045" || i.Code == "ATC-F-ML-F-03-045" || i.SerialNumber == "30317012128");
        if (hirayamaInv != null)
        {
            hirayamaInv.Code = "AUT-F-ML-F-03-045";
            hirayamaInv.InstrumentType = "Hirayama";
            hirayamaInv.ManufacturerName = "Hirayama";
            hirayamaInv.SerialNumber = "30317012128";
            hirayamaInv.Status = EquipmentOperationalStatus.InService;
        }
        else
        {
            db.EquipmentInventories.Add(new EquipmentInventory
            {
                Code = "AUT-F-ML-F-03-045",
                InstrumentType = "Hirayama",
                ManufacturerName = "Hirayama",
                SerialNumber = "30317012128",
                FirmwareVersion = "v2.0",
                Location = "Sterilization room F-ML-F-04",
                Status = EquipmentOperationalStatus.InService,
                CalibrationDueDate = DateTime.UtcNow.AddMonths(12),
                CreatedByUserId = 1,
                LastModifiedByUserId = 1
            });
        }

        db.SaveChanges();

        // 3. Reconcile Autoclave Programs
        var currentAutoclave = db.Equipment.FirstOrDefault(e => e.Code == "AUT-F-ML-F-03-045" || e.Type == EquipmentType.Autoclave);
        if (currentAutoclave != null)
        {
            if (!db.AutoclavePrograms.Any())
            {
                db.AutoclavePrograms.AddRange(
                    new AutoclaveProgram { EquipmentId = currentAutoclave.Id, ProgramCode = "P01", ProgramName = "Prepared Media", LoadType = "Media", Temperature = 121m, CycleTimeMinutes = 15, IsActive = true, CreatedByUserId = 1, LastModifiedByUserId = 1 },
                    new AutoclaveProgram { EquipmentId = currentAutoclave.Id, ProgramCode = "P02", ProgramName = "Glassware", LoadType = "Glassware", Temperature = 121m, CycleTimeMinutes = 20, IsActive = true, CreatedByUserId = 1, LastModifiedByUserId = 1 },
                    new AutoclaveProgram { EquipmentId = currentAutoclave.Id, ProgramCode = "P03", ProgramName = "Biohazard Waste", LoadType = "Biohazard Waste", Temperature = 121m, CycleTimeMinutes = 30, IsActive = true, CreatedByUserId = 1, LastModifiedByUserId = 1 }
                );
            }
            else
            {
                foreach (var prog in db.AutoclavePrograms.ToList())
                {
                    prog.EquipmentId = currentAutoclave.Id;
                }
            }
            db.SaveChanges();
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
            var adminUser = db.Users.FirstOrDefault(u => u.Username == "admin") ?? db.Users.FirstOrDefault();
            int adminUserId = adminUser?.Id ?? 1;
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
                CreatedByUserId = adminUserId,
                LastModifiedByUserId = adminUserId
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
                ApprovedByUserId = adminUserId,
                ApprovedAt = DateTime.UtcNow,
                PreparedByUserId = adminUserId
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

    // The TestWorkflowEngine templates (TAMC/TYMC's single count step,
    // Salmonella's five-stage confirmatory chain, and the same five-stage
    // shape applied generically to every other Observation-typed test
    // with no steps yet) - only for TestDefinition codes that already
    // exist (the analyst adds those via Test Master; this never creates
    // a TestDefinition row itself). Each seed action is idempotent per
    // TestDefinition so this keeps picking up newly-added pathogen test
    // codes on every startup instead of only running once.
    private static void SeedWorkflowTemplates(MicroLimsDbContext db)
    {
        var generalAgar = db.MediaTypes.First(m => m.Class == MediaClass.GeneralAgar);
        var generalBroth = db.MediaTypes.First(m => m.Class == MediaClass.GeneralBroth);
        var selectiveAgar = db.MediaTypes.First(m => m.Class == MediaClass.SelectiveAgar);
        var selectiveBroth = db.MediaTypes.First(m => m.Class == MediaClass.SelectiveBroth);

        SeedCountTestTemplate(db, "TAMC", generalAgar.Id);
        SeedCountTestTemplate(db, "TYMC", generalAgar.Id);

        SeedPathogenTemplate(db, "PATHOGEN_SALMONELLA", "Salmonella enterica",
            generalBroth.Id, selectiveBroth.Id, selectiveAgar.Id,
            selectivePlatingMedium: "XLD Agar",
            confirmatoryMedia: new[] { ("XLD Agar", 35m, 37m), ("TSI Agar", 35m, 37m) });

        foreach (var test in db.TestDefinitions
            .Where(t => t.WorkflowType == WorkflowType.Observation && !db.TestWorkflowSteps.Any(s => s.TestDefinitionId == t.Id))
            .ToList())
        {
            SeedPathogenTemplate(db, test.Code, organismScientificName: null,
                generalBroth.Id, selectiveBroth.Id, selectiveAgar.Id,
                selectivePlatingMedium: "Selective Agar",
                confirmatoryMedia: new[] { ("Selective Agar", 35m, 37m) });
        }
    }

    private static void SeedCountTestTemplate(MicroLimsDbContext db, string testCode, int generalAgarId)
    {
        var test = db.TestDefinitions.FirstOrDefault(t => t.Code == testCode);
        if (test is null) { Console.WriteLine($"Seed: {testCode} not in Test Master - workflow template skipped."); return; }
        if (db.TestWorkflowSteps.Any(s => s.TestDefinitionId == test.Id)) return;

        test.WorkflowType = WorkflowType.CountTest;
        db.TestWorkflowSteps.Add(new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = generalAgarId,
            IncubationMinHours = 72, IncubationMaxHours = 120, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true, StepType = StepType.PlateCount
        });
        db.SaveChanges();
    }

    // Five-stage pathogen chain. Every step gets exactly the StepMedia
    // rows its StepType requires (see WorkflowTemplateValidator's rules).
    private static void SeedPathogenTemplate(
        MicroLimsDbContext db, string testCode, string? organismScientificName,
        int generalBrothId, int selectiveBrothId, int selectiveAgarId,
        string selectivePlatingMedium, (string Name, decimal TempMin, decimal TempMax)[] confirmatoryMedia)
    {
        var test = db.TestDefinitions.FirstOrDefault(t => t.Code == testCode);
        if (test is null) { Console.WriteLine($"Seed: {testCode} not in Test Master - workflow template skipped."); return; }
        if (db.TestWorkflowSteps.Any(s => s.TestDefinitionId == test.Id)) return;

        var organismId = organismScientificName is null
            ? db.Organisms.Select(o => (int?)o.Id).FirstOrDefault()
            : db.Organisms.Where(o => o.ScientificName == organismScientificName).Select(o => (int?)o.Id).FirstOrDefault();
        if (organismId is null) { Console.WriteLine($"Seed: no Organism for {testCode} - workflow template skipped."); return; }

        test.WorkflowType = WorkflowType.Observation;

        var tsb = new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 1, StepName = "Broth Enrichment", MediaTypeId = generalBrothId,
            IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37,
            IsFinalStep = false, StepType = StepType.BrothEnrichment
        };
        var selectiveBroth = new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 2, StepName = "Selective Broth", MediaTypeId = selectiveBrothId,
            IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 41, TemperatureMax = 43,
            IsFinalStep = false, StepType = StepType.SelectiveBroth
        };
        var selectivePlating = new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 3, StepName = "Selective Plating", MediaTypeId = selectiveAgarId,
            IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37,
            IsFinalStep = false, StepType = StepType.SelectivePlating, TargetOrganismId = organismId
        };
        var confirmatory = new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 4, StepName = "Confirmatory Plating", MediaTypeId = selectiveAgarId,
            IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37,
            IsFinalStep = false, StepType = StepType.ConfirmatoryPlating, TargetOrganismId = organismId,
            ConfirmatoryMediaCount = confirmatoryMedia.Length > 0 ? confirmatoryMedia.Length : 1
        };
        var biochemical = new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 5, StepName = "Biochemical Test", MediaTypeId = null,
            IncubationMinHours = 0, IncubationMaxHours = 0, TemperatureMin = 35, TemperatureMax = 37,
            IsFinalStep = true, StepType = StepType.BiochemicalTest, PhenotypicTestType = PhenotypicTestType.IdentificationKit
        };
        db.TestWorkflowSteps.AddRange(tsb, selectiveBroth, selectivePlating, confirmatory, biochemical);
        db.SaveChanges();

        AddStepMedium(db, tsb.Id, "Tryptone Soya Broth", 35, 37, isRequired: true, order: 1);
        AddStepMedium(db, selectiveBroth.Id, "Rappaport Vassiliadis Broth", 41, 43, isRequired: true, order: 1);
        AddStepMedium(db, selectivePlating.Id, selectivePlatingMedium, 35, 37, isRequired: true, order: 1);
        for (var i = 0; i < confirmatoryMedia.Length; i++)
        {
            var (name, tempMin, tempMax) = confirmatoryMedia[i];
            AddStepMedium(db, confirmatory.Id, name, tempMin, tempMax, isRequired: false, order: i + 1);
        }
        db.SaveChanges();
    }

    // Resolves the medium by Material name, creating nothing - a missing
    // material means that medium simply is not offered, which the
    // template validator will surface the next time the step is saved.
    private static void AddStepMedium(MicroLimsDbContext db, int stepId, string materialName, decimal tempMin, decimal tempMax, bool isRequired, int order)
    {
        var materialId = db.Materials
            .Where(m => m.MaterialType == MaterialType.DehydratedMedia && m.MaterialName == materialName)
            .Select(m => (int?)m.Id).FirstOrDefault();
        if (materialId is null) { Console.WriteLine($"Seed: material '{materialName}' not found - step media skipped."); return; }

        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia
        {
            TestWorkflowStepId = stepId, MaterialId = materialId.Value,
            TempMin = tempMin, TempMax = tempMax, IsRequired = isRequired, DisplayOrder = order
        });
    }
}
