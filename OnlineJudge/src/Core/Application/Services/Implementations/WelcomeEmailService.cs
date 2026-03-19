using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class WelcomeEmailService : IWelcomeEmailService
{
    private readonly IConfiguration _configuration;

    public WelcomeEmailService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task SendWelcomeAsync(string email, string userId, string? displayName = null)
    {
        var (hostKey, host) = GetConfiguredValue(
            "Email:Smtp:Host",
            "WelcomeEmail:SmtpHost",
            "PasswordRecovery:SmtpHost");
        var (usernameKey, username) = GetConfiguredValue(
            "Email:Smtp:Username",
            "WelcomeEmail:SmtpUsername",
            "PasswordRecovery:SmtpUsername");
        var (passwordKey, password) = GetConfiguredValue(
            "Email:Smtp:Password",
            "WelcomeEmail:SmtpPassword",
            "PasswordRecovery:SmtpPassword");
        var (fromEmailKey, configuredFromEmail) = GetConfiguredValue(
            "Email:Smtp:From:Email",
            "WelcomeEmail:FromEmail",
            "PasswordRecovery:FromEmail");
        var fromEmail = configuredFromEmail ?? username;

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(fromEmail))
        {
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(host))
            {
                missingFields.Add("Email:Smtp:Host");
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                missingFields.Add("Email:Smtp:Username");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                missingFields.Add("Email:Smtp:Password");
            }

            if (string.IsNullOrWhiteSpace(fromEmail))
            {
                missingFields.Add("Email:Smtp:From:Email");
            }

            Console.Error.WriteLine(
                $"[WelcomeEmail] Configuración incompleta. MissingFields={string.Join(", ", missingFields)} Recipient={email} UserId={userId}");

            throw new InvalidOperationException($"La configuración de correo de bienvenida no está completa. MissingFields: {string.Join(", ", missingFields)}");
        }

        var port = 465;
        if (int.TryParse(_configuration["Email:Smtp:Port"] ?? _configuration["WelcomeEmail:SmtpPort"] ?? _configuration["PasswordRecovery:SmtpPort"], out var configuredPort))
        {
            port = configuredPort;
        }

        var useSsl = true;
        if (bool.TryParse(_configuration["Email:Smtp:UseSsl"] ?? _configuration["WelcomeEmail:UseSsl"] ?? _configuration["PasswordRecovery:UseSsl"], out var configuredUseSsl))
        {
            useSsl = configuredUseSsl;
        }

        var fromName = _configuration["Email:Smtp:From:Name"]
            ?? _configuration["WelcomeEmail:FromName"]
            ?? _configuration["PasswordRecovery:FromName"]
            ?? "Juez Virtual";
        var subject = _configuration["Email:Welcome:Subject"]
            ?? _configuration["WelcomeEmail:Subject"]
            ?? "Bienvenido a Juez Virtual";
        var appName = _configuration["Email:AppName"]
            ?? _configuration["WelcomeEmail:AppName"]
            ?? _configuration["PasswordRecovery:AppName"]
            ?? "Juez Virtual";
        var portalUrl = _configuration["Email:Welcome:PortalUrl"] ?? _configuration["WelcomeEmail:PortalUrl"];
        var recipientName = string.IsNullOrWhiteSpace(displayName) ? userId : displayName.Trim();

        var plainTextBody = BuildPlainTextBody(recipientName, appName, userId, portalUrl);
        var htmlBody = BuildHtmlBody(recipientName, appName, userId, portalUrl);

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = plainTextBody,
            IsBodyHtml = false
        };

        message.To.Add(new MailAddress(email, recipientName));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plainTextBody, null, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = useSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(username, password)
        };

        try
        {
            Console.Error.WriteLine(
                $"[WelcomeEmail] Enviando welcome mail Host={host} Port={port} UseSsl={useSsl} From={fromEmail} To={email} UserId={userId} Subject={subject}");
            Console.Error.WriteLine(
                $"[WelcomeEmail] Config Debug HostKey={hostKey} UsernameKey={usernameKey} PasswordKey={passwordKey} FromKey={fromEmailKey} UsernameLoaded={!string.IsNullOrWhiteSpace(username)} UsernameLength={username?.Length ?? 0} PasswordLoaded={!string.IsNullOrWhiteSpace(password)} PasswordLength={password?.Length ?? 0}");

            await client.SendMailAsync(message);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
        {
            Console.Error.WriteLine(
                $"[WelcomeEmail] Fallo SMTP Host={host} Port={port} UseSsl={useSsl} From={fromEmail} To={email} UserId={userId} Subject={subject}");
            Console.Error.WriteLine(ex.ToString());

            throw new InvalidOperationException(
                $"No se pudo enviar el correo de bienvenida. Host={host}; Port={port}; To={email}; Reason={ex.Message}",
                ex);
        }
    }

    private (string Key, string? Value) GetConfiguredValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return (key, value);
            }
        }

        return (keys.FirstOrDefault() ?? string.Empty, null);
    }

    private static string BuildPlainTextBody(string recipientName, string appName, string userId, string? portalUrl)
    {
        var body = $"""
Hola {recipientName},

Tu cuenta en {appName} fue creada correctamente.

Usuario: {userId}

Ya puedes iniciar sesión y comenzar a resolver problemas.
""";

        if (!string.IsNullOrWhiteSpace(portalUrl))
        {
            body += $"""

Accede aquí: {portalUrl}
""";
        }

        body += """

Bienvenido a Juez Virtual.
""";

        return body;
    }

    private static string BuildHtmlBody(string recipientName, string appName, string userId, string? portalUrl)
    {
        var safeRecipientName = WebUtility.HtmlEncode(recipientName);
        var safeAppName = WebUtility.HtmlEncode(appName);
        var safeUserId = WebUtility.HtmlEncode(userId);
        var safePortalUrl = string.IsNullOrWhiteSpace(portalUrl) ? null : WebUtility.HtmlEncode(portalUrl);

        var actionBlock = string.IsNullOrWhiteSpace(safePortalUrl)
            ? string.Empty
            : $"""
              <tr>
                <td style="padding: 0 32px 28px 32px;">
                  <a href="{safePortalUrl}" style="display:inline-block;background:#0f5bd8;color:#ffffff;text-decoration:none;padding:14px 22px;border-radius:10px;font-weight:600;">
                    Ir al Juez Virtual
                  </a>
                </td>
              </tr>
              <tr>
                <td style="padding: 0 32px 24px 32px;color:#5b6475;font-size:14px;line-height:1.6;">
                  Si el boton no funciona, copia y pega este enlace en tu navegador:<br />
                  <span style="color:#0f5bd8;">{safePortalUrl}</span>
                </td>
              </tr>
              """;

        return $"""
<!DOCTYPE html>
<html lang="es">
  <body style="margin:0;padding:0;background:#eef2f7;font-family:Arial,sans-serif;color:#172033;">
    <table role="presentation" cellpadding="0" cellspacing="0" width="100%" style="background:#eef2f7;padding:32px 16px;">
      <tr>
        <td align="center">
          <table role="presentation" cellpadding="0" cellspacing="0" width="100%" style="max-width:600px;background:#ffffff;border-radius:18px;overflow:hidden;border:1px solid #dbe3ef;">
            <tr>
              <td style="padding:28px 32px;background:linear-gradient(135deg,#0f5bd8,#123b86);color:#ffffff;">
                <div style="font-size:13px;letter-spacing:0.12em;text-transform:uppercase;opacity:0.85;">Juez Virtual</div>
                <div style="margin-top:10px;font-size:28px;font-weight:700;line-height:1.2;">Bienvenido, {safeRecipientName}</div>
                <div style="margin-top:8px;font-size:15px;line-height:1.6;opacity:0.92;">
                  Tu cuenta en {safeAppName} ya esta lista para usarse.
                </div>
              </td>
            </tr>
            <tr>
              <td style="padding:32px 32px 20px 32px;font-size:15px;line-height:1.7;">
                Ya puedes iniciar sesion y comenzar a resolver problemas, participar en cursos y practicar en el juez.
              </td>
            </tr>
            <tr>
              <td style="padding:0 32px 28px 32px;">
                <table role="presentation" cellpadding="0" cellspacing="0" width="100%" style="background:#f6f8fc;border:1px solid #dbe3ef;border-radius:14px;">
                  <tr>
                    <td style="padding:18px 20px;">
                      <div style="font-size:12px;text-transform:uppercase;letter-spacing:0.08em;color:#6a7385;">Usuario</div>
                      <div style="margin-top:6px;font-size:22px;font-weight:700;color:#172033;">{safeUserId}</div>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
            {actionBlock}
            <tr>
              <td style="padding:0 32px 32px 32px;color:#5b6475;font-size:14px;line-height:1.6;">
                Si no creaste esta cuenta, puedes ignorar este mensaje.
              </td>
            </tr>
            <tr>
              <td style="padding:18px 32px;background:#f6f8fc;color:#6a7385;font-size:12px;line-height:1.6;">
                Este es un correo automatico de {safeAppName}.
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </body>
</html>
""";
    }
}
