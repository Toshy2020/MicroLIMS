# Storage Migration Implications

**Status:** assessment only. **The storage migration is deferred — no provider has been selected and no code has been changed.**

This document records whether `IFileStorageService` is a usable seam for replacing local
filesystem storage with durable object storage, and what a future migration must account for.
It exists because the seam looks trivially swappable and is not: one property of the current
implementation will silently invalidate every stored file reference if it is overlooked.

Companion finding: the GxP storage durability audit established that GxP files have **no backup
of any kind**, independent of whether the underlying disk is persistent. Migration does not by
itself solve that — see [Backup is a separate problem](#backup-is-a-separate-problem).

---

## 1. Verdict on the seam

**`IFileStorageService` is a clean seam.** This was verified against the repository rather than
assumed from the interface shape:

| Check | Result |
|---|---|
| Production code performing file I/O outside `LocalFileStorageService` | **None.** No `File.*`, `FileStream`, `Directory.*`, or `PhysicalFile()` anywhere else |
| Production references to the concrete `LocalFileStorageService` | **One** — the DI registration in `ServiceCollectionExtensions.cs` |
| Consumers injecting the interface | 14, all on the abstraction |
| Consumers parsing the returned string as a filesystem path (`Path.*`) | **None** |
| Consumers depending on files being local (streaming from disk, path arithmetic) | **None** |

Every one of the seven services that store files — `RecordArchiveService`,
`DocumentControl/DocumentFileService`, `OosInvestigationDocumentService`,
`MaterialDocumentService`, `EquipmentDocumentService`, `ItemDocumentService`,
`DiscussionService` — goes through the interface. Content type, file name, size and SHA-256 are
held in the database per entity, so the storage layer is not expected to carry metadata. Swapping
the implementation is genuinely a matter of writing one class and changing one registration line.

Layer placement (`MicroLIMS.Infrastructure.Storage`, consumed by Application) matches the
project's stated boundary in `CLAUDE.md`: *Domain → Application → Persistence / Infrastructure → API*.

---

## 2. The trap: stored paths are coupled to the storage location

`SaveAsync` returns `Path.Combine(_basePath, fileName)` — the **combined** path, not the relative
key it was given. Six of the seven services persist that returned value:

```csharp
var storageKey = $"documents/{revisionId}/{newFile.Id}_{role}{ext}";  // relative key
var savedPath  = await _storage.SaveAsync(storageKey, content);        // "storage/documents/…"
newFile.StorageKey = savedPath;                                        // ← the combined path is persisted
```

So the database stores `storage/documents/5/12_final.pdf`, not `documents/5/12_final.pdf`.

**Consequence: changing where files live invalidates every existing row.** This applies to a
Render persistent disk mounted at `/var/data` exactly as much as it applies to R2. Any migration
must therefore either preserve the current base path verbatim, or rewrite these columns:

| Entity | Column |
|---|---|
| `ArchivedRecord` | `StoragePath` |
| `RevisionFile` | `StorageKey` |
| `MaterialDocument` | `StorageKey` |
| `EquipmentDocument` | `StorageKey` |
| `ItemDocument` | `StorageKey` |
| `OosInvestigationDocument` | `StorageKey` |
| `DiscussionAttachment` | `StorageKey` |

A migration that copies bytes to a new provider but leaves these columns untouched produces a
system where every record still lists its evidence and none of it can be opened. Because
`ArchivedRecord` and `RevisionFile` hold GxP evidence, that failure is silent and regulatory.

### Related inconsistency to resolve at the same time

`DiscussionService` is the exception: it discards `SaveAsync`'s return value and persists the
**relative** key. Since `ReadAsync` does not prepend the base path, discussion attachments are
read from a different location than they were written, and only work when the base path is empty
(which is why unit tests pass). Non-GxP, but the two conventions must be reconciled before a
migration, or a bulk path rewrite will corrupt one of them.

**Recommended target convention:** persist the **relative key only**, and have the implementation
resolve it. That is what an object store needs anyway (a key, not a path), and it decouples the
database from the storage location permanently — so this is the last migration of these columns.

---

## 3. Interface changes a durable provider will require

The current surface is minimal:

```csharp
Task<string> SaveAsync(string fileName, byte[] content);
Task<byte[]> ReadAsync(string path);
```

| Gap | Why it matters | Needed for migration? |
|---|---|---|
| Returns a location rather than the key | Section 2 — the whole trap | **Yes** |
| No `CancellationToken` | An abandoned download holds a remote connection open | **Yes** — object stores are network calls |
| `byte[]` only, no streaming | Whole file buffered in memory on read *and* write. `DocumentFileService` permits 50 MB; a Render instance has 512 MB. Concurrent downloads are a real memory risk today and worse over a network | Strongly recommended |
| No `ExistsAsync` | Missing files are detected only when a user opens one, as an exception. A proactive integrity sweep is impossible to write | Recommended |
| No `DeleteAsync` | Nothing in production deletes files, so this is genuinely unused — **do not add it speculatively**; its absence is a GxP-friendly property worth keeping | No |

Retry and transient-fault handling become relevant once storage is a network call; the local
implementation never needed them.

---

## 4. Backup is a separate problem

Neither a Render persistent disk nor an object store is a backup.

```
Database backup          →  rows containing keys
File backup (none today) →  the bytes those keys point to
                            ─────────────────────────────
Combined recovery        →  currently impossible
```

Object storage improves *durability* (survives container replacement, replicated by the provider)
but does not protect against deletion, credential compromise, or lifecycle misconfiguration.
Whichever option is chosen, the migration is not complete until a **restore of database + files
together** has been demonstrated in a non-production environment. No such restore has ever been
performed.

---

## 5. Options, not yet decided

| Option | Strength | Cost |
|---|---|---|
| **Render persistent disk** | Smallest change; files stay a filesystem | Paid plan; binds the service to one instance (a disk cannot be shared), so it forecloses horizontal scaling; still needs the path migration and a backup |
| **Cloudflare R2** | Survives every container event; replicated; S3-compatible; Cloudflare is already in the stack; matches the interface's original intent | New implementation, credentials, streaming work; needs the path migration and a backup |
| **PostgreSQL bytes (`ArchivedRecord` only)** | Files inherit database backups automatically — closes the combined-recovery gap outright | Database growth; only suitable for the small immutable archive PDFs, not 50 MB documents |

A hybrid is worth considering: archive PDFs in PostgreSQL (small, immutable, most regulated) and
bulk documents in object storage.

---

## 6. Sequencing when the migration is scheduled

1. Confirm whether the current production disk is persistent, and whether existing files survive.
2. Decide the provider.
3. Change the persisted value to a **relative key** and reconcile `DiscussionService`.
4. Data-migrate the seven columns.
5. Add `CancellationToken` and streaming to the interface.
6. Implement the provider behind the existing seam; change the one DI registration.
7. Copy existing bytes, verifying each against its recorded SHA-256 where one exists
   (`ArchivedRecord`, `RevisionFile`, `DiscussionAttachment` all carry one).
8. Demonstrate a combined database + file restore in a non-production environment.
9. Only then retire the local implementation.

Steps 3, 4 and 8 are the ones that make this a project rather than a class swap.
