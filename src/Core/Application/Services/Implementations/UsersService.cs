using FluentValidation;
using FluentValidation.Results;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations
{
    public class UsersService : IUsersService
    {
        private readonly IUsersRepository _usersRepository;
        private readonly IValidator<User> _userValidation;
        public UsersService(
            IUsersRepository usersRepository,
            IValidator<User> userValidation)
        {
            _usersRepository = usersRepository ?? throw new ArgumentNullException(nameof(usersRepository));
            _userValidation = userValidation ?? throw new ArgumentNullException(nameof(userValidation));
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return await _usersRepository.GetAllUsersAsync();
        }

        public async Task<User> CreateUserAsync(User user)
        {
            ValidateUser(user);

            User existingUser = await _usersRepository.GetUserByEmailAsync(user.Email);
            if (existingUser != null)
            {
                return existingUser;
            }

            return await _usersRepository.CreateUserAsync(user);
        }

        public async Task<User> GetUserByIdAsync(string id)
        {
            if (id == string.Empty)
            {
                throw new ArgumentNullException(nameof(id));
            }

            User user = await _usersRepository.GetUserByIdAsync(id);
            if (user == null)
            {
                throw new ArgumentException($"User with id {id} not found", id);
            }
            return user;
        }

        public async Task<User> UpdateUserAsync(string id, User user)
        {
            ValidateUser(user);

            var existingUser = await _usersRepository.GetUserByIdAsync(id);

            if (id == string.Empty)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (existingUser == null)
            {
                throw new ApplicationException("User does not exist.");
            }

            return await _usersRepository.UpdateUserAsync(user);
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
    }
}
