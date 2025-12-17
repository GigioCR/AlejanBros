using AlejanBros.Models;
using System.Security.Claims;

namespace AlejanBros.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<Models.User?> GetUserByEmailAsync(string email);
    Task<Models.User?> GetUserByIdAsync(string id);
    ClaimsPrincipal? ValidateToken(string token);
}
