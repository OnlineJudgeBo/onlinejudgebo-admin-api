using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender
{
    public async Task SendAsync(EmailMessage email)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(email.FromEmail, email.FromName),
            Subject = email.Subject,
            Body = email.Text,
            IsBodyHtml = false
        };

        message.To.Add(new MailAddress(email.ToEmail, email.ToName));
        if (email.Html != null)
        {
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(email.Text, null, MediaTypeNames.Text.Plain));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(email.Html, null, MediaTypeNames.Text.Html));
        }

        using var client = new SmtpClient(email.Host, email.Port)
        {
            EnableSsl = email.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(email.Username, email.Password)
        };

        try
        {
            await client.SendMailAsync(message);
        }
        catch (SmtpException ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }
}
