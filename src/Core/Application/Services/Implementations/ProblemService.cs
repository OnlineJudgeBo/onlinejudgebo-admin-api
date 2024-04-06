using FluentValidation;
using OnlineJudgeAdmin.Core.Domain.Abstractions.FileSystemManager;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class ProblemService : IProblemService
{
    private readonly IProblemRepository _problemRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IPrivilegeRepository _privilegeRepository;
    private readonly IFileSystemManager _fileSystemManager;
    private readonly IValidator<Problem> _userValidation;

    public ProblemService(
        IProblemRepository problemRepository,
        ITopicRepository topicRepository,
        IPrivilegeRepository privilegeRepository,
        IFileSystemManager fileSystemManager,
        IValidator<Problem> ProblemValidation)
    {
        _problemRepository = problemRepository ?? throw new ArgumentNullException(nameof(problemRepository));
        _topicRepository = topicRepository ?? throw new ArgumentNullException(nameof(topicRepository));
        _privilegeRepository = privilegeRepository ?? throw new ArgumentNullException(nameof(privilegeRepository));
        _fileSystemManager = fileSystemManager ?? throw new ArgumentNullException(nameof(fileSystemManager));
        _userValidation = ProblemValidation ?? throw new ArgumentNullException(nameof(ProblemValidation));
    }

    public async Task<IEnumerable<Problem>> GetAllProblemsAsync()
    {
        return await _problemRepository.GetAllProblemsAsync();
    }

    public async Task<Problem> GetProblemByIdAsync(int problemId)
    {
        return await _problemRepository.GetProblemByIdAsync(problemId);
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

        _fileSystemManager.CreateFolder(newProblem.ProblemId.ToString());
        _fileSystemManager.WriteToFile(newProblem.ProblemId.ToString(), "sample.in", newProblem.SampleInput);
        _fileSystemManager.WriteToFile(newProblem.ProblemId.ToString(), "sample.out", newProblem.SampleOutput);

        Privilege privilege = new Privilege();
        privilege.UserId = userId;
        privilege.Rightstr = "p" + newProblem.ProblemId;
        _privilegeRepository.CreatePrivilegeAsync(privilege);
        return newProblem;
    }

    public async Task<User> GetUserByIdAsync(string id)
    {
        throw new NotImplementedException();
        /*
        if (id == string.Empty)
        {
            throw new ArgumentNullException(nameof(id));
        }

        User user = await _problemRepository.GetUserByIdAsync(id);
        if (user == null)
        {
            throw new ArgumentException($"User with id {id} not found", id);
        }
        return user;*/
    }

    public async Task<Problem> UpdateUserAsync(string id, User user)
    {
        throw new NotImplementedException();

        /*
        //var existingUser = await _problemRepository.GetUserByIdAsync(id);
        var existingUser = null;
        ValidateUser(user);

        if (id == string.Empty)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (existingUser == null)
        {
            throw new ApplicationException("User does not exist.");
        }
        throw new NotImplementedException();
        //return await _problemRepository.UpdateUserAsync(user);*/
    }

    public async Task<User> DeleteUserAsync(string userId)
    {
        throw new NotImplementedException();
    }

    private void ValidateUser(User user)
    {
        /*ValidationResult result = _userValidation.Validate(user);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }*/
    }

    public async Task<User> UpdateCurrentRoomAsync(string userId, Guid roomId)
    {
        throw new NotImplementedException();
    }

    /*public Task<Problem> CreateProblemAsync(Problem problem)
    {
        throw new NotImplementedException();
    }*/

    public Task<Problem> EditProblemAsync(int problemId, Problem problem)
    {
        throw new NotImplementedException();
    }

    public Task<Problem> DeleteProblemAsync(int problemId)
    {
        throw new NotImplementedException();
    }
}

