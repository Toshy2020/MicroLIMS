using System.Text.Json;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Tests;

// Seeds one titration test order (FP section) with a standardized titrant preparation.
// Used by the in-memory rule tests and the Postgres integration test.
public class TitrationScenario
{
    public const string Password = "Password123!";

    public DocumentSection Section = null!;
    public int UserId;
    public Equipment Equipment = null!;
    public TestDefinition Test = null!;
    public Specification Spec = null!;
    public TestOrder Order = null!;
    public SolutionMaster TitrantMaster = null!;
    public SolutionPreparation TitrantPrep = null!;
    public TitrantStandardization? Standardization;
    public SolutionMaster? ExcessMaster;
    public SolutionPreparation? ExcessPrep;
    public MaterialMasterEntry IndicatorEntry = null!;
    public MaterialMasterEntry? StandardEntry;
    public Material? StandardLot;

    public class Options
    {
        public Action<TestDefinition>? Tweak;
        public bool Standardized = true;
        public int ValidityDays = 30;       // 0 = restandardize before each use
        public int StandardizedDaysAgo = 0;
        public decimal? StandardizationTemperatureC = 25m;
        public decimal Factor = 1.0023m;
        public TitrantStrengthUnit Unit = TitrantStrengthUnit.Normal;
        public decimal Nominal = 0.1m;
        public bool WithExcess;
        public bool WithStandard;
        public decimal StandardPurity = 99.5m;
        public decimal? StandardMoisture = 0.5m;
        public MaterialType StandardType = MaterialType.ReferenceStandard;
        public decimal StandardQuantityG = 5m;
        public DateTime? StandardExpiry;
        public int? ExistingUserId;          // Postgres: the seeded user
        public Action<Specification>? TweakSpec;
    }

    public static TitrationScenario Seed(MicroLimsDbContext db, Options? o = null)
    {
        o ??= new Options();
        var uid = Guid.NewGuid().ToString("N")[..6];
        var s = new TitrationScenario();

        var micro = db.DocumentSections.FirstOrDefault(x => x.Code == "MICRO") ?? TestServiceFactory.EnsureMicroSection(db);
        s.Section = db.DocumentSections.FirstOrDefault(x => x.Code == "FP")
            ?? db.DocumentSections.Add(new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true }).Entity;
        db.SaveChanges();

        if (o.ExistingUserId.HasValue)
        {
            s.UserId = o.ExistingUserId.Value;
            var u = db.Users.First(x => x.Id == s.UserId);
            u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
            u.IsActive = true;
        }
        else
        {
            var role = db.Roles.FirstOrDefault(r => r.Type == RoleType.Analyst)
                ?? db.Roles.Add(new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true }).Entity;
            db.SaveChanges();
            var user = new User { Username = "ti_" + uid, FullName = "Titration Analyst", RoleId = role.Id, Role = role,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password), IsActive = true };
            db.Users.Add(user);
            db.SaveChanges();
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = s.Section.DepartmentId, SectionId = s.Section.Id });
            db.SaveChanges();
            s.UserId = user.Id;
        }

        s.Equipment = new Equipment { Name = $"Titrator {uid}", Code = $"EQ-TIT-{uid}", Type = EquipmentType.Titrator, SectionId = s.Section.Id };
        db.Equipment.Add(s.Equipment);

        s.TitrantMaster = NewMaster(db, s.Section.Id, $"Titrant {uid}", o.Nominal, o.Unit);
        if (o.WithExcess) s.ExcessMaster = NewMaster(db, s.Section.Id, $"Excess {uid}", o.Nominal, o.Unit);
        db.SaveChanges();

        s.TitrantPrep = NewPrep(db, s.Section.Id, s.TitrantMaster, $"VS-{uid}-1", o.Unit, o.Nominal);
        if (s.ExcessMaster != null) s.ExcessPrep = NewPrep(db, s.Section.Id, s.ExcessMaster, $"VS-{uid}-2", o.Unit, o.Nominal);
        db.SaveChanges();

        if (o.Standardized)
        {
            AddStandardization(db, s.TitrantPrep, s.UserId, o.Factor, o.ValidityDays, o.StandardizedDaysAgo, o.StandardizationTemperatureC);
            if (s.ExcessPrep != null) AddStandardization(db, s.ExcessPrep, s.UserId, 1.0010m, 30, 0, 25m);
            db.SaveChanges();
            s.Standardization = db.TitrantStandardizations.First(x => x.SolutionPreparationId == s.TitrantPrep.Id);
        }

        s.IndicatorEntry = new MaterialMasterEntry
        {
            SectionId = s.Section.Id, Code = $"IND-{uid}", Name = "Starch", Category = MaterialMasterCategory.Indicator,
            BaseUnit = MaterialUnit.Gram, IsActive = true, CreatedByUserId = s.UserId, CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = s.UserId, LastModifiedAt = DateTime.UtcNow,
        };
        db.MaterialMasterEntries.Add(s.IndicatorEntry);
        db.SaveChanges();

        if (o.WithStandard)
        {
            s.StandardEntry = new MaterialMasterEntry
            {
                SectionId = s.Section.Id, Code = $"STD-{uid}", Name = $"Standard {uid}", Category = MaterialMasterCategory.ReferenceStandard,
                BaseUnit = MaterialUnit.Gram, IsActive = true, CreatedByUserId = s.UserId, CreatedAt = DateTime.UtcNow,
                LastModifiedByUserId = s.UserId, LastModifiedAt = DateTime.UtcNow,
            };
            db.MaterialMasterEntries.Add(s.StandardEntry);
            db.SaveChanges();
            s.StandardLot = new Material
            {
                SectionId = s.Section.Id, MaterialType = o.StandardType, MaterialMasterEntryId = s.StandardEntry.Id,
                MaterialName = s.StandardEntry.Name, ManufacturerName = "Acme", BatchNumber = $"LOT-{uid}",
                Code = o.StandardType == MaterialType.WorkingStandard ? $"WS-{uid}" : null,
                ReceivingDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = o.StandardExpiry ?? DateTime.UtcNow.AddYears(1),
                Location = "Shelf", QuantityReceived = o.StandardQuantityG, QuantityRemaining = o.StandardQuantityG, Unit = MaterialUnit.Gram,
                Purity = o.StandardPurity, MoisturePercent = o.StandardMoisture, CreatedByUserId = s.UserId, LastModifiedByUserId = s.UserId,
            };
            db.Materials.Add(s.StandardLot);
            db.SaveChanges();
        }

        // Default test: ascorbic acid, redox, direct, USP factor (F 88.06), 2 replicates.
        s.Test = new TestDefinition
        {
            Code = $"TIT_{uid}", DisplayName = $"Titration {uid}", SectionId = s.Section.Id,
            WorkflowType = WorkflowType.Titration, EquationType = EquationType.Titration, IsActive = true, PhyschemArea = PhyschemArea.Both,
            ReplicateCount = 2, TitrationType = TitrationType.Redox, TitrationNonAqueous = null, TitrationMode = TitrationMode.Direct,
            TitrationCalculation = TitrationCalculation.UspFactor, TitrantSolutionMasterId = s.TitrantMaster.Id,
            TitrationEquivalencyFactor = 88.06m, TitrationBlankRequired = true, TitrationMaxRsdPercent = 2m,
            TitrationEndpoint = TitrationEndpoint.Visual, TitrationIndicatorEntryId = s.IndicatorEntry.Id, TitrationIndicator = "Starch", TitrationTempCorrection = false,
        };
        o.Tweak?.Invoke(s.Test);
        if (s.StandardEntry != null && s.Test.TitrationCalculation == TitrationCalculation.Relative) s.Test.TitrationStandardEntryId = s.StandardEntry.Id;
        if (s.ExcessMaster != null && s.Test.TitrationMode == TitrationMode.Residual) s.Test.TitrationExcessSolutionMasterId = s.ExcessMaster.Id;
        db.TestDefinitions.Add(s.Test);

        var item = new Item { Code = $"ITEM-{uid}", Name = $"Vitamin C {uid}", Category = SampleCategory.FinishedProduct, IsActive = true };
        db.Items.Add(item);
        db.SaveChanges();
        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = s.Test.Code, DisplayName = s.Test.DisplayName });

        s.Spec = new Specification
        {
            ItemId = item.Id, TestCode = s.Test.Code, ParameterName = "Assay", LimitType = LimitType.Range,
            LowerLimit = 98.000000m, UpperLimit = 102.000000m, /* scale as loaded from numeric(18,6) */ Unit = "%", SpecLimit = "98.0-102.0%", DisplayOrder = 1,
            ResultBasis = ResultBasis.PercentAsIs,
        };
        o.TweakSpec?.Invoke(s.Spec);
        db.Specifications.Add(s.Spec);

        var cause = db.CausesOfTesting.FirstOrDefault() ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine Testing", IsActive = true }).Entity;
        var sample = new Sample
        {
            ReferenceNumber = $"SMP-T-{uid}", Category = SampleCategory.FinishedProduct, ItemId = item.Id,
            ReceivedAt = DateTime.UtcNow, CauseOfTesting = cause, ReceivedByUserId = s.UserId, Status = SampleStatus.InTesting,
        };
        db.Samples.Add(sample);
        db.SaveChanges();

        s.Order = new TestOrder { SampleId = sample.Id, TestCode = s.Test.Code, SectionId = s.Section.Id, CurrentStep = WorkflowStep.Running, Status = ApprovalStatus.InProgress };
        db.TestOrders.Add(s.Order);
        db.SaveChanges();
        return s;
    }

    private static SolutionMaster NewMaster(MicroLimsDbContext db, int sectionId, string name, decimal nominal, TitrantStrengthUnit unit)
    {
        var m = new SolutionMaster
        {
            SectionId = sectionId, Name = name, Type = SolutionType.Titrant, ShelfLifeValue = 30, ShelfLifeUnit = ShelfLifeUnit.Days,
            StorageCondition = "RT", FinalVolumeMl = 1000m, Instructions = "-", IsActive = true,
            NominalStrength = nominal, StrengthUnit = unit, StandardizationMode = StandardizationMode.PrimaryStandard,
            CreatedByUserId = 1, LastModifiedByUserId = 1, CreatedAt = DateTime.UtcNow, LastModifiedAt = DateTime.UtcNow,
        };
        db.SolutionMasters.Add(m);
        return m;
    }

    private static SolutionPreparation NewPrep(MicroLimsDbContext db, int sectionId, SolutionMaster master, string code, TitrantStrengthUnit unit, decimal nominal)
    {
        var p = new SolutionPreparation
        {
            SectionId = sectionId, SolutionMasterId = master.Id, Type = SolutionType.Titrant, Code = code,
            Status = SolutionPreparationStatus.Prepared, StartedByUserId = 1, StartedAt = DateTime.UtcNow.AddDays(-1),
            PreparedByUserId = 1, PreparedAt = DateTime.UtcNow.AddDays(-1), ExpiresAt = DateTime.UtcNow.AddDays(20),
            RecipeSnapshotJson = JsonSerializer.Serialize(new SolutionMasterResponse
            {
                Id = master.Id, Name = master.Name, Type = SolutionType.Titrant, NominalStrength = nominal, StrengthUnit = unit,
                StorageCondition = "RT", Instructions = "-",
            }, SnapshotJson.Options),
        };
        db.SolutionPreparations.Add(p);
        return p;
    }

    private static void AddStandardization(MicroLimsDbContext db, SolutionPreparation prep, int userId, decimal factor, int validityDays, int daysAgo, decimal? temp)
    {
        var at = DateTime.UtcNow.AddDays(-daysAgo);
        var user = db.Users.First(u => u.Id == userId);
        var sig = new ElectronicSignature
        {
            UserId = userId, UserFullNameSnapshot = user.FullName, UsernameSnapshot = user.Username, RoleSnapshot = "Analyst",
            MeaningOfSignature = SignatureMeaning.TitrantStandardized, EntityType = "TitrantStandardization", EntityId = prep.Id, SignedAt = at,
        };
        db.TitrantStandardizations.Add(new TitrantStandardization
        {
            SolutionPreparationId = prep.Id, Mode = StandardizationMode.PrimaryStandard, MeanFactor = factor, Passed = true,
            StandardizedByUserId = userId, StandardizedAt = at, ValidUntil = validityDays <= 0 ? null : at.AddDays(validityDays),
            TemperatureC = temp, Signature = sig,
        });
    }
}
