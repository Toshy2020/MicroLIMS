using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services.DocumentControl;

public class TrainingMatrixService : ITrainingMatrixService
{
    private readonly MicroLimsDbContext _db;
    private readonly ILogger<TrainingMatrixService> _logger;

    public TrainingMatrixService(
        MicroLimsDbContext db,
        ILogger<TrainingMatrixService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TrainingMatrixGridDto> GetTrainingMatrixGridAsync(
        TrainingMatrixFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;

        // 1. Fetch Users
        var usersQuery = _db.Users
            .Include(u => u.Role)
            .Where(u => u.IsActive)
            .AsNoTracking()
            .AsQueryable();

        if (filter.RoleId.HasValue)
            usersQuery = usersQuery.Where(u => u.RoleId == filter.RoleId.Value);

        if (filter.UserId.HasValue)
            usersQuery = usersQuery.Where(u => u.Id == filter.UserId.Value);

        var users = await usersQuery
            .OrderBy(u => u.FullName)
            .Take(200) // bounded page limit for matrix performance
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();

        // 2. Fetch Document Masters
        var docsQuery = _db.DocumentMasters
            .Include(d => d.DocumentType)
            .Include(d => d.Department)
            .Include(d => d.CurrentEffectiveRevision)
            .Where(d => d.RecordStatus == DocumentRecordStatus.Active && d.CurrentEffectiveRevisionId != null)
            .AsNoTracking()
            .AsQueryable();

        if (filter.DepartmentId.HasValue)
            docsQuery = docsQuery.Where(d => d.DepartmentId == filter.DepartmentId.Value);

        if (filter.DocumentTypeId.HasValue)
            docsQuery = docsQuery.Where(d => d.DocumentTypeId == filter.DocumentTypeId.Value);

        if (filter.DocumentMasterId.HasValue)
            docsQuery = docsQuery.Where(d => d.Id == filter.DocumentMasterId.Value);

        var docs = await docsQuery
            .OrderBy(d => d.CompanyDocumentCode)
            .Take(100) // bounded column limit
            .ToListAsync(cancellationToken);

        var docMasterIds = docs.Select(d => d.Id).ToList();

        // 3. Fetch Curriculum Items for mapped roles
        var roleIds = users.Select(u => u.RoleId).Distinct().ToList();
        var curriculumItems = await _db.DocumentRoleCurriculumItems
            .Include(i => i.DocumentRoleCurriculum)
            .Where(i => i.DocumentRoleCurriculum.IsActive && roleIds.Contains(i.DocumentRoleCurriculum.RoleId) && docMasterIds.Contains(i.DocumentMasterId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var curriculumLookup = new HashSet<(int RoleId, int MasterId)>(
            curriculumItems.Select(ci => (ci.DocumentRoleCurriculum.RoleId, ci.DocumentMasterId)));

        // 4. Fetch all training assignments for these users and docs
        var assignments = await _db.DocumentTrainingAssignments
            .Include(a => a.DocumentRevision)
            .Where(a => userIds.Contains(a.AssignedUserId) && docMasterIds.Contains(a.DocumentMasterId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var assignmentsByUserAndMaster = assignments
            .GroupBy(a => (a.AssignedUserId, a.DocumentMasterId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedAtUtc).ToList());

        // 5. Build Cells
        var cells = new List<TrainingMatrixCellDto>();
        int totalCompliant = 0;
        int totalPending = 0;
        int totalOverdue = 0;
        int totalSupersededGap = 0;

        foreach (var u in users)
        {
            foreach (var d in docs)
            {
                var isRequired = curriculumLookup.Contains((u.RoleId, d.Id));
                assignmentsByUserAndMaster.TryGetValue((u.Id, d.Id), out var userDocAssignments);

                var effRevId = d.CurrentEffectiveRevisionId;
                var effRevNum = d.CurrentEffectiveRevision?.RevisionNumber;

                // Evaluate cell status
                string status = "NotAssigned";
                DocumentTrainingAssignment? currentRevAssignment = null;
                DocumentTrainingAssignment? latestAssignment = null;
                DateTime? dueDateUtc = null;
                DateTime? ackAtUtc = null;
                bool isOverdue = false;
                double? daysDelta = null;

                if (userDocAssignments != null && userDocAssignments.Count > 0)
                {
                    latestAssignment = userDocAssignments[0];
                    currentRevAssignment = userDocAssignments.FirstOrDefault(a => a.DocumentRevisionId == effRevId);

                    var qualifiedPrior = userDocAssignments.Any(a =>
                        (a.Status == TrainingAssignmentStatus.Acknowledged || a.Status == TrainingAssignmentStatus.CompletedPassed) &&
                        a.DocumentRevisionId != effRevId);

                    if (currentRevAssignment != null)
                    {
                        dueDateUtc = currentRevAssignment.DueDateUtc;
                        ackAtUtc = currentRevAssignment.AcknowledgedAtUtc;
                        daysDelta = Math.Round((currentRevAssignment.DueDateUtc - nowUtc).TotalDays, 1);

                        if (currentRevAssignment.Status == TrainingAssignmentStatus.Acknowledged ||
                            currentRevAssignment.Status == TrainingAssignmentStatus.CompletedPassed)
                        {
                            status = "Qualified";
                            totalCompliant++;
                        }
                        else if (qualifiedPrior)
                        {
                            status = "TrainedOnSupersededOnly";
                            totalSupersededGap++;
                        }
                        else if (currentRevAssignment.DueDateUtc < nowUtc || currentRevAssignment.Status == TrainingAssignmentStatus.Overdue)
                        {
                            status = "Overdue";
                            isOverdue = true;
                            totalOverdue++;
                        }
                        else
                        {
                            status = "Pending";
                            totalPending++;
                        }
                    }
                    else
                    {
                        // Check if trained on superseded revision
                        if (qualifiedPrior)
                        {
                            status = "TrainedOnSupersededOnly";
                            totalSupersededGap++;
                        }
                        else
                        {
                            status = latestAssignment.Status.ToString();
                        }
                    }
                }
                else if (isRequired)
                {
                    status = "Pending"; // Required by curriculum but not yet individually dispatched
                    totalPending++;
                }

                // Filter by compliance status if requested
                if (!string.IsNullOrEmpty(filter.ComplianceStatus) && !status.Equals(filter.ComplianceStatus, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                cells.Add(new TrainingMatrixCellDto(
                    UserId: u.Id,
                    UserName: u.FullName,
                    DocumentMasterId: d.Id,
                    CompanyDocumentCode: d.CompanyDocumentCode,
                    DocumentTitle: d.Title,
                    CurrentEffectiveRevisionId: effRevId,
                    CurrentEffectiveRevisionNumber: effRevNum,
                    AssignmentId: currentRevAssignment?.Id ?? latestAssignment?.Id,
                    AssignedRevisionId: currentRevAssignment?.DocumentRevisionId ?? latestAssignment?.DocumentRevisionId,
                    AssignedRevisionNumber: currentRevAssignment?.DocumentRevision?.RevisionNumber ?? latestAssignment?.DocumentRevision?.RevisionNumber,
                    CellStatus: status,
                    DueDateUtc: dueDateUtc,
                    AcknowledgedAtUtc: ackAtUtc,
                    IsOverdue: isOverdue,
                    DaysRemainingOrOverdue: daysDelta,
                    IsRequiredByCurriculum: isRequired
                ));
            }
        }

        var userHeaders = users.Select(u => new TrainingMatrixUserHeaderDto(
            UserId: u.Id,
            FullName: u.FullName,
            Username: u.Username,
            RoleId: u.RoleId,
            RoleName: u.Role?.Name ?? "Analyst",
            DepartmentId: null,
            DepartmentName: null
        )).ToList();

        var docHeaders = docs.Select(d => new TrainingMatrixDocumentHeaderDto(
            DocumentMasterId: d.Id,
            CompanyDocumentCode: d.CompanyDocumentCode,
            MicroLimsDocumentId: d.MicroLimsDocumentId,
            Title: d.Title,
            DocumentTypeName: d.DocumentType?.Name ?? "SOP",
            CurrentEffectiveRevisionId: d.CurrentEffectiveRevisionId,
            CurrentEffectiveRevisionNumber: d.CurrentEffectiveRevision?.RevisionNumber
        )).ToList();

        return new TrainingMatrixGridDto(
            Users: userHeaders,
            Documents: docHeaders,
            Cells: cells,
            TotalUsers: userHeaders.Count,
            TotalDocuments: docHeaders.Count,
            TotalCompliantCells: totalCompliant,
            TotalPendingCells: totalPending,
            TotalOverdueCells: totalOverdue,
            TotalSupersededGapCells: totalSupersededGap
        );
    }

    public async Task<ComplianceKpiSummaryDto> GetComplianceKpisAsync(
        int? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;

        var mastersQuery = _db.DocumentMasters
            .Include(d => d.Department)
            .Where(d => d.RecordStatus == DocumentRecordStatus.Active && d.CurrentEffectiveRevisionId != null)
            .AsNoTracking()
            .AsQueryable();

        if (departmentId.HasValue)
            mastersQuery = mastersQuery.Where(d => d.DepartmentId == departmentId.Value);

        var masters = await mastersQuery.ToListAsync(cancellationToken);
        var masterIds = masters.Select(m => m.Id).ToList();

        var assignments = await _db.DocumentTrainingAssignments
            .Include(a => a.DocumentMaster)
                .ThenInclude(m => m.Department)
            .Where(a => masterIds.Contains(a.DocumentMasterId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        int totalUsers = await _db.Users.CountAsync(u => u.IsActive, cancellationToken);
        int totalAssignments = assignments.Count;

        int completed = assignments.Count(a => a.Status == TrainingAssignmentStatus.Acknowledged || a.Status == TrainingAssignmentStatus.CompletedPassed);
        int overdue = assignments.Count(a => (a.DueDateUtc < nowUtc && a.Status != TrainingAssignmentStatus.Acknowledged && a.Status != TrainingAssignmentStatus.CompletedPassed && a.Status != TrainingAssignmentStatus.Cancelled && a.Status != TrainingAssignmentStatus.SupersededIncomplete) || a.Status == TrainingAssignmentStatus.Overdue);
        int pending = assignments.Count(a => (a.Status == TrainingAssignmentStatus.Assigned || a.Status == TrainingAssignmentStatus.Reading) && a.DueDateUtc >= nowUtc);

        // Calculate superseded gaps: users qualified on a prior revision of active master who haven't completed current effective revision
        var effRevLookup = masters.ToDictionary(m => m.Id, m => m.CurrentEffectiveRevisionId);
        var supersededGaps = assignments
            .GroupBy(a => (a.AssignedUserId, a.DocumentMasterId))
            .Count(g =>
            {
                if (!effRevLookup.TryGetValue(g.Key.DocumentMasterId, out var effId) || effId == null) return false;
                bool qualifiedOnCurrent = g.Any(a => a.DocumentRevisionId == effId && (a.Status == TrainingAssignmentStatus.Acknowledged || a.Status == TrainingAssignmentStatus.CompletedPassed));
                if (qualifiedOnCurrent) return false;
                return g.Any(a => a.DocumentRevisionId != effId && (a.Status == TrainingAssignmentStatus.Acknowledged || a.Status == TrainingAssignmentStatus.CompletedPassed));
            });

        double overallRate = totalAssignments > 0 ? Math.Round(((double)completed / totalAssignments) * 100.0, 1) : 100.0;

        // Department breakdown
        var depts = await _db.DocumentDepartments.Where(d => d.IsActive).AsNoTracking().ToListAsync(cancellationToken);
        var deptKpis = new List<DepartmentComplianceKpiDto>();

        foreach (var dept in depts)
        {
            var deptAssignments = assignments.Where(a => a.DocumentMaster.DepartmentId == dept.Id).ToList();
            int req = deptAssignments.Count;
            int comp = deptAssignments.Count(a => a.Status == TrainingAssignmentStatus.Acknowledged || a.Status == TrainingAssignmentStatus.CompletedPassed);
            int ovd = deptAssignments.Count(a => (a.DueDateUtc < nowUtc && a.Status != TrainingAssignmentStatus.Acknowledged && a.Status != TrainingAssignmentStatus.CompletedPassed && a.Status != TrainingAssignmentStatus.Cancelled && a.Status != TrainingAssignmentStatus.SupersededIncomplete) || a.Status == TrainingAssignmentStatus.Overdue);
            double rate = req > 0 ? Math.Round(((double)comp / req) * 100.0, 1) : 100.0;

            deptKpis.Add(new DepartmentComplianceKpiDto(
                DepartmentId: dept.Id,
                DepartmentName: dept.Name,
                RequiredCount: req,
                CompletedCount: comp,
                OverdueCount: ovd,
                ComplianceRatePercentage: rate
            ));
        }

        // Top overdue documents
        var topOverdue = assignments
            .Where(a => (a.DueDateUtc < nowUtc && a.Status != TrainingAssignmentStatus.Acknowledged && a.Status != TrainingAssignmentStatus.CompletedPassed && a.Status != TrainingAssignmentStatus.Cancelled && a.Status != TrainingAssignmentStatus.SupersededIncomplete) || a.Status == TrainingAssignmentStatus.Overdue)
            .GroupBy(a => (a.DocumentMasterId, a.DocumentMaster.CompanyDocumentCode, a.DocumentMaster.Title))
            .Select(g => new TopOverdueDocumentKpiDto(
                DocumentMasterId: g.Key.DocumentMasterId,
                CompanyDocumentCode: g.Key.CompanyDocumentCode,
                Title: g.Key.Title,
                OverdueCount: g.Count()
            ))
            .OrderByDescending(x => x.OverdueCount)
            .Take(10)
            .ToList();

        return new ComplianceKpiSummaryDto(
            TotalTrackedUsers: totalUsers,
            TotalEffectiveDocuments: masters.Count,
            TotalRequiredAssignments: totalAssignments,
            CompletedAssignments: completed,
            PendingAssignments: pending,
            OverdueAssignments: overdue,
            SupersededGapCount: supersededGaps,
            OverallComplianceRatePercentage: overallRate,
            DepartmentCompliance: deptKpis.OrderBy(d => d.ComplianceRatePercentage).ToList(),
            TopOverdueDocuments: topOverdue
        );
    }

    public async Task<UserComplianceDetailDto?> GetUserComplianceDetailAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null) return null;

        var grid = await GetTrainingMatrixGridAsync(new TrainingMatrixFilterDto { UserId = userId }, cancellationToken);

        int total = grid.Cells.Count;
        int completed = grid.Cells.Count(c => c.CellStatus == "Qualified");
        int pending = grid.Cells.Count(c => c.CellStatus == "Pending");
        int overdue = grid.Cells.Count(c => c.CellStatus == "Overdue");
        int superseded = grid.Cells.Count(c => c.CellStatus == "TrainedOnSupersededOnly");

        double rate = total > 0 ? Math.Round(((double)completed / total) * 100.0, 1) : 100.0;

        return new UserComplianceDetailDto(
            UserId: user.Id,
            FullName: user.FullName,
            Username: user.Username,
            RoleName: user.Role?.Name ?? "Analyst",
            DepartmentName: null,
            TotalRequiredDocuments: total,
            CompletedDocuments: completed,
            PendingDocuments: pending,
            OverdueDocuments: overdue,
            SupersededGapDocuments: superseded,
            ComplianceRatePercentage: rate,
            DocumentStatuses: grid.Cells
        );
    }

    public async Task<DocumentComplianceDetailDto?> GetDocumentComplianceDetailAsync(
        int documentMasterId,
        CancellationToken cancellationToken = default)
    {
        var doc = await _db.DocumentMasters
            .Include(d => d.CurrentEffectiveRevision)
            .FirstOrDefaultAsync(d => d.Id == documentMasterId, cancellationToken);

        if (doc == null) return null;

        var grid = await GetTrainingMatrixGridAsync(new TrainingMatrixFilterDto { DocumentMasterId = documentMasterId }, cancellationToken);

        int total = grid.Cells.Count;
        int completed = grid.Cells.Count(c => c.CellStatus == "Qualified");
        int pending = grid.Cells.Count(c => c.CellStatus == "Pending");
        int overdue = grid.Cells.Count(c => c.CellStatus == "Overdue");
        int superseded = grid.Cells.Count(c => c.CellStatus == "TrainedOnSupersededOnly");

        return new DocumentComplianceDetailDto(
            DocumentMasterId: doc.Id,
            CompanyDocumentCode: doc.CompanyDocumentCode,
            MicroLimsDocumentId: doc.MicroLimsDocumentId,
            Title: doc.Title,
            CurrentEffectiveRevisionId: doc.CurrentEffectiveRevisionId,
            CurrentEffectiveRevisionNumber: doc.CurrentEffectiveRevision?.RevisionNumber,
            AssignedUserCount: total,
            CompletedUserCount: completed,
            PendingUserCount: pending,
            OverdueUserCount: overdue,
            SupersededGapUserCount: superseded,
            UserStatuses: grid.Cells
        );
    }
}
