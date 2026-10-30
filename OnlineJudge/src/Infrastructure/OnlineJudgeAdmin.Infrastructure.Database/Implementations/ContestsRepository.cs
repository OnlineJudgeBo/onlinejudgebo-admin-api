using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class ContestsRepository : IContestsRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public ContestsRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<IEnumerable<Contest>> GetContestsByUserIdDocenteRoleAsync(string userId, bool showAllContest, int siteId, bool includePromoted = false, bool includeDeleted = false)
    {
        IQueryable<DbContest> query;

        if (showAllContest)
        {
            query = _context.Contests
                .Where(c => c.ContestSites.Any(site => site.SiteId == siteId));
        }
        else
        {
            var contestIds = await _context.ContestUsers
                .Where(user => user.UserId == userId)
                .Select(cu => cu.ContestId)
                .ToListAsync();

            query = _context.Contests
                .Where(c => c.ContestSites.Any(cs => cs.SiteId == siteId))
                .Where(c => contestIds.Contains(c.ContestId));
        }

        if (!includePromoted)
        {
            query = query.Where(c => c.Defunct != DefunctStates.Official);
        }

        // Soft-deleted contests only show up in the "deleted" view.
        query = includeDeleted ? query.Where(c => c.Defunct == DefunctStates.Deleted) : query.Where(c => c.Defunct != DefunctStates.Deleted);

        var contests = await query
            .OrderByDescending(c => c.ContestId)
            .Select(c => new DbContest
            {
                ContestId = c.ContestId,
                Title = c.Title,
                Private = c.Private,
                StartTime = c.StartTime,
                EndTime = c.EndTime,
                Defunct = c.Defunct,
                Track = c.Track,
                Level = c.Level,
                IsExam = c.IsExam
            })
            .ToListAsync();
        return _mapper.Map<IEnumerable<Contest>>(contests);
    }

    public async Task<IEnumerable<Contest>> GetContestsByAuxiliarRoleAsync(string userId, int siteId, bool includePromoted = false, bool includeDeleted = false)
    {
        IQueryable<DbContest> query = _context.Contests
            .Where(c => c.ContestSites.Any(site => site.SiteId == siteId));

        if (!includePromoted)
        {
            query = query.Where(c => c.Defunct != DefunctStates.Official);
        }

        // Soft-deleted contests only show up in the "deleted" view.
        query = includeDeleted ? query.Where(c => c.Defunct == DefunctStates.Deleted) : query.Where(c => c.Defunct != DefunctStates.Deleted);

        var contests = await query
            .OrderByDescending(c => c.ContestId)
            .Select(c => new DbContest
            {
                ContestId = c.ContestId,
                Title = c.Title,
                Private = c.Private,
                StartTime = c.StartTime,
                EndTime = c.EndTime,
                Defunct = c.Defunct,
                Track = c.Track,
                Level = c.Level,
                IsExam = c.IsExam
            })
            .Take(100)
            .ToListAsync();
        return _mapper.Map<IEnumerable<Contest>>(contests);
    }

    public async Task<Contest> GetContestByIdAsync(int contestId, int siteId)
    {
        DbContest contest = await _context.Contests
        .Where(c => c.ContestId == contestId && c.ContestSites.Any(cs => cs.SiteId == siteId))
        .Select(c => new DbContest
        {
            ContestId = c.ContestId,
            Title = c.Title,
            Description = c.Description,
            Private = c.Private,
            StartTime = c.StartTime,
            EndTime = c.EndTime,
            Defunct = c.Defunct,
            Langmask = c.Langmask,
            Track = c.Track,
            Level = c.Level,
            IsExam = c.IsExam,
            ExamLabIps = c.ExamLabIps,
            Obi = c.Obi,
            ProgrammingLanguages = c.ProgrammingLanguages.Select(c => new DbProgrammingLanguage
            {
                LanguageId = c.LanguageId,
                Name = c.Name
            }).ToList(),
            ContestProblems = c.ContestProblems.Select(c => new DbContestProblem
            {
                Num = c.Num,
                ProblemId = c.ProblemId,
            })
            .OrderBy(cp => cp.Num)
            .ToList(),
            ContestUsers = c.ContestUsers.Select(cu => new DbContestUser
            {
                ContestId = cu.ContestId,
                UserId = cu.UserId,
                IsOwner = cu.IsOwner,
            })
            .OrderBy(cp => cp.UserId)
            .ToList()
        })
        .FirstOrDefaultAsync();
        return _mapper.Map<Contest>(contest);
    }

    public async Task<Contest> CreateContestAsync(Contest contest, int siteId)
    {
        DbContest newContest = _mapper.Map<DbContest>(contest);

        var dbContestProblems = new List<DbContestProblem>(newContest.ContestProblems);
        var dbContestUsers = new List<DbContestUser>(newContest.ContestUsers);
        var dbProgrammingLanguages = new List<DbProgrammingLanguage>(newContest.ProgrammingLanguages);

        newContest.ContestProblems.Clear();
        newContest.ContestUsers.Clear();
        newContest.ProgrammingLanguages.Clear();

        _context.Contests.Add(newContest);

        await _context.SaveChangesAsync();
        await _context.Contests.FirstOrDefaultAsync(c => c.ContestId == newContest.ContestId);

        foreach (var problem in dbContestProblems)
        {
            var contestProblem = new DbContestProblem
            {
                ContestId = newContest.ContestId,
                ProblemId = problem.ProblemId,
                Num = problem.Num
            };
            _context.ContestProblems.Add(contestProblem);
        }

        foreach (var user in dbContestUsers)
        {
            if (user.UserId != null)
            {

                var contestUser = new DbContestUser
                {
                    ContestId = newContest.ContestId,
                    UserId = user.UserId,
                    SiteId = siteId,
                    IsOwner = user.IsOwner
                };
                _context.ContestUsers.Add(contestUser);
            }
        }

        foreach (var lang in dbProgrammingLanguages)
        {
            var language = await _context.ProgrammingLanguages.FindAsync(lang.LanguageId);
            if (language != null)
            {
                newContest.ProgrammingLanguages.Add(language);
            }
        }

        var newContestSite = new DbContestSite
        {
            ContestId = newContest.ContestId,
            SiteId = siteId
        };

        _context.ContestSites.Add(newContestSite);

        await _context.SaveChangesAsync();
        return await GetContestByIdAsync(newContest.ContestId, siteId);
    }

    public async Task<Contest> UpdateContestAsync(int contestId, Contest contest, int siteId)
    {
        DbContest contestsToUpdate = _mapper.Map<DbContest>(contest);

        var existingContest = await _context.Contests
            .Include(c => c.ContestProblems)
            .Include(c => c.ContestUsers)
            .Include(c => c.ProgrammingLanguages)
            .Where(c => c.ContestSites.Any(cs => cs.SiteId == siteId))
            .FirstOrDefaultAsync(c => c.ContestId == contestId);

        if (existingContest == null)
            throw new KeyNotFoundException("No se encontró el concurso con ID: " + contestId);

        var ownerContest = existingContest.ContestUsers.FirstOrDefault(p => p.IsOwner && p.SiteId == siteId)
            ?? throw new KeyNotFoundException("No se encontró el concurso con ID: " + contestId);

        var previousDefunct = existingContest.Defunct;
        _context.Entry(existingContest).CurrentValues.SetValues(contest);
        // Editing never revives a deleted contest: only Restore does.
        if (previousDefunct == DefunctStates.Deleted)
            existingContest.Defunct = DefunctStates.Deleted;

        // Submissions and clarifications point at a problem by its letter (num): they follow it when the order changes.
        var oldNums = existingContest.ContestProblems.Where(p => p.Num.HasValue).ToDictionary(p => p.ProblemId, p => p.Num!.Value);
        var newNums = contestsToUpdate.ContestProblems.Where(p => p.Num.HasValue).ToDictionary(p => p.ProblemId, p => p.Num!.Value);
        var moved = newNums.Where(item => oldNums.TryGetValue(item.Key, out var old) && old != item.Value).ToList();

        existingContest.ContestProblems.Clear();
        foreach (var problem in contestsToUpdate.ContestProblems)
        {
            existingContest.ContestProblems.Add(problem);
        }

        existingContest.ContestUsers.Clear();
        existingContest.ContestUsers.Add(ownerContest);
        foreach (var user in contestsToUpdate.ContestUsers)
        {
            if (user.UserId != ownerContest.UserId)
            {
                existingContest.ContestUsers.Add(user);
            }
        }

        existingContest.ProgrammingLanguages.Clear();
        foreach (var langId in contestsToUpdate.ProgrammingLanguages)
        {
            var language = await _context.ProgrammingLanguages.FindAsync(langId.LanguageId);
            if (language != null)
            {
                _context.Entry(language).State = EntityState.Unchanged;
                existingContest.ProgrammingLanguages.Add(language);
            }
        }

        if (moved.Count == 0)
        {
            await _context.SaveChangesAsync();
            return await GetContestByIdAsync(contestId, siteId);
        }

        await using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            await _context.SaveChangesAsync();
            foreach (var (problemId, num) in moved)
            {
                await _context.Solutions
                    .Where(solution => solution.ContestId == contestId && solution.ProblemId == problemId)
                    .ExecuteUpdateAsync(set => set.SetProperty(solution => solution.Num, num));
            }

            var newNumByOld = moved.ToDictionary(item => oldNums[item.Key], item => item.Value);
            var clarifications = await _context.ContestClarifications
                .Where(item => item.ContestId == contestId && item.ProblemNum.HasValue)
                .ToListAsync();
            foreach (var clarification in clarifications)
            {
                if (newNumByOld.TryGetValue(clarification.ProblemNum!.Value, out var num))
                {
                    clarification.ProblemNum = num;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        return await GetContestByIdAsync(contestId, siteId);
    }

    public async Task<Contest> PromoteContestAsync(int contestId, int siteId)
    {
        await SetContestDefunctAsync(contestId, siteId, DefunctStates.Official);
        return await GetContestByIdAsync(contestId, siteId);
    }

    public async Task SetContestDefunctAsync(int contestId, int siteId, string defunct)
    {
        var existingContest = await _context.Contests
            .Where(c => c.ContestSites.Any(cs => cs.SiteId == siteId))
            .FirstOrDefaultAsync(c => c.ContestId == contestId);

        if (existingContest == null)
            throw new KeyNotFoundException("No se encontró el concurso con ID: " + contestId);

        existingContest.Defunct = defunct;
        await _context.SaveChangesAsync();
    }

    // The exam a participant is taking right now; when two overlap, the one that started last wins.
    public async Task<Contest?> GetActiveExamForUserAsync(string userId, int siteId, DateTime now)
    {
        var contest = await _context.Contests
            .Where(c => c.IsExam && c.Defunct == DefunctStates.Visible && c.StartTime <= now && c.EndTime >= now
                && c.ContestSites.Any(site => site.SiteId == siteId)
                && c.ContestUsers.Any(user => user.UserId == userId && user.SiteId == siteId && !user.IsOwner))
            .OrderByDescending(c => c.StartTime)
            .Select(c => new DbContest
            {
                ContestId = c.ContestId,
                Title = c.Title,
                StartTime = c.StartTime,
                EndTime = c.EndTime,
                Defunct = c.Defunct,
                IsExam = c.IsExam,
            })
            .FirstOrDefaultAsync();
        return contest == null ? null : _mapper.Map<Contest>(contest);
    }

    public async Task<ExamActivity> GetExamActivityAsync(int contestId, int siteId, DateTime start, DateTime end)
    {
        var participantIds = await _context.ContestUsers
            .Where(user => user.ContestId == contestId && user.SiteId == siteId && !user.IsOwner)
            .Select(user => user.UserId)
            .ToListAsync();

        // Every run counts as activity, custom-input runs included.
        var submissions = await _context.Solutions
            .Where(solution => solution.ContestId == contestId && solution.SiteId == siteId
                && solution.InDate >= start && solution.InDate <= end)
            .Select(solution => new { solution.UserId, solution.Ip, solution.InDate })
            .ToListAsync();

        var userIds = participantIds.Concat(submissions.Select(item => item.UserId)).Distinct().ToList();

        var logins = await _context.Loginlogs
            .Where(login => userIds.Contains(login.UserId) && login.SiteId == siteId
                && login.Time >= start.AddHours(-1) && login.Time <= end)
            .Select(login => new { login.UserId, login.Ip, login.Time })
            .ToListAsync();

        var nicks = await _context.UserProfiles
            .Where(profile => profile.SiteId == siteId && userIds.Contains(profile.UserId))
            .ToDictionaryAsync(profile => profile.UserId, profile => profile.Nick);

        return new ExamActivity
        {
            ParticipantUserIds = userIds,
            Nicks = nicks,
            Events = submissions
                .Select(item => new ExamActivityEvent(item.UserId, (item.Ip ?? string.Empty).Trim(), item.InDate, ExamActivitySources.Submission))
                .Concat(logins.Select(item => new ExamActivityEvent(item.UserId, (item.Ip ?? string.Empty).Trim(), item.Time!.Value, ExamActivitySources.Login)))
                .ToList()
        };
    }

    public async Task<IReadOnlyCollection<ContestSimilarityItem>> GetSimilarityAsync(int contestId, int siteId)
    {
        return await (
            from similar in _context.SimilarCodes
            join solution in _context.Solutions on similar.SolutionId equals solution.SolutionId
            join other in _context.Solutions on similar.SimilarSId equals other.SolutionId
            // Rows written before the kernel left out own submissions can pair a user with themselves.
            where solution.ContestId == contestId && solution.SiteId == siteId && solution.UserId != other.UserId
            select new ContestSimilarityItem(
                solution.ProblemId, solution.Num, solution.SolutionId, solution.UserId,
                other.SolutionId, other.UserId, similar.Percentage)
        ).ToListAsync();
    }

    public async Task<IReadOnlyCollection<ContestSubmissionExportItem>> GetSubmissionsForExportAsync(int contestId, int siteId)
    {
        return await (
            from solution in _context.Solutions.AsNoTracking()
            where solution.ContestId == contestId && solution.SiteId == siteId
            orderby solution.SolutionId
            select new ContestSubmissionExportItem(
                solution.SolutionId,
                solution.UserId,
                _context.UserProfiles.Where(profile => profile.UserId == solution.UserId && profile.SiteId == siteId)
                    .Select(profile => profile.Nick).FirstOrDefault() ?? solution.UserId,
                solution.Num,
                solution.ProblemId,
                _context.ProgrammingLanguages.Where(language => language.LanguageId == (int)solution.Language)
                    .Select(language => language.Name).FirstOrDefault() ?? string.Empty,
                solution.Result,
                solution.PassRate,
                solution.Time,
                solution.Memory,
                solution.Ip,
                solution.InDate,
                solution.SourceCode != null ? solution.SourceCode.Source : string.Empty)
        ).ToListAsync();
    }
}
