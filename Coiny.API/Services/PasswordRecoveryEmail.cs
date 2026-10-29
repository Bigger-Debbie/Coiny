using System.Net;
using System.Threading.Channels;
using System.Net.Mail;
using Coiny.API.Configuration;
using Microsoft.Extensions.Options;
namespace Coiny.API.Services;
public interface IPasswordRecoveryEmail
{
    bool IsConfigured { get; }
    Task SendAsync(string email, string token, CancellationToken cancellationToken);
}
public class PasswordRecoveryEmail(IOptions<RecoveryEmailOptions> options, IWebHostEnvironment environment, ILogger<PasswordRecoveryEmail> logger) : BackgroundService, IPasswordRecoveryEmail
{
    private readonly Channel<(string Email, string Token)> queue = Channel.CreateBounded<(string, string)>(new BoundedChannelOptions(100) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private RecoveryEmailOptions Settings => options.Value;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Settings.Host) && MailAddress.TryCreate(Settings.From, out _) &&
        Uri.TryCreate(Settings.AppUrl, UriKind.Absolute, out var uri) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        (uri.Scheme == "https" || environment.IsDevelopment() && uri.IsLoopback && uri.Scheme == "http") &&
        (Settings.EnableSsl || environment.IsDevelopment() &&
            (Settings.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(Settings.Host, out var address) && IPAddress.IsLoopback(address)));
    public Task SendAsync(string email, string token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsConfigured || !queue.Writer.TryWrite((email, token))) throw new InvalidOperationException("Password recovery email is unavailable.");
        return Task.CompletedTask;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken)) {
            try { await DeliverAsync(message.Email, message.Token, stoppingToken); }
            catch (Exception exception) when (exception is SmtpException or OperationCanceledException or InvalidOperationException or ArgumentException) {
                logger.LogWarning("Password recovery email delivery failed ({FailureType}). Check the mail service configuration.", exception.GetType().Name);
            }
        }
    }
    private async Task DeliverAsync(string email, string token, CancellationToken cancellationToken)
    {
        // Fragments aren't sent in HTTP requests or Referer headers. Never build links from the request Host.
        var link = Settings.AppUrl.TrimEnd('/') + "/reset-password#email=" + Uri.EscapeDataString(email) + "&token=" + Uri.EscapeDataString(token);
        using var message = new MailMessage(Settings.From, email) { Subject = "Reset your Coiny password",
            Body = "A password reset was requested for your Coiny account. This link expires in 30 minutes and can only be used once.\n\n" + link +
                "\n\nIf you didn't request this, ignore this email. Your password hasn't changed." };
        using var smtp = new SmtpClient(Settings.Host, Settings.Port) { EnableSsl = Settings.EnableSsl, UseDefaultCredentials = false };
        if (!string.IsNullOrEmpty(Settings.Username)) smtp.Credentials = new NetworkCredential(Settings.Username, Settings.Password);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await smtp.SendMailAsync(message, timeout.Token);
    }
}
