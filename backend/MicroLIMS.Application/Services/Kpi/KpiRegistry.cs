namespace MicroLIMS.Application.Services.Kpi;

public class KpiRegistry
{
    public KpiRegistry(IEnumerable<IKpi> all)
    {
        var list = all.OrderBy(k => k.Order).ThenBy(k => k.Key, StringComparer.Ordinal).ToList();
        var duplicate = list.GroupBy(k => k.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"KPI key '{duplicate.Key}' is registered more than once.");
        All = list;
    }

    public IReadOnlyList<IKpi> All { get; }

    public IKpi? Find(string key) =>
        All.FirstOrDefault(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));
}
