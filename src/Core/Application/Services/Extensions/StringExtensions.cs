namespace OnlineJudgeAdmin.Core.Application.Services.Extensions;

internal static class StringExtensions
{
    internal static string RemoveAllWhiteSpace(this string text)
    {
        return text.Replace(" ", "");
    }
}