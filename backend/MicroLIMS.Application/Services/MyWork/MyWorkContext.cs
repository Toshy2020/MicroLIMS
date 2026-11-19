using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.MyWork;

// Everything a source needs to answer for one caller at one instant. Built once per request.
// ReadableSectionIds is already narrowed to Lab when a laboratory was chosen; null = every section.
public sealed record MyWorkContext(int UserId, IReadOnlySet<string> Permissions, IReadOnlyCollection<int>? ReadableSectionIds,
    IReadOnlyCollection<int> MemberSectionIds, DashboardLabScope? Lab, DateTime NowUtc, DateTime EndOfLabDayUtc,
    int LookAheadDays, int ReadOverdueHours,
    int? PhyschemSectionId = null, IReadOnlyCollection<SampleCategory>? ReadablePhyschemCategories = null);

public sealed record MyWorkSourceResult(IReadOnlyList<MyWorkItem> Items, IReadOnlyDictionary<string, int> TotalsByGroup, IReadOnlyDictionary<string, int> LockedByGroup);
