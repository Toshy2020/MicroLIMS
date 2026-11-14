using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services;

/// <summary>
/// Generic all-or-nothing batch creation helper for master-data screens:
/// validates all rows up front (including duplicate and existing codes),
/// then executes the existing single-create service method per row inside one transaction.
/// </summary>
public class MasterDataBatchService
{
    public const int MaxBatchRows = 200;

    private readonly IMicroLimsDbContext _db;

    public MasterDataBatchService(IMicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TResponse>> ExecuteAsync<TRequest, TResponse>(
        IReadOnlyList<TRequest>? rows,
        Func<TRequest, int, CancellationToken, Task<IReadOnlyList<FieldError>>> validateRowAsync,
        Func<TRequest, Task<TResponse>> createSingleAsync,
        Func<TRequest, string?>? getCode = null,
        string codeField = "code",
        Func<IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlySet<string>>>? getExistingCodesAsync = null,
        Func<IReadOnlyList<TRequest>, CancellationToken, Task<IReadOnlyList<FieldError>>>? validateBatchAsync = null,
        CancellationToken ct = default)
    {
        if (rows is null || rows.Count == 0)
        {
            throw new FieldValidationException(
                "At least one row is required.",
                new[] { new FieldError(string.Empty, "At least one row is required.") });
        }

        if (rows.Count > MaxBatchRows)
        {
            throw new FieldValidationException(
                $"A batch may contain at most {MaxBatchRows} rows.",
                new[] { new FieldError(string.Empty, $"A batch may contain at most {MaxBatchRows} rows.") });
        }

        var errors = new List<FieldError>();
        var codesByNormalized = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var row = rows[i];
            if (row is null)
            {
                errors.Add(new FieldError(string.Empty, "Row is required.", i));
                continue;
            }

            var rowErrors = await validateRowAsync(row, i, ct);
            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
            }

            if (getCode is not null)
            {
                var rawCode = getCode(row);
                if (!string.IsNullOrWhiteSpace(rawCode))
                {
                    var normalized = rawCode.Trim();
                    if (!codesByNormalized.TryGetValue(normalized, out var indices))
                    {
                        indices = new List<int>();
                        codesByNormalized[normalized] = indices;
                    }
                    indices.Add(i);
                }
            }
        }

        foreach (var (code, indices) in codesByNormalized)
        {
            if (indices.Count <= 1) continue;
            foreach (var rowIndex in indices)
            {
                errors.Add(new FieldError(codeField, $"Duplicate {codeField} '{code}' in batch.", rowIndex));
            }
        }

        if (getCode is not null && getExistingCodesAsync is not null && codesByNormalized.Count > 0)
        {
            var existingCodes = await getExistingCodesAsync(codesByNormalized.Keys.ToList(), ct);
            foreach (var (code, indices) in codesByNormalized)
            {
                if (!existingCodes.Contains(code)) continue;
                foreach (var rowIndex in indices)
                {
                    errors.Add(new FieldError(codeField, $"Code '{code}' already exists.", rowIndex));
                }
            }
        }

        if (validateBatchAsync is not null)
        {
            var batchErrors = await validateBatchAsync(rows, ct);
            if (batchErrors.Count > 0)
            {
                errors.AddRange(batchErrors);
            }
        }

        if (errors.Count > 0)
        {
            var ordered = errors.OrderBy(e => e.Row ?? -1).ToList();
            throw new FieldValidationException("Some rows need correction.", ordered);
        }

        return await UnitOfWork.RunAsync(_db, async () =>
        {
            var results = new List<TResponse>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    results.Add(await createSingleAsync(rows[i]));
                }
                catch (FieldValidationException ex)
                {
                    var rowErrors = ex.FieldErrors.Count > 0
                        ? ex.FieldErrors.Select(e => e with { Row = e.Row ?? i }).ToList()
                        : new List<FieldError> { new(string.Empty, ex.Message, i) };
                    throw new FieldValidationException(ex.Message, rowErrors);
                }
                catch (BusinessRuleException ex)
                {
                    throw new FieldValidationException(ex.Message, new[] { new FieldError(string.Empty, ex.Message, i) });
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new FieldValidationException(ex.Message, new[] { new FieldError(string.Empty, ex.Message, i) });
                }
                catch (Exception ex) when (ex is NotFoundException or KeyNotFoundException)
                {
                    throw new FieldValidationException(ex.Message, new[] { new FieldError(string.Empty, ex.Message, i) });
                }
            }

            return (IReadOnlyList<TResponse>)results;
        });
    }
}
