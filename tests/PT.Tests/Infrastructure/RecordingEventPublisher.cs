using PT.Domain.Events;
using PT.Infrastructure.Events;

namespace PT.Tests.Integration;

public sealed class RecordingEventPublisher : IEventPublisher
{
    private readonly List<DocumentEvent> _events = [];
    private readonly object _sync = new();
    private int _failuresRemaining;

    public int FailuresRemaining
    {
        get
        {
            lock (_sync)
            {
                return _failuresRemaining;
            }
        }

        set
        {
            lock (_sync)
            {
                _failuresRemaining = value;
            }
        }
    }

    public IReadOnlyList<DocumentEvent> Events
    {
        get
        {
            lock (_sync)
            {
                return _events.ToArray();
            }
        }
    }

    public Task PublishAsync(DocumentEvent documentEvent, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_failuresRemaining > 0)
            {
                _failuresRemaining--;
                throw new HttpRequestException("Simulated publisher failure.");
            }

            _events.Add(documentEvent);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _events.Clear();
            _failuresRemaining = 0;
        }
    }
}
