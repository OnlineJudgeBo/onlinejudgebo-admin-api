using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public partial class PublicRepository : IPublicRepository
{
    private const short AcceptedResultCode = 4;

    private readonly AppDbContext _context;
    private readonly AcademicCatalogDbContext _academicContext;

    public PublicRepository(AppDbContext context, AcademicCatalogDbContext academicContext)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _academicContext = academicContext ?? throw new ArgumentNullException(nameof(academicContext));
    }

    private IQueryable<DbSolution> OfficialSolutions()
    {
        return _context.Solutions.Where(solution => !_context.CustomInputs.Any(customInput => customInput.SolutionId == solution.SolutionId));
    }

    private sealed class ProblemMetadata
    {
        public static ProblemMetadata Empty { get; } = new();

        public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<int> Years { get; } = new();

        public HashSet<string> Tracks { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string OriginSource { get; set; } = string.Empty;
    }

    private sealed class ProblemListProjection
    {
        public int ProblemId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public int Accepted { get; set; }

        public int Submit { get; set; }

        public int Solved { get; set; }

        public DateTime? PublishedAt { get; set; }
    }

    private sealed class ContestProjection
    {
        public int ContestId { get; set; }

        public string Title { get; set; } = string.Empty;

        public DateTime StartTimeUtc { get; set; }

        public DateTime EndTimeUtc { get; set; }

        public bool IsPrivate { get; set; }

        public string Track { get; set; } = "GENERAL";

        public string Level { get; set; } = "PRACTICE";

        public bool IsPromoted { get; set; }
    }

    private sealed class ContestProblemReference
    {
        public int ContestId { get; set; }

        public int ProblemId { get; set; }

        public int Num { get; set; }

        public string ContestProblemId { get; set; } = string.Empty;
    }
}
