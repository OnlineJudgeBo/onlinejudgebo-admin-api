using System.Text.Json;
using System.Text.RegularExpressions;

namespace OnlineJudgeAdmin.Core.Domain.Models;

// How scoring.json is read, written and matched against the tests of a problem.
public static class ProblemScoringRules
{
    public const string FileName = "scoring.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly Regex SampleTest = new(@"^sample\d*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ProblemScoring Parse(byte[] content)
    {
        try
        {
            return JsonSerializer.Deserialize<ProblemScoring>(content, JsonOptions) ?? new ProblemScoring();
        }
        catch (JsonException)
        {
            // A hand-edited file the kernel would also ignore.
            return new ProblemScoring();
        }
    }

    public static string Serialize(IEnumerable<ProblemScoreGroup> groups) => JsonSerializer.Serialize(new { groups }, JsonOptions);

    // Same matching as the kernel's fnmatch: *, ? and [set].
    public static bool Matches(string pattern, string test) =>
        Regex.IsMatch(test, "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".").Replace(@"\[", "[") + "$");

    // Judge tests are the .in files; the public samples are not scored.
    public static List<string> AvailableTests(IReadOnlyList<string> fileNames) =>
        fileNames
            .Where(name => name.EndsWith(".in", StringComparison.Ordinal))
            .Select(name => name[..^3])
            .Where(name => !SampleTest.IsMatch(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
}
