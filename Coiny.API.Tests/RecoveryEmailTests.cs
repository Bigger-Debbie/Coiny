using System.Net;
using System.Net.Sockets;
using System.Text;
using Coiny.API.Configuration;
using Coiny.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Coiny.API.Tests;
public class RecoveryEmailTests
{
    [Theory]
    [InlineData("Production", "https://coiny.example", true, true)]
    [InlineData("Production", "http://coiny.example", true, false)]
    [InlineData("Production", "https://coiny.example", false, false)]
    [InlineData("Development", "http://localhost:4200", false, true)]
    [InlineData("Development", "http://external.example", false, false)]
    [InlineData("Production", "https://coiny.example?next=evil", true, false)]
    public void RecoveryRequiresTrustedHttpsLinkAndEncryptedMailOutsideLocalDevelopment(string environment, string appUrl, bool ssl, bool expected)
    {
        using var mail = new PasswordRecoveryEmail(Options.Create(new RecoveryEmailOptions { AppUrl = appUrl, Host = "localhost", From = "coiny@example.test", EnableSsl = ssl }),
            new Environment { EnvironmentName = environment }, NullLogger<PasswordRecoveryEmail>.Instance);
        Assert.Equal(expected, mail.IsConfigured);
    }
    [Fact]
    public async Task LocalSmtpReceivesConfiguredLinkWithEncodedFragmentAndNoRequestHost()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var receipt = Receive(listener);
        using var mail = new PasswordRecoveryEmail(Options.Create(new RecoveryEmailOptions {
            AppUrl = "http://localhost:4200", Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port,
            From = "coiny@example.test", EnableSsl = false }), new Environment { EnvironmentName = "Development" }, NullLogger<PasswordRecoveryEmail>.Instance);
        await mail.StartAsync(CancellationToken.None);
        await mail.SendAsync("alex@example.test", "secret+/=", CancellationToken.None);
        var message = (await receipt.WaitAsync(TimeSpan.FromSeconds(10))).Replace("=\r\n", "").Replace("=\n", "").Replace("=3D", "=");
        Assert.Contains("http://localhost:4200/reset-password#email=alex%40example.test&token=secret%2B%2F%3D", message);
        Assert.Contains("30 minutes", message); Assert.DoesNotContain("secret+/=", message);
        await mail.StopAsync(CancellationToken.None);
    }
    private static async Task<string> Receive(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync(); using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII); using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        await writer.WriteLineAsync("220 localhost test SMTP"); var body = new StringBuilder();
        while (await reader.ReadLineAsync() is { } line) {
            if (line.StartsWith("DATA")) {
                await writer.WriteLineAsync("354 End with a dot");
                while (await reader.ReadLineAsync() is { } data && data != ".") body.AppendLine(data);
                await writer.WriteLineAsync("250 queued");
            } else if (line.StartsWith("QUIT")) { await writer.WriteLineAsync("221 goodbye"); break; }
            else await writer.WriteLineAsync("250 OK");
        }
        return body.ToString();
    }
    private sealed class Environment : IWebHostEnvironment {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Coiny.Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
