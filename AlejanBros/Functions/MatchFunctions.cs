using AlejanBros.Models;
using AlejanBros.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
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
    public async Task<IActionResult> FindMatches(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "match")] HttpRequest req)
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
    public async Task<IActionResult> FindMatchesForProject(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projects/{projectId}/matches")] HttpRequest req,
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
    public async Task<IActionResult> ChatMatch(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "chat")] HttpRequest req)
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
