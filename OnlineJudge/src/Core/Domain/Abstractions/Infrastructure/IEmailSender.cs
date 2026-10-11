using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public interface IEmailSender
{
    // Throws InvalidOperationException when the message cannot be delivered.
    Task SendAsync(EmailMessage email);
}
