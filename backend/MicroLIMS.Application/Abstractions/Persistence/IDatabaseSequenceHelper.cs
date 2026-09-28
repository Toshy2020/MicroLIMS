namespace MicroLIMS.Application.Abstractions.Persistence;

public interface IDatabaseSequenceHelper
{
    Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken cancellationToken = default);
}
