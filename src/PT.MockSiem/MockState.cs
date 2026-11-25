using System.Collections.Concurrent;
using System.Text.Json;

namespace PT.MockSiem;

public sealed class MockState
{
    private readonly ConcurrentDictionary<Guid, JsonElement> _events = new();
    private readonly ConcurrentQueue<Guid> _order = new();
    private int _failuresRemaining;

    public MockState(int failFirstNRequests)
    {
        _failuresRemaining = failFirstNRequests;
    }

    public int Count => _events.Count;

    public IReadOnlyList<JsonElement> Events =>
        _order.Select(eventId => _events[eventId]).ToArray();

    public bool ShouldFail() =>
        Interlocked.Decrement(ref _failuresRemaining) >= 0;

    public bool TryAdd(Guid eventId, JsonElement payload)
    {
        if (!_events.TryAdd(eventId, payload))
        {
            return false;
        }

        _order.Enqueue(eventId);
        return true;
    }

    public void Clear()
    {
        _events.Clear();
        _order.Clear();
    }
}
