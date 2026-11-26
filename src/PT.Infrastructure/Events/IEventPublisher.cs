using PT.Domain.Events;

namespace PT.Infrastructure.Events;

public interface IEventPublisher
{
    Task PublishAsync(DocumentEvent documentEvent, CancellationToken cancellationToken);
}
