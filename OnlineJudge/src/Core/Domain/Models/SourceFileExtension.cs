namespace OnlineJudgeAdmin.Core.Domain.Models;

// File extension of a submission from the name of its language.
public static class SourceFileExtension
{
    public static string FromLanguageName(string languageName)
    {
        var normalized = languageName.Trim().ToLowerInvariant();

        if (normalized.Contains("c++") || normalized.Contains("cpp"))
        {
            return ".cpp";
        }

        if (normalized == "c" || normalized.StartsWith("c "))
        {
            return ".c";
        }

        // Before "java": "javascript".Contains("java").
        if (normalized.Contains("javascript"))
        {
            return ".js";
        }

        if (normalized.Contains("typescript"))
        {
            return ".ts";
        }

        if (normalized.Contains("java"))
        {
            return ".java";
        }

        if (normalized.Contains("python"))
        {
            return ".py";
        }

        if (normalized.Contains("c#") || normalized.Contains("csharp"))
        {
            return ".cs";
        }

        if (normalized.Contains("go"))
        {
            return ".go";
        }

        if (normalized.Contains("rust"))
        {
            return ".rs";
        }

        if (normalized.Contains("pascal"))
        {
            return ".pas";
        }

        if (normalized.Contains("kotlin"))
        {
            return ".kt";
        }

        return ".txt";
    }
}
