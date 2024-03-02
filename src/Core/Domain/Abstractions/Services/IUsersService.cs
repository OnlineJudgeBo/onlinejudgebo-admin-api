using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services
{
    public interface IUsersService
    {
        public Task<IEnumerable<User>> GetAllUsersAsync();
        public Task<User> GetUserByIdAsync(string id);

        public Task<User> CreateUserAsync(User user);

        public Task<User> UpdateUserAsync(string id, User user);

        public Task<User> DeleteUserAsync(string userId);

        public Task<User> UpdateCurrentRoomAsync(string userId, Guid roomId);
    }
}
