using System.Threading.Channels;

namespace MyBudget.Features.SharedKernel.Email;

/// <summary>
/// Fire-and-forget email sender. Writes messages to a bounded channel; EmailBackgroundService reads them.
/// </summary>
public sealed class EmailChannel : IEmailSender
{
    private readonly Channel<EmailMessage> _channel;

    public EmailChannel()
    {
        _channel = Channel.CreateUnbounded<EmailMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
        => await _channel.Writer.WriteAsync(message, ct);

    public ChannelReader<EmailMessage> Reader => _channel.Reader;
}
