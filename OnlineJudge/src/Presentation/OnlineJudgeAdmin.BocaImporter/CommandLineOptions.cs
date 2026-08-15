namespace OnlineJudgeAdmin.BocaImporter;

public sealed class CommandLineOptions
{
    // Matches the read-only mount added to the patito-api service in docker-compose.yml.
    private const string DefaultBocaDir = "/boca_problems";

    public required string BocaDir { get; init; }

    public string? UserId { get; init; }

    public int SiteId { get; init; }

    public bool DryRun { get; init; }

    // Each entry is either a .zip file name or an already-extracted folder name, both
    // relative to BocaDir (or an absolute/relative path of its own).
    public List<string> Targets { get; init; } = [];

    public static CommandLineOptions Parse(string[] args)
    {
        string? bocaDir = null;
        string? userId = null;
        int? siteId = null;
        var dryRun = false;
        var targets = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dir":
                    bocaDir = args[++i];
                    break;
                case "--user-id":
                    userId = args[++i];
                    break;
                case "--site-id":
                    siteId = int.Parse(args[++i]);
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"Unknown option: {args[i]}");
                    }
                    targets.Add(args[i]);
                    break;
            }
        }

        if (!dryRun && string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("--user-id is required unless --dry-run is set.");
        }

        if (!dryRun && (siteId is null || siteId <= 0))
        {
            throw new ArgumentException("--site-id is required (and must be positive) unless --dry-run is set.");
        }

        return new CommandLineOptions
        {
            BocaDir = bocaDir ?? DefaultBocaDir,
            UserId = userId,
            SiteId = siteId ?? 0,
            DryRun = dryRun,
            Targets = targets,
        };
    }
}
