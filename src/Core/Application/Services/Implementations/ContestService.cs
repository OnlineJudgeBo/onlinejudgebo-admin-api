using FluentValidation;

using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class ContestService : IContestService
{
    private readonly IContestsRepository _contestRepository;
    private readonly IPrivilegeRepository _privilegeRepository;

    private readonly IValidator<Problem> _userValidation;

    public ContestService(
        IContestsRepository topicRepository,
        IPrivilegeRepository privilegeRepository,
        IValidator<Problem> ProblemValidation)
    {
        _contestRepository = topicRepository ?? throw new ArgumentNullException(nameof(topicRepository));
        _privilegeRepository = privilegeRepository ?? throw new ArgumentException(nameof(privilegeRepository));
        _userValidation = ProblemValidation ?? throw new ArgumentNullException(nameof(ProblemValidation));
    }

    public async Task<IEnumerable<Contest>> GetAllContestAsync()
    {
        return await _contestRepository.GetAllContestsAsync();
    }

    public async Task<Contest> GetContestById(int contestId)
    {
        return await _contestRepository.GetContestByIdAsync(contestId);
    }

    public async Task<Contest> CreateContestAsync(string userIdCreator, Contest contest)
    {
        int numeration = 0;
        foreach (var problem in contest.ContestProblems)
        {
            problem.Num = numeration;
            numeration++;
        }

        contest.ContestUsers.Add(new ContestUser
        {
            UserId = userIdCreator
        });

        foreach (var contestUser in contest.ContestUsers)
        {
            contestUser.IsOwner = contestUser.UserId == userIdCreator;
        }

        Contest contestCreated = await _contestRepository.CreateContestAsync(contest);

        return await _contestRepository.GetContestByIdAsync(contestCreated.ContestId);
    }

    public async Task<Contest> UpdateContestAsync(int contestId, Contest contest)
    {
        var existingContest = await _contestRepository.GetContestByIdAsync(contestId);
        //ValidateUser(user);

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

        return await _contestRepository.UpdateContestAsync(contestId, contest);
    }
}
