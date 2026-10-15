using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

// Biological waste destruction: one signed record per autoclave cycle.
// Cryovial material becomes eligible when
//  - a thawed vial is more than 24 hours old (CryovialThaw.UsableFor), or
//  - its batch was withdrawn (Cryovial.IsDestroyed) - then every thawed
//    vial of the batch at once, plus the vials never thawed.
// Anything else loaded into the same cycle is recorded alongside as free
// text. Autoclaving is the only destruction method.
public class BiologicalWasteDestructionService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IElectronicSignatureService _signatures;
    private readonly IUserSectionScopeService _scope;
    private readonly TimeProvider _time;

    // A cycle start may sit slightly in the future of the server clock
    // (workstation clock drift) - not further.
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    public BiologicalWasteDestructionService(IMicroLimsDbContext db, IElectronicSignatureService signatures,
        IUserSectionScopeService scope, TimeProvider? timeProvider = null)
    {
        _db = db;
        _signatures = signatures;
        _scope = scope;
        _time = timeProvider ?? TimeProvider.System;
    }

    public static string ReferenceOf(int id) => $"BWD-{id:D5}";

    public async Task<BiologicalWasteEligibleDto> GetEligibleAsync(IReadOnlyCollection<int>? sectionIds)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var thawedBefore = now - CryovialThaw.UsableFor;

        var thaws = await _db.ThawEvents
            .Include(t => t.Cryovial)
            .Include(t => t.UsedInChallenges).ThenInclude(c => c.MediaEvaluation!).ThenInclude(e => e.Media)
            .Where(t => t.DestructionItem == null
                && (t.ThawedAt <= thawedBefore || t.Cryovial!.IsDestroyed)
                && (sectionIds == null || sectionIds.Contains(t.Cryovial!.Material!.SectionId)))
            .OrderBy(t => t.ThawedAt)
            .ToListAsync();

        var names = await NamesAsync(thaws.Select(t => t.ThawedByUserId));

        var batches = await _db.Cryovials
            .Where(c => c.IsDestroyed && c.VialsRemaining > 0
                && (sectionIds == null || sectionIds.Contains(c.Material!.SectionId)))
            .OrderBy(c => c.Code)
            .Select(c => new EligibleWithdrawnBatchDto(c.Id, c.Code, c.OrganismNameSnapshot, c.VialsRemaining))
            .ToListAsync();

        return new BiologicalWasteEligibleDto(
            thaws.Select(t => new EligibleThawedVialDto(
                t.Id, t.CryovialId, t.Cryovial!.Code, t.Cryovial.OrganismNameSnapshot, t.ThawedAt,
                names.TryGetValue(t.ThawedByUserId, out var n) ? n : "Unknown",
                t.Cryovial.IsDestroyed ? "Batch withdrawn" : $"Thawed over {CryovialThaw.UsableFor.TotalHours:0} h ago",
                UsedLots(t))).ToList(),
            batches);
    }

    public async Task<List<BiologicalWasteDestructionDto>> GetAllAsync(IReadOnlyCollection<int>? sectionIds) =>
        await ToDtosAsync(await Query()
            .Where(d => sectionIds == null || sectionIds.Contains(d.SectionId))
            .OrderByDescending(d => d.CycleStartedAt).ThenByDescending(d => d.Id)
            .ToListAsync());

    public async Task<BiologicalWasteDestructionDto> GetByIdAsync(int id, IReadOnlyCollection<int>? sectionIds)
    {
        var destruction = await Query().FirstOrDefaultAsync(d => d.Id == id);
        if (destruction is null || (sectionIds != null && !sectionIds.Contains(destruction.SectionId)))
            throw new NotFoundException($"Biological waste destruction {id} not found.");
        return (await ToDtosAsync(new List<BiologicalWasteDestruction> { destruction }))[0];
    }

    public Task<BiologicalWasteDestructionDto> RecordAsync(RecordBiologicalWasteDestructionRequest request, int userId, string? ipAddress) =>
        UnitOfWork.RunAsync(_db, async () =>
        {
            var destruction = await RecordCoreAsync(request, userId, ipAddress);
            return await GetByIdAsync(destruction.Id, null);
        });

    private async Task<BiologicalWasteDestruction> RecordCoreAsync(RecordBiologicalWasteDestructionRequest r, int userId, string? ipAddress)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var thawIds = (r.ThawEventIds ?? new()).Distinct().ToList();
        var batchIds = (r.WithdrawnCryovialIds ?? new()).Distinct().ToList();
        var others = r.OtherItems ?? new();

        if (string.IsNullOrWhiteSpace(r.CycleNumber))
            throw new InvalidOperationException("The autoclave cycle number is required.");
        var cycleStartedAt = DateTime.SpecifyKind(r.CycleStartedAt, DateTimeKind.Utc);
        if (cycleStartedAt > now + ClockTolerance)
            throw new InvalidOperationException("The autoclave cycle start cannot be in the future.");
        if (thawIds.Count + batchIds.Count + others.Count == 0)
            throw new InvalidOperationException("Select at least one thawed vial, withdrawn batch or other waste item destroyed in this cycle.");

        var autoclave = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == r.AutoclaveEquipmentId && e.Type == EquipmentType.Autoclave)
            ?? throw new InvalidOperationException("Selected equipment is not a valid autoclave.");
        await _scope.EnsureEquipmentAccessAsync(userId, autoclave.Id);
        var program = await _db.AutoclavePrograms.FirstOrDefaultAsync(p => p.Id == r.AutoclaveProgramId && p.EquipmentId == autoclave.Id)
            ?? throw new InvalidOperationException($"The selected program does not belong to autoclave {autoclave.Code}.");
        if (!program.IsActive)
            throw new InvalidOperationException($"Autoclave program {program.ProgramCode} is inactive.");

        var sectionIds = await _scope.GetAccessibleSectionIdsAsync(userId);
        var items = new List<BiologicalWasteDestructionItem>();

        var thaws = await _db.ThawEvents
            .Include(t => t.Cryovial!).ThenInclude(c => c.Material)
            .Include(t => t.DestructionItem)
            .Where(t => thawIds.Contains(t.Id))
            .ToListAsync();
        foreach (var id in thawIds)
        {
            var thaw = thaws.FirstOrDefault(t => t.Id == id)
                ?? throw new NotFoundException($"Thawed vial {id} not found.");
            var vial = $"{thaw.Cryovial!.Code} vial thawed {thaw.ThawedAt:dd-MMM-yyyy HH:mm}";
            if (sectionIds != null && !sectionIds.Contains(thaw.Cryovial.Material!.SectionId))
                throw new UnauthorizedAccessException($"The {vial} belongs to a laboratory section you do not have access to.");
            if (thaw.DestructionItem is not null)
                throw new InvalidOperationException($"The {vial} has already been destroyed.");
            if (!thaw.Cryovial.IsDestroyed && thaw.ThawedAt.Add(CryovialThaw.UsableFor) > now)
                throw new InvalidOperationException($"The {vial} is still within its {CryovialThaw.UsableFor.TotalHours:0}-hour use window and is not yet eligible for destruction.");

            items.Add(new BiologicalWasteDestructionItem
            {
                ItemType = BiologicalWasteItemType.ThawedVial,
                ThawEvent = thaw,
                Description = $"{vial} ({thaw.Cryovial.OrganismNameSnapshot})",
                Quantity = 1,
                Unit = "vial"
            });
        }

        var batches = await _db.Cryovials.Include(c => c.Material)
            .Where(c => batchIds.Contains(c.Id))
            .ToListAsync();
        foreach (var id in batchIds)
        {
            var batch = batches.FirstOrDefault(c => c.Id == id)
                ?? throw new NotFoundException($"Cryovial {id} not found.");
            if (sectionIds != null && !sectionIds.Contains(batch.Material!.SectionId))
                throw new UnauthorizedAccessException($"Cryovial batch {batch.Code} belongs to a laboratory section you do not have access to.");
            if (!batch.IsDestroyed)
                throw new InvalidOperationException($"Cryovial batch {batch.Code} has not been withdrawn - only a withdrawn batch's unthawed vials can be destroyed.");
            if (batch.VialsRemaining <= 0)
                throw new InvalidOperationException($"Cryovial batch {batch.Code} has no unthawed vials left to destroy.");

            items.Add(new BiologicalWasteDestructionItem
            {
                ItemType = BiologicalWasteItemType.UnthawedCryovials,
                Cryovial = batch,
                Description = $"{batch.Code} unthawed vials ({batch.OrganismNameSnapshot})",
                Quantity = batch.VialsRemaining,
                Unit = "vial"
            });
        }

        foreach (var o in others)
        {
            if (string.IsNullOrWhiteSpace(o.Description))
                throw new InvalidOperationException("Every other waste item needs a description.");
            if (o.Quantity <= 0)
                throw new InvalidOperationException($"Quantity for \"{o.Description.Trim()}\" must be greater than zero.");
            if (string.IsNullOrWhiteSpace(o.Unit))
                throw new InvalidOperationException($"Unit for \"{o.Description.Trim()}\" is required.");
            items.Add(new BiologicalWasteDestructionItem
            {
                ItemType = BiologicalWasteItemType.Other,
                Description = o.Description.Trim(),
                Quantity = o.Quantity,
                Unit = o.Unit.Trim()
            });
        }

        // Signs last of the checks and before anything is written - a
        // wrong password saves only its own failure audit row. The record
        // id does not exist yet, so the signature is pointed at it after
        // the first save (same transaction).
        var signature = await _signatures.SignAsync(userId, r.Password, SignatureMeaning.BiologicalWasteDestroyed,
            nameof(BiologicalWasteDestruction), 0, string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim(), ipAddress);

        // The withdrawn batches' unthawed vials are gone now. Changed only
        // after signing: a refused signature saves its audit row, and must
        // not save this with it.
        foreach (var batch in batches) batch.VialsRemaining = 0;

        var destruction = new BiologicalWasteDestruction
        {
            SectionId = autoclave.SectionId,
            AutoclaveEquipmentId = autoclave.Id,
            AutoclaveProgramId = program.Id,
            ProgramCodeSnapshot = program.ProgramCode,
            ProgramNameSnapshot = program.ProgramName,
            LoadTypeSnapshot = program.LoadType,
            TemperatureSnapshot = program.Temperature,
            CycleTimeMinutesSnapshot = program.CycleTimeMinutes,
            CycleNumber = r.CycleNumber.Trim(),
            CycleStartedAt = cycleStartedAt,
            Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim(),
            RecordedByUserId = userId,
            RecordedAt = now,
            Signature = signature,
            Items = items
        };
        _db.BiologicalWasteDestructions.Add(destruction);
        await _db.SaveChangesAsync();

        signature.EntityId = destruction.Id;
        await _db.SaveChangesAsync();
        return destruction;
    }

    private IQueryable<BiologicalWasteDestruction> Query() =>
        _db.BiologicalWasteDestructions
            .Include(d => d.AutoclaveEquipment)
            .Include(d => d.Signature)
            .Include(d => d.Items);

    private async Task<List<BiologicalWasteDestructionDto>> ToDtosAsync(List<BiologicalWasteDestruction> list)
    {
        var names = await NamesAsync(list.Select(d => d.RecordedByUserId));
        return list.Select(d => new BiologicalWasteDestructionDto(
            d.Id, ReferenceOf(d.Id), d.SectionId,
            d.AutoclaveEquipment?.Name ?? string.Empty, d.AutoclaveEquipment?.Code ?? string.Empty,
            d.ProgramCodeSnapshot, d.ProgramNameSnapshot, d.LoadTypeSnapshot, d.TemperatureSnapshot, d.CycleTimeMinutesSnapshot,
            d.CycleNumber, d.CycleStartedAt, d.Notes,
            names.TryGetValue(d.RecordedByUserId, out var n) ? n : "Unknown", d.RecordedAt,
            d.Signature?.UserFullNameSnapshot ?? string.Empty, d.Signature?.RoleSnapshot ?? string.Empty, d.Signature?.SignedAt ?? d.RecordedAt,
            d.Items.Count(i => i.ItemType != BiologicalWasteItemType.Other),
            d.Items.Count(i => i.ItemType == BiologicalWasteItemType.Other),
            d.Items.OrderBy(i => i.ItemType).ThenBy(i => i.Id).Select(i => new BiologicalWasteItemDto(
                i.Id, i.ItemType.ToString(), i.Description, i.Quantity, i.Unit, i.ThawEventId, i.CryovialId)).ToList()
        )).ToList();
    }

    private async Task<Dictionary<int, string>> NamesAsync(IEnumerable<int> userIds)
    {
        var ids = userIds.Distinct().ToList();
        return await _db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    private static List<string> UsedLots(ThawEvent t) =>
        t.UsedInChallenges.Select(c => c.MediaEvaluation?.Media?.LotNumber).OfType<string>().Distinct().OrderBy(l => l).ToList();
}
