using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

// Response types for the master-data endpoints (api/masterdata/...). Each
// mirrors the entity's own fields; related records are left out unless the
// endpoint loads them on purpose.

public class CauseOfTestingResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = null!;
    public bool IsActive { get; init; }

    public static CauseOfTestingResponse From(CauseOfTesting e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Name = e.Name,
        IsActive = e.IsActive,
    };
}

public class SamplerResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = null!;
    public bool IsActive { get; init; }

    public static SamplerResponse From(Sampler e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Name = e.Name,
        IsActive = e.IsActive,
    };
}

public class ProductionStageResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = null!;
    public bool IsActive { get; init; }
    public ProductionStageRole Role { get; init; }

    public static ProductionStageResponse From(ProductionStage e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Name = e.Name,
        IsActive = e.IsActive,
        Role = e.Role,
    };
}

public class DiluentTypeResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public bool RequiresBatchTracking { get; init; }
    public int? MaterialId { get; init; }

    public static DiluentTypeResponse From(DiluentType e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        RequiresBatchTracking = e.RequiresBatchTracking,
        MaterialId = e.MaterialId,
    };
}

public class NeutralizerResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public bool IsActive { get; init; }

    public static NeutralizerResponse From(Neutralizer e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        IsActive = e.IsActive,
    };
}

public class OrganismResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string ScientificName { get; init; } = null!;
    public string? AtccNumber { get; init; }
    public string? CommonName { get; init; }
    public string? Description { get; init; }

    public static OrganismResponse From(Organism e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        ScientificName = e.ScientificName,
        AtccNumber = e.AtccNumber,
        CommonName = e.CommonName,
        Description = e.Description,
    };
}

public class MachineResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = null!;

    public static MachineResponse From(Machine e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Name = e.Name,
    };
}

public class MachinePartResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public int MachineId { get; init; }
    public string Name { get; init; } = null!;

    public static MachinePartResponse From(MachinePart e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        MachineId = e.MachineId,
        Name = e.Name,
    };
}

public class MachinePartConfigurationResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public int MachinePartId { get; init; }
    public string TestType { get; init; } = null!;
    public string TestCode { get; init; } = null!;
    public string AlertLimit { get; init; } = null!;
    public string ActionLimit { get; init; } = null!;
    public string SpecLimit { get; init; } = null!;
    public string Unit { get; init; } = null!;
    public bool IsPathogenTest { get; init; }

    public static MachinePartConfigurationResponse From(MachinePartConfiguration e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        MachinePartId = e.MachinePartId,
        TestType = e.TestType,
        TestCode = e.TestCode,
        AlertLimit = e.AlertLimit,
        ActionLimit = e.ActionLimit,
        SpecLimit = e.SpecLimit,
        Unit = e.Unit,
        IsPathogenTest = e.IsPathogenTest,
    };
}

public class EmDepartmentResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = null!;
    public string Class { get; init; } = null!;
    public string TestingFrequency { get; init; } = null!;

    public static EmDepartmentResponse From(Department e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Name = e.Name,
        Class = e.Class,
        TestingFrequency = e.TestingFrequency,
    };
}

public class RoomResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = null!;
    public int DepartmentId { get; init; }
    public string GradeClassification { get; init; } = null!;

    public static RoomResponse From(Room e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Name = e.Name,
        DepartmentId = e.DepartmentId,
        GradeClassification = e.GradeClassification,
    };
}

public class RoomTestConfigurationResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public int RoomId { get; init; }
    public string TestType { get; init; } = null!;
    public string TestCode { get; init; } = null!;
    public string AlertLimit { get; init; } = null!;
    public string ActionLimit { get; init; } = null!;
    public string SpecLimit { get; init; } = null!;
    public string Unit { get; init; } = null!;

    public static RoomTestConfigurationResponse From(RoomTestConfiguration e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        RoomId = e.RoomId,
        TestType = e.TestType,
        TestCode = e.TestCode,
        AlertLimit = e.AlertLimit,
        ActionLimit = e.ActionLimit,
        SpecLimit = e.SpecLimit,
        Unit = e.Unit,
    };
}

public class WaterDepartmentResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = null!;

    public static WaterDepartmentResponse From(WaterDepartment e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Name = e.Name,
    };
}

public class WaterSamplingPointResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Code { get; init; } = null!;
    public string Location { get; init; } = null!;
    public string TestingFrequency { get; init; } = null!;
    public List<string> AssignedTestCodes { get; init; } = new();
    public int? WaterDepartmentId { get; init; }

    public static WaterSamplingPointResponse From(WaterSamplingPoint e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Code = e.Code,
        Location = e.Location,
        TestingFrequency = e.TestingFrequency,
        AssignedTestCodes = e.AssignedTestCodes.ToList(),
        WaterDepartmentId = e.WaterDepartmentId,
    };
}

public class SamplingConfigurationResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public int WaterSamplingPointId { get; init; }
    public string TestCode { get; init; } = null!;
    public string AlertLimit { get; init; } = null!;
    public string ActionLimit { get; init; } = null!;
    public string SpecLimit { get; init; } = null!;
    public string Unit { get; init; } = null!;

    public static SamplingConfigurationResponse From(SamplingConfiguration e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        WaterSamplingPointId = e.WaterSamplingPointId,
        TestCode = e.TestCode,
        AlertLimit = e.AlertLimit,
        ActionLimit = e.ActionLimit,
        SpecLimit = e.SpecLimit,
        Unit = e.Unit,
    };
}
