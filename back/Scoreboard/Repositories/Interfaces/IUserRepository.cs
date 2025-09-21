using Scoreboard.Models.Entities;

namespace Scoreboard.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetUserWithRoleAsync(string username, string password);
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByIdWithRoleAsync(int id);   // 👈 agrega esto
        Task AddAsync(User user);
    }
}
