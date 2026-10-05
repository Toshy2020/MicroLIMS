using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Persistence.DbContext;
using Microsoft.AspNetCore.Mvc;

namespace MicroLIMS.Tests;

// The api/masterdata controllers, built the way DI builds them, over one
// database and one request context - for tests that exercise several
// master-data areas as the same user.
public sealed class MasterDataControllers
{
    public WaterMasterDataController Water { get; }
    public EnvironmentalMonitoringMasterDataController EnvironmentalMonitoring { get; }
    public AfterCleaningMasterDataController AfterCleaning { get; }
    public SpecificationMasterDataController Specification { get; }
    public ReferenceListMasterDataController ReferenceList { get; }
    public EquipmentMasterDataController Equipment { get; }
    public MediaMasterDataController Media { get; }
    public OrganismMasterDataController Organism { get; }
    public TestDefinitionMasterDataController TestDefinition { get; }
    public TestWorkflowStepMasterDataController TestWorkflowStep { get; }
    public TestStageReplicateMasterDataController TestStageReplicate { get; }

    // Same arguments the single MasterDataController used to take.
    public MasterDataControllers(
        MicroLimsDbContext db,
        EquipmentConfigurationService configService,
        MediaProductService mediaProductService,
        MediaIncubationConditionService mediaIncubationConditionService,
        IUserSectionScopeService scope,
        ChromatographyColumnService columnService,
        SpecificationService? specificationService = null)
    {
        Water = new(new WaterMasterDataService(db));
        EnvironmentalMonitoring = new(new EnvironmentalMonitoringMasterDataService(db));
        AfterCleaning = new(new AfterCleaningMasterDataService(db));
        Specification = new(new SpecificationMasterDataService(db, scope, specificationService));
        ReferenceList = new(new ReferenceListMasterDataService(db));
        Equipment = new(new EquipmentMasterDataService(db, scope), configService, scope, columnService);
        Media = new(new MediaMasterDataService(db), mediaProductService, mediaIncubationConditionService);
        Organism = new(new OrganismMasterDataService(db));
        TestDefinition = new(TestServiceFactory.TestDefinitionMaster(db, scope));
        TestWorkflowStep = new(new TestWorkflowStepMasterDataService(db));
        TestStageReplicate = new(new TestStageReplicateMasterDataService(db, scope));
    }

    private IEnumerable<ControllerBase> All =>
    [
        Water, EnvironmentalMonitoring, AfterCleaning, Specification, ReferenceList, Equipment,
        Media, Organism, TestDefinition, TestWorkflowStep, TestStageReplicate
    ];

    public ControllerContext ControllerContext
    {
        get => TestDefinition.ControllerContext;
        set { foreach (var c in All) c.ControllerContext = value; }
    }
}
