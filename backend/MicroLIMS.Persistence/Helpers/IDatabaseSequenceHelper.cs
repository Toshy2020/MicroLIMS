namespace MicroLIMS.Persistence.Helpers;

public interface IDatabaseSequenceHelper
{
    Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken cancellationToken = default);
}
