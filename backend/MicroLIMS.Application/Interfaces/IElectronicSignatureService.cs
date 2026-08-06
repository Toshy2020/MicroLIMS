using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces;

public interface IElectronicSignatureService
{
    // Re-verifies the signer's password (11.200(a)(1) - being logged in
    // is not sufficient, signing requires the signer's own credentials
    // at the moment of signing). Throws before any state mutation if
    // verification fails; queues the new signature on the shared
    // DbContext without saving, so the caller can commit it atomically
    // alongside its own state change in one SaveChangesAsync.
    Task<ElectronicSignature> SignAsync(int userId, string password, SignatureMeaning meaning, string entityType, int entityId, string? comment, string? ipAddress);
}
