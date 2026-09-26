using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

// Sqlite-backed judge database with small builders for the rows most queries need.
// FK enforcement is off so each test seeds only the tables its query reads.
internal sealed class JudgeSeed : IDisposable
{
    private readonly SqliteContext<AppDbContext> _app = CreateSqliteContext<AppDbContext>(options => new SqliteAppDbContext(options));
    private readonly SqliteContext<AcademicCatalogDbContext> _academic = CreateSqliteContext<AcademicCatalogDbContext>(options => new SqliteAcademicCatalogDbContext(options));
    private int _nextSolutionId = 1000;

    public JudgeSeed()
    {
        Db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
        Academic.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
    }

    public AppDbContext Db => _app.Context;

    public AcademicCatalogDbContext Academic => _academic.Context;

    public PublicRepository PublicRepository() => new(Db, Academic);

    public JudgeSeed Site(int siteId)
    {
        Db.Sites.Add(new DbSite { SiteId = siteId, Name = "Site " + siteId });
        return Save();
    }

    public JudgeSeed User(string userId, int siteId = 1, string? nick = null, string? school = null, bool active = true)
    {
        Db.Users.Add(new DbUser { UserId = userId, Ip = "127.0.0.1", SiteId = siteId, IsActive = active, IsDeleted = false, Password = "x" });
        Db.UserProfiles.Add(new DbUserProfile { UserId = userId, SiteId = siteId, Nick = nick ?? string.Empty, School = school, Email = userId + "@mail.bo" });
        return Save();
    }

    public int Problem(string title = "Problema", int siteId = 1, bool active = true, string defunct = "N", string? source = null, string? originSource = null, DateTime? inDate = null)
    {
        var problem = new DbProblem
        {
            Title = title, Spj = "N", Defunct = defunct, TimeLimit = 1, MemoryLimit = 128,
            Source = source, OriginSource = originSource, InDate = inDate ?? new DateTime(2025, 1, 1), Description = title + " desc"
        };
        Db.Problems.Add(problem);
        Save();
        Db.ProblemSites.Add(new DbProblemSite { problemId = problem.ProblemId!.Value, SiteId = siteId, IsActive = active });
        Save();
        return problem.ProblemId!.Value;
    }

    public int Contest(string title, DateTime start, DateTime end, int siteId = 1, bool isPrivate = false, string defunct = "N", string track = "GENERAL", string level = "PRACTICE")
    {
        var contest = new DbContest
        {
            Title = title, StartTime = start, EndTime = end, Defunct = defunct, Private = (sbyte)(isPrivate ? 1 : 0), Track = track, Level = level, Description = title + " desc"
        };
        Db.Contests.Add(contest);
        Save();
        Db.ContestSites.Add(new DbContestSite { ContestId = contest.ContestId, SiteId = siteId });
        Save();
        return contest.ContestId;
    }

    public JudgeSeed ContestProblem(int contestId, int problemId, int num, string? title = null)
    {
        Db.ContestProblems.Add(new DbContestProblem { ContestId = contestId, ProblemId = problemId, Num = num, Title = title ?? string.Empty });
        return Save();
    }

    public JudgeSeed ContestUser(int contestId, string userId, int siteId = 1, bool isOwner = false)
    {
        Db.ContestUsers.Add(new DbContestUser { ContestId = contestId, UserId = userId, SiteId = siteId, IsOwner = isOwner });
        return Save();
    }

    public JudgeSeed Privilege(string userId, string right)
    {
        Db.Privilege.Add(new DbPrivilege { UserId = userId, Rightstr = right, Defunct = "N" });
        return Save();
    }

    public JudgeSeed Tag(string name, params int[] problemIds)
    {
        var topic = Db.Topics.FirstOrDefault(item => item.Name == "Temas") ?? Db.Topics.Add(new DbTopic { Name = "Temas" }).Entity;
        Save();
        var classification = new DbClassification { Name = name, TopicId = topic.TopicId };
        Db.Classifications.Add(classification);
        Save();
        foreach (var problemId in problemIds)
        {
            Db.Database.ExecuteSqlRaw("INSERT INTO problem_classification (problem_id, classification_id) VALUES ({0}, {1})", problemId, classification.ClassificationId);
        }

        return this;
    }

    public JudgeSeed Online(string hash, DateTimeOffset lastMove, DateTimeOffset? firstSeen = null, string uri = "/oj/")
    {
        Db.Online.Add(new DbOnline { Hash = hash, Ip = "1.1.1.1", Ua = "Mozilla", Lastmove = (int)lastMove.ToUnixTimeSeconds(), Firsttime = (int?)firstSeen?.ToUnixTimeSeconds(), Uri = uri });
        return Save();
    }

    public JudgeSeed Language(int languageId, string name)
    {
        Db.ProgrammingLanguages.Add(new DbProgrammingLanguage { LanguageId = languageId, Name = name });
        return Save();
    }

    public int Solution(string userId, int problemId, short result, DateTime? inDate = null, int siteId = 1, int? contestId = null, int num = -1, uint language = 1, bool customInput = false, string? source = null, int time = 10, int memory = 1024)
    {
        var solution = new DbSolution
        {
            SolutionId = ++_nextSolutionId, UserId = userId, ProblemId = problemId, Result = result, InDate = inDate ?? DateTime.Now,
            SiteId = siteId, ContestId = contestId, Num = num, Language = language, Ip = "10.0.0.1", Time = time, Memory = memory, CodeLength = source?.Length ?? 0
        };
        Db.Solutions.Add(solution);
        if (source != null)
        {
            Db.SourceCodes.Add(new DbSourceCode { SolutionId = solution.SolutionId, Source = source });
        }

        if (customInput)
        {
            Db.CustomInputs.Add(new DbCustomInput { SolutionId = solution.SolutionId, ProblemId = problemId, UserId = userId, SiteId = siteId, CreatedAt = DateTime.Now });
        }

        Save();
        return solution.SolutionId;
    }

    public JudgeSeed Save()
    {
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return this;
    }

    public void Dispose()
    {
        _app.Dispose();
        _academic.Dispose();
    }
}
