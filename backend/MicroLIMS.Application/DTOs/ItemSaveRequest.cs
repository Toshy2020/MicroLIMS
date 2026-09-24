using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

// What a client may set when creating or updating an Item - and nothing
// else. POST /api/items used to bind the Item entity itself, so a request
// could also set Id, IsActive and a nested Specifications graph (the
// microbial limits), none of which item creation is meant to touch:
// specifications have their own endpoints, and freezing has its own.
// Mirrors the frontend's ItemSaveRequest (ItemService.ts).
public record ItemSaveRequest(
    string Name,
    string Code,
    SampleCategory Category,
    string? SopNumber,
    List<ItemTestRequest>? AssignedTests);

public record ItemTestRequest(string TestCode, string DisplayName);
