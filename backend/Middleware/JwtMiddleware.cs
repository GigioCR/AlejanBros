using AlejanBros.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Claims;

namespace AlejanBros.Middleware;

public class JwtMiddleware : IFunctionsWorkerMiddleware
{
    private readonly IAuthService _authService;
    private readonly ILogger<JwtMiddleware> _logger;

    private static readonly HashSet<string> PublicRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/register",
        "/api/auth/login",
        "/api/swagger",
        "/api/openapi",
    };

    public JwtMiddleware(IAuthService authService, ILogger<JwtMiddleware> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var requestData = await context.GetHttpRequestDataAsync();
        
        if (requestData == null)
        {
            await next(context);
            return;
        }

        var path = requestData.Url.AbsolutePath;

        if (IsPublicRoute(path))
        {
            await next(context);
            return;
        }

        var authHeader = requestData.Headers.TryGetValues("Authorization", out var values) 
            ? values.FirstOrDefault() 
            : null;

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            await WriteUnauthorizedResponse(context, requestData, "Authorization header missing or invalid");
            return;
        }

        var token = authHeader.Substring("Bearer ".Length);
        var principal = _authService.ValidateToken(token);

        if (principal == null)
        {
            await WriteUnauthorizedResponse(context, requestData, "Token expired or invalid", "TOKEN_EXPIRED");
            return;
        }

        context.Items["User"] = principal;
        context.Items["UserId"] = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        context.Items["UserEmail"] = principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        context.Items["UserRole"] = principal.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        await next(context);
    }

    private static bool IsPublicRoute(string path)
    {
        foreach (var route in PublicRoutes)
        {
            if (path.StartsWith(route, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static async Task WriteUnauthorizedResponse(
        FunctionContext context, 
        HttpRequestData requestData, 
        string message,
        string? code = null)
    {
        var response = requestData.CreateResponse(HttpStatusCode.Unauthorized);
        response.Headers.Add("Content-Type", "application/json");
        
        var body = code != null 
            ? $"{{\"message\":\"{message}\",\"code\":\"{code}\"}}"
            : $"{{\"message\":\"{message}\"}}";
            
        await response.WriteStringAsync(body);
        
        context.GetInvocationResult().Value = response;
    }
}
