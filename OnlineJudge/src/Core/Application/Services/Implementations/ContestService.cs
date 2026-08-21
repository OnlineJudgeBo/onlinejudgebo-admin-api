using FluentValidation;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class ContestService : IContestService
{
    private readonly IContestsRepository _contestRepository;
    private readonly IProblemRepository _problemRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPrivilegeRepository _privilegeRepository;
    private readonly IValidator<Problem> _userValidation;
    private static readonly string[] UserListSeparators = new[] { ",", "\r\n", "\n", "\r" };

    public ContestService(
        IContestsRepository topicRepository,
        IProblemRepository problemRepository,
        IPrivilegeRepository privilegeRepository,
        IUserRepository userRepository,
        IValidator<Problem> ProblemValidation)
    {
        _contestRepository = topicRepository ?? throw new ArgumentNullException(nameof(topicRepository));
        _problemRepository = problemRepository ?? throw new ArgumentNullException(nameof(problemRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _privilegeRepository = privilegeRepository ?? throw new ArgumentException(null, nameof(privilegeRepository));
        _userValidation = ProblemValidation ?? throw new ArgumentNullException(nameof(ProblemValidation));
    }

    public async Task<IEnumerable<Contest>> GetAllContestAsync(CurrentUser userContextRole, bool includePromoted = false)
    {
        if (userContextRole.Role is UserRolesEnum.Administrador or UserRolesEnum.Docente)
        {
            return await _contestRepository.GetContestsByUserIdDocenteRoleAsync(userContextRole.UserId, showAllContest: true, userContextRole.SiteId, includePromoted);
        }

        if (userContextRole.Role == UserRolesEnum.Auxiliar)
        {
            return await _contestRepository.GetContestsByAuxiliarRoleAsync(userContextRole.UserId, userContextRole.SiteId, includePromoted);
        }

        return await _contestRepository.GetContestsByUserIdDocenteRoleAsync(userContextRole.UserId, showAllContest: false, userContextRole.SiteId, includePromoted);
    }

    public async Task<Contest> GetContestById(int contestId, int siteId)
    {
        return await _contestRepository.GetContestByIdAsync(contestId, siteId);
    }

    public async Task<Contest> CreateContestAsync(string userIdCreator, Contest contest, string manualUserList, int siteId)
    {
        EnsureContestMetadata(contest);
        int numeration = 0;
        foreach (var problem in contest.ContestProblems)
        {
            problem.Num = numeration++;
        }

        contest.ContestUsers ??= new List<ContestUser>();

        HashSet<string> uniqueUserIds = new HashSet<string>();
        uniqueUserIds.Add(userIdCreator);
        foreach (var existingUser in contest.ContestUsers)
        {
            uniqueUserIds.Add(existingUser.UserId);
        }

        foreach (var userId in ParseManualUserList(manualUserList))
        {
            uniqueUserIds.Add(userId);
        }

        contest.ContestUsers.Clear();
        foreach (var userId in uniqueUserIds)
        {
            if (await _userRepository.GetUserById(userId, siteId) != null)
            {
                contest.ContestUsers.Add(new ContestUser
                {
                    UserId = userId,
                    IsOwner = userId == userIdCreator
                });
            }
        }

        Contest contestCreated = await _contestRepository.CreateContestAsync(contest, siteId);
        return await _contestRepository.GetContestByIdAsync(contestCreated.ContestId, siteId);
    }

    public async Task<Contest> UpdateContestAsync(int contestId, Contest contest, string manualUserList, int siteId)
    {
        var existingContest = await _contestRepository.GetContestByIdAsync(contestId, siteId);

        if (contestId == 0)
        {
            throw new ArgumentNullException(nameof(contestId));
        }

        if (existingContest == null)
        {
            throw new ApplicationException("Contest does not exist.");
        }

        EnsureContestMetadata(contest);
        int numeration = 0;
        foreach (var problem in contest.ContestProblems)
        {
            problem.Num = numeration;
            numeration++;
        }

        contest.ContestUsers ??= new List<ContestUser>();

        HashSet<string> uniqueUserIds = new HashSet<string>();

        foreach (var userId in ParseManualUserList(manualUserList))
        {
            uniqueUserIds.Add(userId);
        }

        contest.ContestUsers.Clear();
        foreach (var userId in uniqueUserIds)
        {
            if (await _userRepository.GetUserById(userId, siteId) != null)
            {
                contest.ContestUsers.Add(new ContestUser
                {
                    UserId = userId,
                    IsOwner = false,
                    SiteId = siteId,
                });
            }
        }

        return await _contestRepository.UpdateContestAsync(contestId, contest, siteId);
    }

    public async Task PromoteContestAsync(int contestId, int siteId)
    {
        var existingContest = await _contestRepository.GetContestByIdAsync(contestId, siteId);

        if (contestId == 0)
        {
            throw new ArgumentNullException(nameof(contestId));
        }

        if (existingContest == null)
        {
            throw new ApplicationException("Contest does not exist.");
        }
        List<int> problemIdList = new List<int>();
        foreach (var problem in existingContest.ContestProblems)
        {
            problemIdList.Add(problem.ProblemId ?? 0);
        }
        await _contestRepository.PromoteContestAsync(contestId, siteId);
        await _problemRepository.PromoteProblemAsync(problemIdList);
    }

    private static IEnumerable<string> ParseManualUserList(string? manualUserList)
    {
        return (manualUserList ?? string.Empty)
            .Split(UserListSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(userId => userId.Trim())
            .Where(userId => !string.IsNullOrWhiteSpace(userId));
    }

    private static void EnsureContestMetadata(Contest contest)
    {
        contest.Track = string.IsNullOrWhiteSpace(contest.Track) ? "GENERAL" : contest.Track;
        contest.Level = string.IsNullOrWhiteSpace(contest.Level) ? "PRACTICE" : contest.Level;

        if (contest.Track != "OBI"
            && contest.Track != "ICPC_BOLIVIA"
            && contest.Track != "GENERAL")
        {
            throw new ArgumentException("Contest track must be OBI, ICPC_BOLIVIA or GENERAL.");
        }

        if (contest.Level != "REGIONAL"
            && contest.Level != "NATIONAL"
            && contest.Level != "PRACTICE"
            && contest.Level != "TRAINING")
        {
            throw new ArgumentException("Contest level must be REGIONAL, NATIONAL, PRACTICE or TRAINING.");
        }
    }
}
