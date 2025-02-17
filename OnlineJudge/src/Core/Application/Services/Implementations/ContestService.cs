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
    private static readonly string[] separator = new string[] { "," };

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

    public async Task<IEnumerable<Contest>> GetAllContestAsync(CurrentUser userContextRole)
    {
        bool showAllContest = false;
        if (userContextRole.Role == UserRolesEnum.Administrador)
        {
            showAllContest = true;
            return await _contestRepository.GetContestsByUserIdDocenteRoleAsync(userContextRole.UserId, showAllContest, userContextRole.SiteId);
        }

        if (userContextRole.Role == UserRolesEnum.Auxiliar)
        {
            return await _contestRepository.GetContestsByAuxiliarRoleAsync(userContextRole.UserId, userContextRole.SiteId);
        }

        return await _contestRepository.GetContestsByUserIdDocenteRoleAsync(userContextRole.UserId, showAllContest, userContextRole.SiteId);
    }

    public async Task<Contest> GetContestById(int contestId)
    {
        return await _contestRepository.GetContestByIdAsync(contestId);
    }

    public async Task<Contest> CreateContestAsync(string userIdCreator, Contest contest, string manualUserList, int siteId)
    {
        int numeration = 0;
        foreach (var problem in contest.ContestProblems)
        {
            problem.Num = numeration++;
        }

        HashSet<string> uniqueUserIds = new HashSet<string>();
        uniqueUserIds.Add(userIdCreator);
        foreach (var existingUser in contest.ContestUsers)
        {
            uniqueUserIds.Add(existingUser.UserId);
        }

        string[] users = manualUserList.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var userId in users)
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
        return await _contestRepository.GetContestByIdAsync(contestCreated.ContestId);
    }

    public async Task<Contest> UpdateContestAsync(int contestId, Contest contest, string manualUserList, int siteId)
    {
        var existingContest = await _contestRepository.GetContestByIdAsync(contestId);

        if (contestId == 0)
        {
            throw new ArgumentNullException(nameof(contestId));
        }

        if (existingContest == null)
        {
            throw new ApplicationException("Contest does not exist.");
        }

        int numeration = 0;
        foreach (var problem in contest.ContestProblems)
        {
            problem.Num = numeration;
            numeration++;
        }

        string[] users = manualUserList.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        HashSet<string> uniqueUserIds = new HashSet<string>();
        /*foreach (var existingUser in contest.ContestUsers)
        {
            uniqueUserIds.Add(existingUser.UserId);
        }*/

        foreach (var userId in users)
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
        var existingContest = await _contestRepository.GetContestByIdAsync(contestId);

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
}
