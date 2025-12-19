using AlejanBros.Domain.Entities;

namespace AlejanBros.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id);
    Task<User?> GetByEmailAsync(string email);
    Task<User> CreateAsync(User user);
    Task<bool> EmailExistsAsync(string email);
}
