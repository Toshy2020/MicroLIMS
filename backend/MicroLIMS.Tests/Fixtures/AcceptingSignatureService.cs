using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Tests.Fixtures;

// For tests of an action that happens to be signed but are about something
// else: it queues a signature exactly like the real service, with no
// password check. Tests of the signing itself use the real service.
public sealed class AcceptingSignatureService : IElectronicSignatureService
{
    private readonly IMicroLimsDbContext _db;

    public AcceptingSignatureService(IMicroLimsDbContext db) => _db = db;

    public Task<ElectronicSignature> SignAsync(int userId, string password, SignatureMeaning meaning, string entityType, int entityId, string? comment, string? ipAddress)
    {
        var signature = new ElectronicSignature
        {
            UserId = userId,
            UserFullNameSnapshot = "Test Signer",
            UsernameSnapshot = "testsigner",
            RoleSnapshot = "SystemAdministrator",
            MeaningOfSignature = meaning,
            EntityType = entityType,
            EntityId = entityId,
            Comment = comment,
            IpAddress = ipAddress
        };
        _db.ElectronicSignatures.Add(signature);
        return Task.FromResult(signature);
    }
}
