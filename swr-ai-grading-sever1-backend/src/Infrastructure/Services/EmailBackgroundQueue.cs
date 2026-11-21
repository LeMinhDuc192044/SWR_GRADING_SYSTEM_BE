using System.Threading.Channels;
using Application.Common.Interfaces;

namespace Infrastructure.Services;

public sealed class EmailBackgroundQueue : IEmailBackgroundQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>(new UnboundedChannelOptions
    {
        SingleReader = true
    });

    public void QueueEmail(EmailMessage message)
    {
        _channel.Writer.TryWrite(message);
    }

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}
