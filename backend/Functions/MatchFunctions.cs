using AlejanBros.Models;
using AlejanBros.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Text.Json;

namespace AlejanBros.Functions;

public class MatchFunctions
{
    private readonly IEmployeeMatcherService _matcherService;
    private readonly IOpenAIService _openAIService;
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<MatchFunctions> _logger;

    public MatchFunctions(
        IEmployeeMatcherService matcherService,
        IOpenAIService openAIService,
        ICosmosDbService cosmosDbService,
        ILogger<MatchFunctions> logger)
    {
        _matcherService = matcherService;
        _openAIService = openAIService;
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    [Function("FindMatches")]
    [OpenApiOperation(operationId: "FindMatches", tags: new[] { "Matching" }, Summary = "Find matching employees", Description = "Uses AI to find and rank employees matching the specified requirements")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(MatchRequest), Required = true, Description = "Match criteria")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(MatchResponse), Description = "Match results")]
    public async Task<IActionResult> FindMatches(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "match")] HttpRequest req)
    {
        _logger.LogInformation("Processing match request");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var matchRequest = JsonSerializer.Deserialize<MatchRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (matchRequest == null || string.IsNullOrWhiteSpace(matchRequest.Query))
            {
                return new BadRequestObjectResult(new { message = "Query is required" });
            }

            var result = await _matcherService.FindMatchesAsync(matchRequest);
            return new OkObjectResult(result);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in request body");
            return new BadRequestObjectResult(new { message = "Invalid JSON format" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing match request");
            return new StatusCodeResult(500);
        }
    }

    [Function("FindMatchesForProject")]
    [OpenApiOperation(operationId: "FindMatchesForProject", tags: new[] { "Matching" }, Summary = "Find matches for a project", Description = "Finds employees matching a specific project's requirements")]
    [OpenApiParameter(name: "projectId", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Project ID")]
    [OpenApiParameter(name: "teamSize", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Number of team members needed")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(MatchResponse), Description = "Match results")]
    public async Task<IActionResult> FindMatchesForProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId}/matches")] HttpRequest req,
        string projectId)
    {
        _logger.LogInformation("Finding matches for project: {ProjectId}", projectId);

        try
        {
            var teamSizeParam = req.Query["teamSize"].FirstOrDefault();
            var teamSize = int.TryParse(teamSizeParam, out var size) ? size : 5;

            var result = await _matcherService.FindMatchesForProjectAsync(projectId, teamSize);
            return new OkObjectResult(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Project not found: {ProjectId}", projectId);
            return new NotFoundObjectResult(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding matches for project {ProjectId}", projectId);
            return new StatusCodeResult(500);
        }
    }

    [Function("ChatMatch")]
    [OpenApiOperation(operationId: "ChatMatch", tags: new[] { "Chat" }, Summary = "Chat with AI to find employees", Description = "Natural language interface to find matching employees")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ChatRequest), Required = true, Description = "Chat message")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ChatResponse), Description = "AI response")]
    public async Task<IActionResult> ChatMatch(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "chat")] HttpRequest req)
    {
        _logger.LogInformation("Processing chat request");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var chatRequest = JsonSerializer.Deserialize<ChatRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (chatRequest == null || string.IsNullOrWhiteSpace(chatRequest.Message))
            {
                return new BadRequestObjectResult(new { message = "Message is required" });
            }

            // Create a match request from the chat message
            var matchRequest = new MatchRequest
            {
                Query = chatRequest.Message,
                TeamSize = chatRequest.TeamSize ?? 5,
                AvailabilityRequired = chatRequest.AvailabilityRequired ?? true
            };

            // Get all employees for analysis
            var employees = await _cosmosDbService.GetAllEmployeesAsync();

            // Generate a conversational response
            var analysis = await _openAIService.GenerateMatchAnalysisAsync(matchRequest, employees);

            return new OkObjectResult(new ChatResponse
            {
                Message = chatRequest.Message,
                Response = analysis,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return new StatusCodeResult(500);
        }
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public int? TeamSize { get; set; }
    public bool? AvailabilityRequired { get; set; }
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
