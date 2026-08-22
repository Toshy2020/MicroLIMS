using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class SampleAssignmentService
{
    private readonly MicroLimsDbContext _db;
    private readonly ITestWorkspaceService _workspaceService;

    public SampleAssignmentService(MicroLimsDbContext db, ITestWorkspaceService workspaceService)
    {
        _db = db;
        _workspaceService = workspaceService;
    }

    public async Task<SampleDto> AssignAnalystAsync(int sampleId, int? analystUserId, int actingUserId, string? reason = null)
    {
        var sample = await _db.Samples
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        string? newAnalystName = null;
        if (analystUserId.HasValue)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == analystUserId.Value)
                ?? throw new InvalidOperationException($"User {analystUserId.Value} not found.");

            if (!user.IsActive)
                throw new InvalidOperationException($"User '{user.FullName}' is not active.");

            newAnalystName = user.FullName;
        }

        var previousOrder = sample.TestOrders.FirstOrDefault(t => t.AssignedAnalystId.HasValue);
        int? previousAnalystId = previousOrder?.AssignedAnalystId;
        string? previousAnalystName = null;
        if (previousAnalystId.HasValue)
        {
            var prevUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == previousAnalystId.Value);
            previousAnalystName = prevUser?.FullName ?? $"User #{previousAnalystId.Value}";
        }

        // Assign/reassign analyst to all test orders on this sample that are not already finished
        var targetOrders = sample.TestOrders
            .Where(t => !t.IsSuperseded && t.Status != ApprovalStatus.Approved && t.Status != ApprovalStatus.Rejected)
            .ToList();

        foreach (var order in targetOrders)
        {
            order.AssignedAnalystId = analystUserId;
        }

        // Record GMP Audit Trail for reassignment / assignment
        string action = previousAnalystId == null
            ? "AssignedAnalyst"
            : (analystUserId == null ? "UnassignedAnalyst" : "ReassignedAnalyst");

        string previousDisplay = previousAnalystName ?? "Unassigned";
        string newDisplay = newAnalystName ?? "Unassigned";
        string formattedNewValue = string.IsNullOrWhiteSpace(reason)
            ? newDisplay
            : $"{newDisplay} | Reason: {reason.Trim()}";

        _db.AuditLogs.Add(new Domain.Entities.AuditLog
        {
            EntityName = "Sample",
            EntityId = sampleId.ToString(),
            Action = action,
            PreviousValue = previousDisplay,
            NewValue = formattedNewValue,
            UserId = actingUserId,
            Timestamp = DateTime.UtcNow,
            SampleId = sampleId,
            SampleReferenceNumber = sample.ReferenceNumber,
            BatchNumber = sample.BatchNumber,
            ControlNumber = sample.ControlNumber
        });

        await _db.SaveChangesAsync();

        var updatedSample = await _workspaceService.GetSampleAsync(sampleId);
        return updatedSample ?? TestingWorkspaceService.ToDto(sample);
    }
}
