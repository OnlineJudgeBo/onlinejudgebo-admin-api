using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public partial class AcademicRepository : IAcademicRepository
{
    private const short AcceptedResultCode = 4;

    private sealed class CourseScopedSolutionRow
    {
        public int SolutionId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public int ProblemId { get; set; }

        public short Result { get; set; }

        public long CourseId { get; set; }

        public long AssignmentId { get; set; }
    }

    private sealed class LearningPathProgressContext
    {
        public long LearningPathId { get; init; }

        public string LearningPathKey { get; init; } = string.Empty;

        public List<string> OrderedTopicIds { get; init; } = new();

        public HashSet<string> ValidTopicIdSet { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly AppDbContext _context;
    private readonly AcademicCatalogDbContext _academicContext;

    public AcademicRepository(AppDbContext context, AcademicCatalogDbContext academicContext)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _academicContext = academicContext ?? throw new ArgumentNullException(nameof(academicContext));
    }

    public Task<IEnumerable<AcademicInstitution>> GetInstitutionsAsync(int siteId)
    {
        return Task.FromResult<IEnumerable<AcademicInstitution>>(Array.Empty<AcademicInstitution>());
    }

    public Task<IEnumerable<AcademicInstitutionRankingItem>> GetInstitutionsRankingAsync(int siteId, int limit)
    {
        return Task.FromResult<IEnumerable<AcademicInstitutionRankingItem>>(Array.Empty<AcademicInstitutionRankingItem>());
    }
}
