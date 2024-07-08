using FluentValidation;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class ProblemService : IProblemService
{
    private readonly IProblemRepository _problemRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IPrivilegeRepository _privilegeRepository;
    private readonly IFileSystemLocalManagerManager _FileSystemLocalManagerManager;
    private readonly IValidator<Problem> _userValidation;

    public ProblemService(
        IProblemRepository problemRepository,
        ITopicRepository topicRepository,
        IPrivilegeRepository privilegeRepository,
        IFileSystemLocalManagerManager FileSystemLocalManagerManager,
        IValidator<Problem> ProblemValidation)
    {
        _problemRepository = problemRepository ?? throw new ArgumentNullException(nameof(problemRepository));
        _topicRepository = topicRepository ?? throw new ArgumentNullException(nameof(topicRepository));
        _privilegeRepository = privilegeRepository ?? throw new ArgumentNullException(nameof(privilegeRepository));
        _FileSystemLocalManagerManager = FileSystemLocalManagerManager ?? throw new ArgumentNullException(nameof(FileSystemLocalManagerManager));
        _userValidation = ProblemValidation ?? throw new ArgumentNullException(nameof(ProblemValidation));
    }

    public async Task<IEnumerable<Problem>> GetAllProblemsAsync(CurrentUser currentUser)
    {
        if (currentUser.Role == UserRolesEnum.Administrador)
        {
            return await _problemRepository.GetAllProblemsForAdminAsync();
        }
        else
        {
            return await _problemRepository.GetAllProblemsAsync();
        }
    }

    public async Task<Problem> GetProblemByIdAsync(int problemId)
    {
        return await _problemRepository.GetProblemByIdAsync(problemId);
    }

    public async Task<IEnumerable<Problem>> SearchProblemAsync(CurrentUser currentUser, string searchTerm)
    {
        if (currentUser.Role == UserRolesEnum.Administrador)
        {
            return await _problemRepository.SearchProblemForAdminAsync(searchTerm);
        }
        else
        {
            return await _problemRepository.SearchProblemAsync(searchTerm);
        }
    }

    public async Task<Problem> CreateProblemAsync(string userId, Problem problem)
    {
        IEnumerable<Classification>? newTopic = problem.Classifications;
        problem.Classifications = null;

        Problem newProblem = await _problemRepository.CreateProblemAsync(problem);
        if (newTopic != null)
        {
            await _topicRepository.AddClassificationsToProblemAsync(newProblem.ProblemId.Value, newTopic);
        }

        _FileSystemLocalManagerManager.CreateFolder(newProblem.ProblemId.Value.ToString());
        _FileSystemLocalManagerManager.WriteToFile(newProblem.ProblemId.Value.ToString(), "sample.in", problem.SampleInput);
        _FileSystemLocalManagerManager.WriteToFile(newProblem.ProblemId.Value.ToString(), "sample.out", problem.SampleOutput);

        Privilege privilege = new Privilege();
        privilege.UserId = userId;
        privilege.Rightstr = "p" + newProblem.ProblemId;
        _privilegeRepository.CreatePrivilegeAsync(privilege);
        return newProblem;
    }

    public async Task<Problem> UpdateProblemAsync(string userId, int problemId, Problem problem)
    {
        var existingProblem = await _problemRepository.GetProblemByIdAsync(problemId);
        //ValidateUser(user);

        if (userId == string.Empty)
        {
            throw new ArgumentNullException(nameof(userId));
        }

        if (existingProblem == null)
        {
            throw new ApplicationException("Problem does not exist.");
        }

        await _topicRepository.RemoveAllClassificationsFromProblemAsync(existingProblem.ProblemId.Value);
        IEnumerable<Classification>? newTopic = problem.Classifications;
        problem.Classifications = null;

        var updateProblem = await _problemRepository.UpdateProblemAsync(userId, problemId, problem);
        if (updateProblem != null)
        {
            await _topicRepository.AddClassificationsToProblemAsync(updateProblem.ProblemId.Value, newTopic);
        }

        _FileSystemLocalManagerManager.CreateFolder(updateProblem.ProblemId.Value.ToString());
        _FileSystemLocalManagerManager.CreateFolder(updateProblem.ProblemId.Value.ToString() + "/ac");
        _FileSystemLocalManagerManager.WriteToFile(updateProblem.ProblemId.Value.ToString(), "sample.in", updateProblem.SampleInput);
        _FileSystemLocalManagerManager.WriteToFile(updateProblem.ProblemId.Value.ToString(), "sample.out", updateProblem.SampleOutput);

        return updateProblem;
    }

    public async Task DeleteProblemAsync(int problemId)
    {
        await _problemRepository.DeleteProblemAsync(problemId);
    }
}

