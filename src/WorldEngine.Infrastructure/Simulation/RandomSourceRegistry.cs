using System.Collections.Concurrent;
using WorldEngine.Domain.Simulation;

namespace WorldEngine.Infrastructure.Simulation;

public sealed class RandomSourceRegistry
{
    private readonly ConcurrentDictionary<Guid, IRandomSource> _sources = new();

    public IRandomSource GetOrCreate(Guid worldId, int seed)
    {
        return _sources.GetOrAdd(worldId, _ => new SeededRandomSource(seed));
    }

    public bool Remove(Guid worldId) => _sources.TryRemove(worldId, out _);
}