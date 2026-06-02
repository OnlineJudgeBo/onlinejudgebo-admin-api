using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class PasswordRecoveryEmailService : IPasswordRecoveryEmailService
{
    private readonly IConfiguration _configuration;

    public PasswordRecoveryEmailService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task SendRecoveryCodeAsync(string email, string userId, string recoveryCode, string? displayName = null)
    {
        var host = _configuration["Email:Smtp:Host"] ?? _configuration["PasswordRecovery:SmtpHost"];
        var username = _configuration["Email:Smtp:Username"] ?? _configuration["PasswordRecovery:SmtpUsername"];
        var password = _configuration["Email:Smtp:Password"] ?? _configuration["PasswordRecovery:SmtpPassword"];
        var fromEmail = _configuration["Email:Smtp:From:Email"] ?? _configuration["PasswordRecovery:FromEmail"] ?? username;

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException("La configuración de correo de recuperación no está completa.");
        }

        var port = 465;
        if (int.TryParse(_configuration["Email:Smtp:Port"] ?? _configuration["PasswordRecovery:SmtpPort"], out var configuredPort))
        {
            port = configuredPort;
        }

        var useSsl = true;
        if (bool.TryParse(_configuration["Email:Smtp:UseSsl"] ?? _configuration["PasswordRecovery:UseSsl"], out var configuredUseSsl))
        {
            useSsl = configuredUseSsl;
        }
        var fromName = _configuration["Email:Smtp:From:Name"] ?? _configuration["PasswordRecovery:FromName"] ?? "Juez Virtual";
        var subject = _configuration["Email:PasswordRecovery:Subject"] ?? _configuration["PasswordRecovery:Subject"] ?? "Recuperación de contraseña";
        var appName = _configuration["Email:AppName"] ?? _configuration["PasswordRecovery:AppName"] ?? "Juez Virtual";
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
