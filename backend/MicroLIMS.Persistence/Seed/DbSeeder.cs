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

        if (!db.MediaTypes.Any())
        {
            db.MediaTypes.Add(new MediaType
            {
                Name = "Tryptic Soy Agar",
                Code = "TSA",
                Class = MediaClass.GeneralAgar,
                IncubationMinHours = 24,
                IncubationMaxHours = 48,
                RequiredTemperatureMin = 30,
                RequiredTemperatureMax = 35,
                ApprovedTestCodes = new List<string> { "TAMC" }
            });
        }

        if (!db.DiluentTypes.Any())
        {
            db.DiluentTypes.Add(new DiluentType { Name = "Buffer PhB 7.2", RequiresBatchTracking = false });
        }

        db.SaveChanges();

        // Bootstrap: mark one starter TSA lot as already GPT-released so
        // the very first Reference Strain can be received (GPT itself
        // needs a released media lot for its identity-confirmation panel -
        // this breaks that chicken-and-egg only for the initial seed).
        if (!db.Media.Any())
        {
            var tsa = db.MediaTypes.First(m => m.Code == "TSA");
            db.Media.Add(new Media
            {
                MediaTypeId = tsa.Id,
                LotNumber = $"{tsa.Code}/01/{DateTime.UtcNow:yy}",
                ManufacturerLot = "SEED-0001",
                ManufacturerName = "Seed Data",
                ExpiryDate = DateTime.UtcNow.AddYears(1),
                Status = MediaStatus.Active,
                GptStage = GptStage.Release
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
    }
}
