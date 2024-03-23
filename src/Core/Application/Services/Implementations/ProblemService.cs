using FluentValidation;
using FluentValidation.Results;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class ProblemService : IProblemService
{
    private readonly IProblemRepository _problemRepository;
    private readonly IValidator<User> _userValidation;
    public ProblemService(
        IProblemRepository problemRepository,
        IValidator<User> userValidation)
    {
        _problemRepository = problemRepository ?? throw new ArgumentNullException(nameof(problemRepository));
        _userValidation = userValidation ?? throw new ArgumentNullException(nameof(userValidation));
    }

    public async Task<IEnumerable<Problem>> GetAllProblemsAsync()
    {
        return await _problemRepository.GetAllProblemsAsync();
    }

    public async Task<User> CreateUserAsync(User user)
    {
        throw new NotImplementedException();
        /*
        ValidateUser(user);

        User existingUser = await _problemRepository.GetUserByEmailAsync(user.Email);
        if (existingUser != null)
        {
            return existingUser;
        }

        return await _problemRepository.CreateUserAsync(user);*/
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
        ValidationResult result = _userValidation.Validate(user);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }

    public async Task<User> UpdateCurrentRoomAsync(string userId, Guid roomId)
    {
        throw new NotImplementedException();
    }

    public Task<Problem> GetProblemByIdAsync(int problemId)
    {
        throw new NotImplementedException();
    }

    public Task<Problem> CreateProblemAsync(Problem problem)
    {
        throw new NotImplementedException();
    }

    public Task<Problem> EditProblemAsync(int problemId, Problem problem)
    {
        throw new NotImplementedException();
    }

    public Task<Problem> DeleteProblemAsync(int problemId)
    {
        throw new NotImplementedException();
    }
}

