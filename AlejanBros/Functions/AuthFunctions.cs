using AlejanBros.Models;
using AlejanBros.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;

namespace AlejanBros.Functions;

public class AuthFunctions
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthFunctions> _logger;

    public AuthFunctions(IAuthService authService, ILogger<AuthFunctions> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [Function("Register")]
    [OpenApiOperation(operationId: "Register", tags: new[] { "Auth" })]
    [OpenApiRequestBody("application/json", typeof(RegisterRequest), Description = "Registration details")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AuthResponse), Description = "Registration result")]
    public async Task<IActionResult> Register(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/register")] HttpRequest req)
    {
        try
        {
            var request = await req.ReadFromJsonAsync<RegisterRequest>();
            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return new BadRequestObjectResult(new AuthResponse
                {
                    Success = false,
                    Message = "Email and password are required"
                });
            }

            if (request.Password.Length < 6)
            {
                return new BadRequestObjectResult(new AuthResponse
                {
                    Success = false,
                    Message = "Password must be at least 6 characters"
                });
            }

            var result = await _authService.RegisterAsync(request);

            if (!result.Success)
            {
                return new ConflictObjectResult(result);
            }

            return new OkObjectResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Register function");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [Function("Login")]
    [OpenApiOperation(operationId: "Login", tags: new[] { "Auth" })]
    [OpenApiRequestBody("application/json", typeof(LoginRequest), Description = "Login credentials")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AuthResponse), Description = "Login result")]
    public async Task<IActionResult> Login(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/login")] HttpRequest req)
    {
        try
        {
            var request = await req.ReadFromJsonAsync<LoginRequest>();
            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return new BadRequestObjectResult(new AuthResponse
                {
                    Success = false,
                    Message = "Email and password are required"
                });
            }

            var result = await _authService.LoginAsync(request);

            if (!result.Success)
            {
                return new UnauthorizedObjectResult(result);
            }

            return new OkObjectResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Login function");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [Function("GetCurrentUser")]
    [OpenApiOperation(operationId: "GetCurrentUser", tags: new[] { "Auth" })]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UserInfo), Description = "Current user info")]
    public async Task<IActionResult> GetCurrentUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/me")] HttpRequest req)
    {
        try
        {
            var authHeader = req.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return new UnauthorizedObjectResult(new { message = "Authorization header missing or invalid" });
            }

            var token = authHeader.Substring("Bearer ".Length);
            var principal = _authService.ValidateToken(token);

            if (principal == null)
            {
                return new UnauthorizedObjectResult(new { message = "Token expired or invalid", code = "TOKEN_EXPIRED" });
            }

            var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return new UnauthorizedObjectResult(new { message = "Invalid token" });
            }

            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return new NotFoundObjectResult(new { message = "User not found" });
            }

            return new OkObjectResult(new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                Role = user.Role
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetCurrentUser function");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}
