namespace OnlineJudgeAdmin.Core.Domain.Models;

public static class ContestProblemCode
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string FromNumber(int number)
    {
        if (number < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "El índice del problema del concurso no puede ser negativo.");
        }

        var letter = Alphabet[number % Alphabet.Length];
        var suffix = number / Alphabet.Length;

        return suffix == 0 ? letter.ToString() : $"{letter}{suffix}";
    }

    public static bool TryParse(string? value, out int number)
    {
        number = -1;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length == 0)
        {
            return false;
        }

        var letter = normalized[0];
        var letterIndex = Alphabet.IndexOf(letter);
        if (letterIndex < 0)
        {
            return false;
        }

        if (normalized.Length == 1)
        {
            number = letterIndex;
            return true;
        }

        if (!int.TryParse(normalized[1..], out var suffix) || suffix < 0)
        {
            return false;
        }

        number = (suffix * Alphabet.Length) + letterIndex;
        return true;
    }
}
