using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record AuditTraceabilityNode(
    string NodeType,
    string Identifier,
    string Title,
    string? Status,
    string? Description,
    int? EntityId,
    string? NavigationTarget,
    DateTime? Timestamp);

public record AuditTraceabilityResult(
    string PrimaryCategory,
    string RootIdentifier,
    List<AuditTraceabilityNode> Nodes);

public class AuditTraceabilityService
{
    private readonly MicroLimsDbContext _db;

    public AuditTraceabilityService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<AuditTraceabilityResult?> GetTraceabilityAsync(int auditLogId)
    {
        var log = await _db.AuditLogs.FindAsync(auditLogId);
        if (log == null) return null;

        return await BuildTraceabilityFromLogAsync(log);
    }

    public async Task<AuditTraceabilityResult?> GetTraceabilityForEntityAsync(string entityName, string entityId)
    {
        var log = await _db.AuditLogs
            .Where(a => a.EntityName == entityName && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();

        if (log != null)
        {
            return await BuildTraceabilityFromLogAsync(log);
        }

        return await BuildTraceabilityDirectAsync(entityName, entityId);
    }

    private async Task<AuditTraceabilityResult?> BuildTraceabilityFromLogAsync(AuditLog log)
    {
        // 1. Sample / Testing Chain
        if (log.SampleId.HasValue ||
            log.EntityName == nameof(Sample) ||
            log.EntityName == nameof(TestOrder) ||
            log.EntityName == nameof(Result) ||
            log.EntityName == nameof(WorkflowStepResult) ||
            log.EntityName == nameof(ResultRecord) ||
            (log.EntityName == nameof(ReviewWorkflowEvent) && log.SampleReferenceNumber != null) ||
            (log.EntityName == nameof(ElectronicSignature) && log.SampleReferenceNumber != null) ||
            !string.IsNullOrWhiteSpace(log.SampleReferenceNumber))
        {
            var sampleId = log.SampleId;
            if (!sampleId.HasValue && log.EntityName == nameof(Sample) && int.TryParse(log.EntityId, out var parsedSid))
            {
                sampleId = parsedSid;
            }
            else if (!sampleId.HasValue && log.EntityName == nameof(TestOrder) && int.TryParse(log.EntityId, out var parsedTid))
            {
                sampleId = await _db.TestOrders.Where(t => t.Id == parsedTid).Select(t => (int?)t.SampleId).FirstOrDefaultAsync();
            }
            else if (!sampleId.HasValue && !string.IsNullOrWhiteSpace(log.SampleReferenceNumber))
            {
                sampleId = await _db.Samples.Where(s => s.ReferenceNumber == log.SampleReferenceNumber).Select(s => (int?)s.Id).FirstOrDefaultAsync();
            }

            if (sampleId.HasValue)
            {
                return await BuildSampleChainAsync(sampleId.Value);
            }
        }

        // 2. Media Chain
        if (log.EntityName == nameof(Media) ||
            log.EntityName == nameof(MediaEvaluation) ||
            log.EntityName == nameof(MediaConfiguration) ||
            log.EntityName == nameof(MediaConfigurationChallenge) ||
            log.EntityName == "MediaChallengeSpec" ||
            !string.IsNullOrWhiteSpace(log.MediaLotNumber))
        {
            int? mediaId = null;
            if (log.EntityName == nameof(Media) && int.TryParse(log.EntityId, out var parsedMid))
            {
                mediaId = parsedMid;
            }
            else if (!string.IsNullOrWhiteSpace(log.MediaLotNumber))
            {
                mediaId = await _db.Media.Where(m => m.LotNumber == log.MediaLotNumber).Select(m => (int?)m.Id).FirstOrDefaultAsync();
            }

            if (mediaId.HasValue)
            {
                return await BuildMediaChainAsync(mediaId.Value);
            }
        }

        // 3. Material Lot Chain
        if (log.EntityName == nameof(Material) ||
            log.EntityName == nameof(MaterialDocument) ||
            !string.IsNullOrWhiteSpace(log.BatchNumber))
        {
            int? materialId = null;
            if (log.EntityName == nameof(Material) && int.TryParse(log.EntityId, out var parsedMatId))
            {
                materialId = parsedMatId;
            }
            else if (log.EntityName == nameof(MaterialDocument) && int.TryParse(log.EntityId, out var parsedDocId))
            {
                materialId = await _db.MaterialDocuments.Where(d => d.Id == parsedDocId).Select(d => (int?)d.MaterialId).FirstOrDefaultAsync();
            }
            else if (!string.IsNullOrWhiteSpace(log.BatchNumber))
            {
                materialId = await _db.Materials.Where(m => m.BatchNumber == log.BatchNumber).Select(m => (int?)m.Id).FirstOrDefaultAsync();
            }

            if (materialId.HasValue)
            {
                return await BuildMaterialChainAsync(materialId.Value);
            }
        }

        // 4. Cryovial / Culture Chain
        if (log.EntityName == nameof(Cryovial) ||
            log.EntityName == nameof(Organism) ||
            !string.IsNullOrWhiteSpace(log.CryovialCode) ||
            !string.IsNullOrWhiteSpace(log.ReferenceStrainCode))
        {
            int? cryovialId = null;
            if (log.EntityName == nameof(Cryovial) && int.TryParse(log.EntityId, out var parsedCryoId))
            {
                cryovialId = parsedCryoId;
            }
            else if (!string.IsNullOrWhiteSpace(log.CryovialCode))
            {
                cryovialId = await _db.Cryovials.Where(c => c.Code == log.CryovialCode).Select(c => (int?)c.Id).FirstOrDefaultAsync();
            }

            if (cryovialId.HasValue)
            {
                return await BuildCryovialChainAsync(cryovialId.Value);
            }
        }

        // 5. Equipment Chain
        if (log.EntityName == nameof(EquipmentInventory) ||
            log.EntityName == nameof(EquipmentDocument) ||
            log.EntityName == nameof(EquipmentStatusHistory))
        {
            int? equipId = null;
            if (log.EntityName == nameof(EquipmentInventory) && int.TryParse(log.EntityId, out var parsedEqId))
            {
                equipId = parsedEqId;
            }
            else if (log.EntityName == nameof(EquipmentDocument) && int.TryParse(log.EntityId, out var parsedEqDocId))
            {
                equipId = await _db.EquipmentDocuments.Where(d => d.Id == parsedEqDocId).Select(d => (int?)d.EquipmentInventoryId).FirstOrDefaultAsync();
            }
            else if (log.EntityName == nameof(EquipmentStatusHistory) && int.TryParse(log.EntityId, out var parsedEqHistId))
            {
                equipId = await _db.EquipmentStatusHistories.Where(h => h.Id == parsedEqHistId).Select(h => (int?)h.EquipmentInventoryId).FirstOrDefaultAsync();
            }

            if (equipId.HasValue)
            {
                return await BuildEquipmentChainAsync(equipId.Value);
            }
        }

        // 6. Item Master Chain (preparation configuration hangs off the Item,
        // not off any one Sample - it outlives every sample that used it).
        if (log.EntityName == nameof(Item) ||
            log.EntityName == nameof(ItemPreparationConfiguration))
        {
            int? itemId = null;
            if (log.EntityName == nameof(Item) && int.TryParse(log.EntityId, out var parsedItemId))
            {
                itemId = parsedItemId;
            }
            else if (log.EntityName == nameof(ItemPreparationConfiguration) && int.TryParse(log.EntityId, out var parsedCfgId))
            {
                itemId = await _db.ItemPreparationConfigurations.Where(c => c.Id == parsedCfgId).Select(c => (int?)c.ItemId).FirstOrDefaultAsync();
            }

            if (itemId.HasValue)
            {
                return await BuildItemChainAsync(itemId.Value);
            }
        }

        return new AuditTraceabilityResult("General", $"{log.EntityName} #{log.EntityId}", new List<AuditTraceabilityNode>
        {
            new(log.EntityName, log.EntityId, log.EntityName, log.Action, $"Entity {log.EntityName} with ID {log.EntityId}", int.TryParse(log.EntityId, out var id) ? id : null, null, log.Timestamp)
        });
    }

    private async Task<AuditTraceabilityResult?> BuildTraceabilityDirectAsync(string entityName, string entityId)
    {
        if (!int.TryParse(entityId, out var id)) return null;

        if (entityName == nameof(Sample)) return await BuildSampleChainAsync(id);
        if (entityName == nameof(Media)) return await BuildMediaChainAsync(id);
        if (entityName == nameof(Material)) return await BuildMaterialChainAsync(id);
        if (entityName == nameof(Cryovial)) return await BuildCryovialChainAsync(id);
        if (entityName == nameof(EquipmentInventory)) return await BuildEquipmentChainAsync(id);
        if (entityName == nameof(Item)) return await BuildItemChainAsync(id);

        return null;
    }

    // ---- Domain Relationship Chain Builders ----

    private async Task<AuditTraceabilityResult> BuildItemChainAsync(int itemId)
    {
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item == null)
        {
            return new AuditTraceabilityResult("ItemMaster", $"Item #{itemId}", new List<AuditTraceabilityNode>());
        }

        var nodes = new List<AuditTraceabilityNode>
        {
            new("Item", item.Code, item.Name, item.Category.ToString(), $"Product/Item: {item.Name} ({item.Code})", item.Id, null, null)
        };

        var config = await _db.ItemPreparationConfigurations
            .Include(c => c.Neutralizer)
            .FirstOrDefaultAsync(c => c.ItemId == itemId);

        if (config != null)
        {
            nodes.Add(new(
                "PreparationConfiguration",
                $"PREP-CFG-{config.Id}",
                "Preparation Configuration",
                config.ApprovalStatus.ToString(),
                $"{config.Amount} {config.Unit}, {config.Technique}, Neutralizer: {config.Neutralizer?.Name ?? "—"}",
                config.Id,
                null,
                config.CreatedAt));
        }

        var samples = await _db.Samples
            .Where(s => s.ItemId == itemId)
            .OrderByDescending(s => s.ReceivedAt)
            .Take(20)
            .Select(s => new { s.Id, s.ReferenceNumber, s.Status, s.ReceivedAt, s.ControlNumber })
            .ToListAsync();

        foreach (var s in samples)
        {
            nodes.Add(new(
                "Sample",
                s.ReferenceNumber,
                $"Sample (Ctrl: {s.ControlNumber})",
                s.Status.ToString(),
                $"Received: {s.ReceivedAt:dd-MMM-yyyy HH:mm}",
                s.Id,
                "samples",
                s.ReceivedAt));
        }

        return new AuditTraceabilityResult("ItemMaster", item.Code, nodes);
    }

    private async Task<AuditTraceabilityResult> BuildSampleChainAsync(int sampleId)
    {
        var sample = await _db.Samples
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.WaterDepartment)
            .Include(s => s.TestOrders)
                .ThenInclude(t => t.Results)
            .FirstOrDefaultAsync(s => s.Id == sampleId);

        if (sample == null)
        {
            return new AuditTraceabilityResult("SampleTesting", $"Sample #{sampleId}", new List<AuditTraceabilityNode>());
        }

        var nodes = new List<AuditTraceabilityNode>();

        // 1. Source / Item Node
        if (sample.Item != null)
        {
            nodes.Add(new(
                "Item",
                sample.Item.Code,
                sample.Item.Name,
                sample.Item.Category.ToString(),
                $"Product/Item: {sample.Item.Name} ({sample.Item.Code})",
                sample.Item.Id,
                null,
                null));
        }
        else if (sample.WaterSamplingPoint != null)
        {
            nodes.Add(new(
                "SamplingPoint",
                sample.WaterSamplingPoint.Code,
                sample.WaterSamplingPoint.Location,
                sample.WaterDepartment?.Name ?? "Water Point",
                $"Location: {sample.WaterSamplingPoint.Location}",
                sample.WaterSamplingPoint.Id,
                null,
                null));
        }
        else if (sample.Department != null)
        {
            nodes.Add(new(
                "Department",
                sample.Department.Name,
                sample.Department.Name,
                "Environmental Monitoring",
                $"EM Area: {sample.Department.Name}",
                sample.Department.Id,
                null,
                null));
        }

        // 2. Sample Node
        nodes.Add(new(
            "Sample",
            sample.ReferenceNumber,
            $"{sample.Category} Sample (Ctrl: {sample.ControlNumber})",
            sample.Status.ToString(),
            $"Batch: {sample.BatchNumber ?? "—"}, Received: {sample.ReceivedAt:dd-MMM-yyyy HH:mm}",
            sample.Id,
            "samples",
            sample.ReceivedAt));

        // 2b. Preparation snapshot - the values actually used, plus a link
        // back to the config version they were confirmed from.
        var prep = await _db.SamplePreparations
            .Include(p => p.Neutralizer)
            .FirstOrDefaultAsync(p => p.SampleId == sampleId);

        if (prep != null)
        {
            var provenance = prep.SourceConfigurationId.HasValue
                ? $"Confirmed from Preparation Configuration PREP-CFG-{prep.SourceConfigurationId}"
                : "Manually entered (no configuration on file)";

            nodes.Add(new(
                "SamplePreparation",
                $"PREP-{prep.Id}",
                "Sample Preparation",
                prep.WasConfirmedFromConfig ? "Confirmed from Configuration" : "Manual Entry",
                $"{prep.Amount} {prep.Unit}, {prep.Technique}, Neutralizer: {prep.Neutralizer?.Name ?? "—"}. {provenance}",
                prep.Id,
                null,
                prep.PreparedAt));
        }

        // 3. Test Orders
        foreach (var to in sample.TestOrders)
        {
            nodes.Add(new(
                "TestOrder",
                $"TO-{to.Id:D4}",
                $"Test Order: {to.TestCode}",
                to.Status.ToString(),
                $"Current step: {to.CurrentStep}",
                to.Id,
                "testing",
                null));

            // 4. Results
            foreach (var res in to.Results)
            {
                nodes.Add(new(
                    "Result",
                    $"RES-{res.Id}",
                    $"Result for {to.TestCode}",
                    res.Type.ToString(),
                    $"Value: {res.InterpretedValue ?? res.RawValue}",
                    res.Id,
                    "testing",
                    res.EnteredAt));
            }
        }

        // 5. Reviews
        var reviews = await _db.ReviewWorkflowEvents
            .Where(r => r.EntityType == "Sample" && r.EntityId == sample.Id)
            .OrderBy(r => r.Timestamp)
            .ToListAsync();

        foreach (var rev in reviews)
        {
            nodes.Add(new(
                "Review",
                rev.EventType.ToString(),
                $"Review Event by {rev.PerformedByNameSnapshot}",
                rev.Decision?.ToString() ?? rev.EventType.ToString(),
                rev.Comment ?? "Lifecycle decision recorded",
                rev.Id,
                null,
                rev.Timestamp));
        }

        // 6. Signatures
        var signatures = await _db.ElectronicSignatures
            .Where(s => s.EntityType == "Sample" && s.EntityId == sample.Id)
            .OrderBy(s => s.SignedAt)
            .ToListAsync();

        foreach (var sig in signatures)
        {
            nodes.Add(new(
                "ElectronicSignature",
                $"SIG-{sig.Id}",
                $"{sig.MeaningOfSignature} by {sig.UserFullNameSnapshot}",
                "Signed",
                $"Reason: {sig.MeaningOfSignature}, Role: {sig.RoleSnapshot}",
                sig.Id,
                null,
                sig.SignedAt));
        }

        return new AuditTraceabilityResult("SampleTesting", sample.ReferenceNumber, nodes);
    }

    private async Task<AuditTraceabilityResult> BuildMediaChainAsync(int mediaId)
    {
        var media = await _db.Media
            .Include(m => m.Material)
            .Include(m => m.AutoclaveEquipment)
            .FirstOrDefaultAsync(m => m.Id == mediaId);

        if (media == null)
        {
            return new AuditTraceabilityResult("MediaPreparation", $"Media #{mediaId}", new List<AuditTraceabilityNode>());
        }

        var nodes = new List<AuditTraceabilityNode>();

        if (media.Material != null)
        {
            nodes.Add(new(
                "Material",
                media.Material.BatchNumber,
                media.Material.MaterialName,
                media.Material.Status.ToString(),
                $"Source powder/material: {media.Material.MaterialName}",
                media.Material.Id,
                "materials",
                media.Material.ReceivingDate));
        }

        nodes.Add(new(
            "Media",
            media.LotNumber,
            $"{media.Material?.MaterialName ?? "Media Lot"} (Lot: {media.LotNumber})",
            media.Status.ToString(),
            $"Prep: {media.PreparedAt:dd-MMM-yyyy}, Exp: {media.ExpiryDate:dd-MMM-yyyy}, Autoclave: {media.AutoclaveEquipment?.Code ?? "—"}",
            media.Id,
            "media",
            media.PreparedAt));

        var evaluations = await _db.MediaEvaluations
            .Where(e => e.MediaId == media.Id)
            .OrderBy(e => e.AssignedAt)
            .ToListAsync();

        foreach (var eval in evaluations)
        {
            nodes.Add(new(
                "MediaEvaluation",
                $"EVAL-{eval.Id}",
                $"GPT Evaluation ({eval.EvaluationType}) for {media.LotNumber}",
                eval.Outcome?.ToString() ?? eval.Status.ToString(),
                $"Status: {eval.Status}, Assigned on {eval.AssignedAt:dd-MMM-yyyy}",
                eval.Id,
                "media",
                eval.CompletedAt ?? eval.AssignedAt));
        }

        return new AuditTraceabilityResult("MediaPreparation", media.LotNumber, nodes);
    }

    private async Task<AuditTraceabilityResult> BuildMaterialChainAsync(int materialId)
    {
        var material = await _db.Materials
            .FirstOrDefaultAsync(m => m.Id == materialId);

        if (material == null)
        {
            return new AuditTraceabilityResult("MaterialLot", $"Material #{materialId}", new List<AuditTraceabilityNode>());
        }

        var nodes = new List<AuditTraceabilityNode>
        {
            new(
                "Material",
                material.BatchNumber,
                $"{material.MaterialName} (Lot: {material.BatchNumber})",
                material.Status.ToString(),
                $"Type: {material.MaterialType}, Qty remaining: {material.QuantityRemaining} {material.Unit}",
                material.Id,
                "materials",
                material.ReceivingDate)
        };

        var docs = await _db.MaterialDocuments
            .Where(d => d.MaterialId == material.Id)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        foreach (var doc in docs)
        {
            nodes.Add(new(
                "MaterialDocument",
                doc.OriginalFileName,
                $"{doc.DocumentType}: {doc.OriginalFileName}",
                doc.Status.ToString(),
                $"Uploaded by user #{doc.UploadedByUserId} on {doc.UploadedAt:dd-MMM-yyyy}",
                doc.Id,
                "materials",
                doc.UploadedAt));
        }

        return new AuditTraceabilityResult("MaterialLot", material.BatchNumber, nodes);
    }

    private async Task<AuditTraceabilityResult> BuildCryovialChainAsync(int cryovialId)
    {
        var cryo = await _db.Cryovials
            .Include(c => c.Organism)
            .FirstOrDefaultAsync(c => c.Id == cryovialId);

        if (cryo == null)
        {
            return new AuditTraceabilityResult("CryovialCulture", $"Cryovial #{cryovialId}", new List<AuditTraceabilityNode>());
        }

        var nodes = new List<AuditTraceabilityNode>();

        if (cryo.Organism != null)
        {
            nodes.Add(new(
                "Organism",
                cryo.Organism.AtccNumber ?? cryo.Organism.ScientificName,
                cryo.Organism.ScientificName,
                "Master Strain",
                $"ATCC: {cryo.Organism.AtccNumber ?? "—"}, Common: {cryo.Organism.CommonName ?? "—"}",
                cryo.Organism.Id,
                "cryovials",
                null));
        }

        nodes.Add(new(
            "Cryovial",
            cryo.Code,
            $"Cryovial {cryo.Code}",
            cryo.ApprovalStatus.ToString(),
            $"Mfg: {cryo.ManufacturerName}, Vials remaining: {cryo.VialsRemaining}/{cryo.NumberOfVialsPrepared}",
            cryo.Id,
            "cryovials",
            cryo.PreparedAt));

        return new AuditTraceabilityResult("CryovialCulture", cryo.Code, nodes);
    }

    private async Task<AuditTraceabilityResult> BuildEquipmentChainAsync(int equipmentId)
    {
        var equip = await _db.EquipmentInventories
            .FirstOrDefaultAsync(e => e.Id == equipmentId);

        if (equip == null)
        {
            return new AuditTraceabilityResult("EquipmentRegister", $"Equipment #{equipmentId}", new List<AuditTraceabilityNode>());
        }

        var nodes = new List<AuditTraceabilityNode>
        {
            new(
                "Equipment",
                equip.Code,
                $"{equip.InstrumentType} - {equip.ManufacturerName}",
                equip.Status.ToString(),
                $"SN: {equip.SerialNumber ?? "—"}, Location: {equip.Location}, Cal Due: {equip.CalibrationDueDate:dd-MMM-yyyy}",
                equip.Id,
                "equipment",
                equip.CreatedAt)
        };

        var docs = await _db.EquipmentDocuments
            .Where(d => d.EquipmentInventoryId == equip.Id)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        foreach (var doc in docs)
        {
            nodes.Add(new(
                "EquipmentDocument",
                doc.OriginalFileName,
                $"{doc.DocumentType}: {doc.OriginalFileName}",
                doc.Status.ToString(),
                $"Uploaded by user #{doc.UploadedByUserId} on {doc.UploadedAt:dd-MMM-yyyy}",
                doc.Id,
                "equipment",
                doc.UploadedAt));
        }

        var histories = await _db.EquipmentStatusHistories
            .Where(h => h.EquipmentInventoryId == equip.Id)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();

        foreach (var hist in histories)
        {
            nodes.Add(new(
                "EquipmentStatusHistory",
                $"{hist.PreviousStatus} → {hist.NewStatus}",
                $"Status Transition: {hist.NewStatus}",
                hist.NewStatus.ToString(),
                $"Reason: {hist.Comment} (on {hist.ChangedAt:dd-MMM-yyyy HH:mm})",
                hist.Id,
                "equipment",
                hist.ChangedAt));
        }

        return new AuditTraceabilityResult("EquipmentRegister", equip.Code, nodes);
    }
}
