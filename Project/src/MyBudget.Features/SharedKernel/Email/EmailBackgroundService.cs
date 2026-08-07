using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace MyBudget.Features.SharedKernel.Email;

public sealed class EmailBackgroundService : BackgroundService
{
    private readonly EmailChannel _emailChannel;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailBackgroundService> _logger;

    public EmailBackgroundService(
        EmailChannel emailChannel,
        IConfiguration configuration,
        ILogger<EmailBackgroundService> logger)
    {
        _emailChannel = emailChannel;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _emailChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await SendEmailAsync(message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To} with subject {Subject}", message.To, message.Subject);
            }
        }
    }

    private async Task SendEmailAsync(EmailMessage message, CancellationToken ct)
    {
        var host = _configuration["Email:SmtpHost"] ?? "localhost";
        var port = int.Parse(_configuration["Email:SmtpPort"] ?? "1025");
        var fromName = _configuration["Email:FromName"] ?? "MyBudget";
        var fromAddress = _configuration["Email:FromAddress"] ?? "noreply@mybudget.local";
        var username = _configuration["Email:SmtpUsername"];
        var password = _configuration["Email:SmtpPassword"];
        var useStartTls = bool.Parse(_configuration["Email:SmtpUseStartTls"] ?? "false");

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(fromName, fromAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new TextPart("html") { Text = message.Body };

        // Mailpit (dev): no TLS, no auth — defaults preserve that.
        // Real SMTP relays (e.g. Brevo): Email:SmtpUseStartTls=true + Email:SmtpUsername/SmtpPassword set.
        var secureSocketOptions = useStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, secureSocketOptions, ct);

        if (!string.IsNullOrEmpty(username))
            await client.AuthenticateAsync(username, password ?? string.Empty, ct);

        await client.SendAsync(mimeMessage, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("Email sent to {To} with subject {Subject}", message.To, message.Subject);
    }
}
