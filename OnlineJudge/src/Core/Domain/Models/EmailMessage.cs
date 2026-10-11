namespace OnlineJudgeAdmin.Core.Domain.Models;

public record EmailMessage(
    string Host,
    int Port,
    bool UseSsl,
    string Username,
    string Password,
    string FromEmail,
    string FromName,
    string ToEmail,
    string ToName,
    string Subject,
    string Text,
    string? Html = null);
