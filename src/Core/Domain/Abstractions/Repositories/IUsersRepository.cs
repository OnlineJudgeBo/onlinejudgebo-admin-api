using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories
{
    public interface IUsersRepository
    {
        public Task<IEnumerable<User>> GetAllUsersAsync();
        public Task<User> GetUserByIdAsync(string id);
        public Task<User> GetUserByEmailAsync(string email);
        public Task<User> CreateUserAsync(User user);
        public Task<IEnumerable<User>> GetUsersListByUsersIdListAsync(IEnumerable<string> usersId);
        public Task<User> UpdateUserAsync(User user);
        public Task<User> DeleteUserAsync(User user);
    }
}
