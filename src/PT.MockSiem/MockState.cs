using System.Collections.Concurrent;
using System.Text.Json;

namespace PT.MockSiem;

public sealed class MockState
{
    private readonly ConcurrentDictionary<Guid, JsonElement> _events = new();
    private readonly ConcurrentQueue<Guid> _eventOrder = new();
    private readonly ConcurrentDictionary<Guid, JsonElement> _documents = new();
    private readonly ConcurrentQueue<Guid> _documentOrder = new();
    private int _eventFailuresRemaining;
    private int _importFailuresRemaining;

    public MockState(int failFirstNRequests, int failFirstNImportRequests)
    {
        _eventFailuresRemaining = failFirstNRequests;
        _importFailuresRemaining = failFirstNImportRequests;
    }

    public int EventCount => _events.Count;

    public IReadOnlyList<JsonElement> Events =>
        _eventOrder.Select(eventId => _events[eventId]).ToArray();

    public int DocumentCount => _documents.Count;

    public IReadOnlyList<JsonElement> Documents =>
        _documentOrder.Select(documentId => _documents[documentId]).ToArray();

    public bool ShouldFailEvent() =>
        Interlocked.Decrement(ref _eventFailuresRemaining) >= 0;

    public bool ShouldFailImport() =>
        Interlocked.Decrement(ref _importFailuresRemaining) >= 0;

    public bool TryAddEvent(Guid eventId, JsonElement payload)
    {
        if (!_events.TryAdd(eventId, payload))
        {
            return false;
        }

        _eventOrder.Enqueue(eventId);
        return true;
    }

    public (int Received, int Added) Import(IEnumerable<JsonElement> documents)
    {
        var received = 0;
        var added = 0;

        foreach (var document in documents)
        {
            received++;

            if (!document.TryGetProperty("id", out var idProperty)
                || !Guid.TryParse(idProperty.GetString(), out var documentId))
            {
                continue;
            }

            var payload = document.Clone();

            if (_documents.TryAdd(documentId, payload))
            {
                _documentOrder.Enqueue(documentId);
                added++;
            }
            else
            {
                _documents[documentId] = payload;
            }
        }

        return (received, added);
    }

    public void ClearEvents()
    {
        _events.Clear();
        _eventOrder.Clear();
    }

    public void ClearDocuments()
    {
        _documents.Clear();
        _documentOrder.Clear();
    }
}
