using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Application.Services.Helpers;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class PasswordRecoveryEmailService : IPasswordRecoveryEmailService
{
    private readonly IConfiguration _configuration;

    public PasswordRecoveryEmailService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task SendRecoveryCodeAsync(string email, string userId, string recoveryCode, int siteId, string? displayName = null)
    {
        string? Setting(string key, params string[] legacyKeys) => SiteEmailSettings.Get(_configuration, siteId, key, legacyKeys);

        var host = Setting("Smtp:Host", "PasswordRecovery:SmtpHost");
        var username = Setting("Smtp:Username", "PasswordRecovery:SmtpUsername");
        var password = Setting("Smtp:Password", "PasswordRecovery:SmtpPassword");
        var fromEmail = Setting("Smtp:From:Email", "PasswordRecovery:FromEmail") ?? username;

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException("La configuración de correo de recuperación no está completa.");
        }

        var port = 465;
        if (int.TryParse(Setting("Smtp:Port", "PasswordRecovery:SmtpPort"), out var configuredPort))
        {
            port = configuredPort;
        }

        var useSsl = true;
        if (bool.TryParse(Setting("Smtp:UseSsl", "PasswordRecovery:UseSsl"), out var configuredUseSsl))
        {
            useSsl = configuredUseSsl;
        }
        var fromName = Setting("Smtp:From:Name", "PasswordRecovery:FromName") ?? "Juez Virtual";
        var subject = Setting("PasswordRecovery:Subject", "PasswordRecovery:Subject") ?? "Recuperación de contraseña";
        var appName = Setting("AppName", "PasswordRecovery:AppName") ?? "Juez Virtual";
        var recipientName = string.IsNullOrWhiteSpace(displayName) ? userId : displayName.Trim();

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = $"""
Hola {recipientName},

Recibimos una solicitud para recuperar tu contraseña en {appName}.

Tu código de recuperación es: {recoveryCode}

Ese código será tu contraseña temporal después de confirmarlo en la pantalla de recuperación.
Si no solicitaste este cambio, ignora este correo.
""",
            IsBodyHtml = false
        };

        message.To.Add(new MailAddress(email, recipientName));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = useSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(username, password)
        };

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
        {
            throw new InvalidOperationException("No se pudo enviar el correo de recuperación.", ex);
        }
    }
}
