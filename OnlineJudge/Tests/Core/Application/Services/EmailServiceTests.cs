using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class EmailServiceTests
{
    [Fact]
    public async Task Welcome_UsesGlobalSettingsForSiteWithoutOverrides()
    {
        using var smtp = new FakeSmtpServer();
        var service = Welcome(Config(smtp.Port));

        await service.SendWelcomeAsync("ana@example.com", "ana", siteId: 1, displayName: "Ana");

        var message = Assert.Single(smtp.Messages);
        Assert.Contains("<no-reply@global.bo>", message.From);
        Assert.Contains("\"Juez Global\"", message.From);
        Assert.Contains("ana@example.com", message.To);
        Assert.Contains("Subject: Bienvenido global", message.Raw);
        Assert.Contains("https://global.bo/oj", message.DecodedBody);
    }

    [Fact]
    public async Task Welcome_UsesSiteOverridesForSenderBrandAndPortal()
    {
        using var smtp = new FakeSmtpServer();
        var service = Welcome(Config(smtp.Port, new()
        {
            ["Email:Sites:2:Smtp:From:Email"] = "no-reply@site2.bo",
            ["Email:Sites:2:Smtp:From:Name"] = "Sitio Dos",
            ["Email:Sites:2:AppName"] = "Sitio Dos",
            ["Email:Sites:2:Welcome:Subject"] = "Bienvenido a Sitio Dos",
            ["Email:Sites:2:Welcome:PortalUrl"] = "https://site2.bo/oj",
        }));

        await service.SendWelcomeAsync("bob@example.com", "bob", siteId: 2);

        var message = Assert.Single(smtp.Messages);
        Assert.Contains("<no-reply@site2.bo>", message.From);
        Assert.Contains("Subject: Bienvenido a Sitio Dos", message.Raw);
        Assert.Contains("https://site2.bo/oj", message.DecodedBody);
        Assert.Contains("Sitio Dos", message.DecodedBody);
        Assert.DoesNotContain("global.bo", message.DecodedBody);
        Assert.DoesNotContain("Juez Global", message.DecodedBody);
    }

    [Fact]
    public async Task Welcome_HtmlEncodesUserSuppliedValues()
    {
        using var smtp = new FakeSmtpServer();
        var service = Welcome(Config(smtp.Port));

        await service.SendWelcomeAsync("ana@example.com", "ana", 1, "<script>x</script>");

        var body = Assert.Single(smtp.Messages).DecodedBody;
        Assert.Contains("&lt;script&gt;x&lt;/script&gt;", body);
        Assert.DoesNotContain("<script>x</script></div>", body);
    }

    [Fact]
    public async Task Welcome_FallsBackToUserIdAndOmitsPortalLinkWhenNotConfigured()
    {
        using var smtp = new FakeSmtpServer();
        var settings = BaseSettings(smtp.Port);
        settings.Remove("Email:Welcome:PortalUrl");
        settings.Remove("Email:Smtp:From:Email");
        settings.Remove("Email:Smtp:From:Name");
        settings.Remove("Email:AppName");
        var service = Welcome(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        await service.SendWelcomeAsync("ana@example.com", "ana_user", 1, "  ");

        var message = Assert.Single(smtp.Messages);
        Assert.Contains("<smtp-user@global.bo>", message.From);
        Assert.Contains("Juez Virtual", message.DecodedBody);
        Assert.Contains("Hola ana_user", message.DecodedBody);
        Assert.DoesNotContain("Accede aquí", message.DecodedBody);
        Assert.DoesNotContain("Ir a ", message.DecodedBody);
    }

    [Fact]
    public async Task Welcome_ReadsLegacyKeys()
    {
        using var smtp = new FakeSmtpServer();
        var service = Welcome(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WelcomeEmail:SmtpHost"] = "127.0.0.1",
            ["WelcomeEmail:SmtpPort"] = smtp.Port.ToString(),
            ["WelcomeEmail:UseSsl"] = "false",
            ["WelcomeEmail:SmtpUsername"] = "legacy@global.bo",
            ["WelcomeEmail:SmtpPassword"] = "pw",
            ["WelcomeEmail:Subject"] = "Legacy subject",
        }).Build());

        await service.SendWelcomeAsync("ana@example.com", "ana", 1);

        var message = Assert.Single(smtp.Messages);
        Assert.Contains("<legacy@global.bo>", message.From);
        Assert.Contains("Subject: Legacy subject", message.Raw);
    }

    [Fact]
    public async Task Welcome_ReportsEveryMissingSetting()
    {
        var service = Welcome(new ConfigurationBuilder().Build());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendWelcomeAsync("ana@example.com", "ana", 1));

        Assert.Contains("Email:Smtp:Host", error.Message);
        Assert.Contains("Email:Smtp:Username", error.Message);
        Assert.Contains("Email:Smtp:Password", error.Message);
        Assert.Contains("Email:Smtp:From:Email", error.Message);
    }

    [Fact]
    public async Task Welcome_ReportsOnlyTheMissingPassword()
    {
        var settings = BaseSettings(25);
        settings.Remove("Email:Smtp:Password");
        var service = Welcome(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendWelcomeAsync("ana@example.com", "ana", 1));

        Assert.Contains("Email:Smtp:Password", error.Message);
        Assert.DoesNotContain("Email:Smtp:Host", error.Message);
    }

    [Fact]
    public async Task Welcome_WrapsDeliveryFailure()
    {
        var service = Welcome(Config(ClosedPort()));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendWelcomeAsync("ana@example.com", "ana", 1));

        Assert.Equal("No se pudo enviar el correo de bienvenida.", error.Message);
        Assert.NotNull(error.InnerException);
    }

    [Fact]
    public void Welcome_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new WelcomeEmailService(null!, NullLogger<WelcomeEmailService>.Instance));
        Assert.Throws<ArgumentNullException>(() => new WelcomeEmailService(new ConfigurationBuilder().Build(), null!));
    }

    [Fact]
    public async Task Recovery_SendsCodeWithSiteSettings()
    {
        using var smtp = new FakeSmtpServer();
        var service = new PasswordRecoveryEmailService(Config(smtp.Port, new()
        {
            ["Email:Sites:2:Smtp:From:Email"] = "no-reply@site2.bo",
            ["Email:Sites:2:AppName"] = "Sitio Dos",
            ["Email:Sites:2:PasswordRecovery:Subject"] = "Recupera tu cuenta",
        }));

        await service.SendRecoveryCodeAsync("ana@example.com", "ana", "ABC123DEF456", siteId: 2, displayName: "Ana");

        var message = Assert.Single(smtp.Messages);
        Assert.Contains("<no-reply@site2.bo>", message.From);
        Assert.Contains("ana@example.com", message.To);
        Assert.Contains("Subject: Recupera tu cuenta", message.Raw);
        Assert.Contains("ABC123DEF456", message.DecodedBody);
        Assert.Contains("Sitio Dos", message.DecodedBody);
        Assert.Contains("Hola Ana", message.DecodedBody);
    }

    [Fact]
    public async Task Recovery_UsesDefaultsAndUserIdWhenOptionalValuesMissing()
    {
        using var smtp = new FakeSmtpServer();
        var settings = BaseSettings(smtp.Port);
        settings.Remove("Email:AppName");
        settings.Remove("Email:Smtp:From:Email");
        var service = new PasswordRecoveryEmailService(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        await service.SendRecoveryCodeAsync("ana@example.com", "ana_user", "CODE01", 1);

        var message = Assert.Single(smtp.Messages);
        Assert.Contains("<smtp-user@global.bo>", message.From);
        Assert.Contains("Subject: =?utf-8?", message.Raw);
        Assert.Contains("Hola ana_user", message.DecodedBody);
        Assert.Contains("Juez Virtual", message.DecodedBody);
    }

    [Fact]
    public async Task Recovery_RejectsIncompleteConfiguration()
    {
        var service = new PasswordRecoveryEmailService(new ConfigurationBuilder().Build());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendRecoveryCodeAsync("ana@example.com", "ana", "CODE01", 1));

        Assert.Equal("La configuración de correo de recuperación no está completa.", error.Message);
    }

    [Fact]
    public async Task Recovery_WrapsDeliveryFailure()
    {
        var service = new PasswordRecoveryEmailService(Config(ClosedPort()));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendRecoveryCodeAsync("ana@example.com", "ana", "CODE01", 1));

        Assert.Equal("No se pudo enviar el correo de recuperación.", error.Message);
    }

    [Fact]
    public void Recovery_RejectsNullConfiguration()
    {
        Assert.Throws<ArgumentNullException>(() => new PasswordRecoveryEmailService(null!));
    }

    private static WelcomeEmailService Welcome(IConfiguration configuration) =>
        new(configuration, NullLogger<WelcomeEmailService>.Instance);

    private static Dictionary<string, string?> BaseSettings(int port) => new()
    {
        ["Email:Smtp:Host"] = "127.0.0.1",
        ["Email:Smtp:Port"] = port.ToString(),
        ["Email:Smtp:UseSsl"] = "false",
        ["Email:Smtp:Username"] = "smtp-user@global.bo",
        ["Email:Smtp:Password"] = "secret",
        ["Email:Smtp:From:Email"] = "no-reply@global.bo",
        ["Email:Smtp:From:Name"] = "Juez Global",
        ["Email:AppName"] = "Juez Global",
        ["Email:Welcome:Subject"] = "Bienvenido global",
        ["Email:Welcome:PortalUrl"] = "https://global.bo/oj",
    };

    private static IConfiguration Config(int port, Dictionary<string, string?>? overrides = null)
    {
        var settings = BaseSettings(port);
        foreach (var (key, value) in overrides ?? new())
        {
            settings[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private static int ClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public sealed record CapturedMessage(string From, string To, string Raw)
    {
        // Bodies are sent quoted-printable or base64; decode every base64 part so assertions can read the text.
        public string DecodedBody
        {
            get
            {
                var builder = new StringBuilder(Raw.Replace("=\r\n", string.Empty));
                foreach (var block in Raw.Split("\r\n\r\n").Skip(1))
                {
                    var candidate = string.Concat(block.Split("\r\n").TakeWhile(line => line.Length > 0 && !line.StartsWith("--")));
                    try
                    {
                        builder.Append(Encoding.UTF8.GetString(Convert.FromBase64String(candidate)));
                    }
                    catch (FormatException)
                    {
                    }
                }

                return builder.ToString();
            }
        }
    }

    // Minimal SMTP server: no TLS, no AUTH advertised, accepts one or more messages per connection.
    private sealed class FakeSmtpServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly List<CapturedMessage> _messages = new();
        private readonly Task _loop;

        public FakeSmtpServer()
        {
            _listener.Start();
            _loop = Task.Run(AcceptLoop);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public IReadOnlyList<CapturedMessage> Messages
        {
            get
            {
                lock (_messages)
                {
                    return _messages.ToList();
                }
            }
        }

        private async Task AcceptLoop()
        {
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync();
                    await HandleAsync(client);
                }
            }
            catch (Exception) when (!_listener.Server.IsBound)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync("220 fake");
            string from = string.Empty, to = string.Empty;

            while (await reader.ReadLineAsync() is { } line)
            {
                var command = line.ToUpperInvariant();
                if (command.StartsWith("EHLO") || command.StartsWith("HELO"))
                {
                    await writer.WriteLineAsync("250 fake");
                }
                else if (command.StartsWith("MAIL FROM"))
                {
                    await writer.WriteLineAsync("250 OK");
                }
                else if (command.StartsWith("RCPT TO"))
                {
                    to += line;
                    await writer.WriteLineAsync("250 OK");
                }
                else if (command == "DATA")
                {
                    await writer.WriteLineAsync("354 go");
                    var raw = new StringBuilder();
                    while (await reader.ReadLineAsync() is { } dataLine && dataLine != ".")
                    {
                        raw.Append(dataLine).Append("\r\n");
                        if (dataLine.StartsWith("From:", StringComparison.OrdinalIgnoreCase))
                        {
                            from = dataLine;
                        }
                    }

                    lock (_messages)
                    {
                        _messages.Add(new CapturedMessage(from, to, raw.ToString()));
                    }

                    to = string.Empty;
                    await writer.WriteLineAsync("250 queued");
                }
                else if (command == "QUIT")
                {
                    await writer.WriteLineAsync("221 bye");
                    return;
                }
                else
                {
                    await writer.WriteLineAsync("250 OK");
                }
            }
        }

        public void Dispose()
        {
            _listener.Stop();
        }
    }
}
