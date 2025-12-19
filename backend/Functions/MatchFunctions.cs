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
    [OpenApiOperation(operationId: "ChatMatch", tags: new[] { "Chat" }, Summary = "Chat with AI to find employees", Description = "Natural language interface to find matching employees with structured results")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ChatRequest), Required = true, Description = "Chat message")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ChatResponse), Description = "AI response with matches")]
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

            // Convert history to ConversationMessage format for OpenAI service
            var conversationHistory = chatRequest.History?
                .Select(h => new ConversationMessage { Role = h.Role, Content = h.Content })
                .ToList();

            // Classify the query type to determine how to handle it
            var queryType = await _openAIService.ClassifyQueryTypeAsync(chatRequest.Message);
            _logger.LogInformation("Query classified as: {QueryType}", queryType);

            // Get all employees for context
            var employees = await _cosmosDbService.GetAllEmployeesAsync();

            // Handle based on query type
            switch (queryType)
            {
                case QueryType.Social:
                    // Handle greetings, thanks, goodbyes with a polite response
                    var socialResponse = await _openAIService.GenerateSocialResponseAsync(chatRequest.Message);
                    return new OkObjectResult(new ChatResponse
                    {
                        Message = chatRequest.Message,
                        Response = socialResponse,
                        Matches = new List<MatchResult>(),
                        Summary = string.Empty,
                        TotalCandidates = 0,
                        Timestamp = DateTime.UtcNow
                    });

                case QueryType.OffTopic:
                    return new OkObjectResult(new ChatResponse
                    {
                        Message = chatRequest.Message,
                        Response = "I'm specifically designed to help you find and match employees to projects. I can help you with:\n\n" +
                                   "- **Finding developers** with specific skills (e.g., \"Find React developers\")\n" +
                                   "- **Building teams** for projects (e.g., \"I need a team for a .NET project\")\n" +
                                   "- **Matching employees** to requirements (e.g., \"Who has Azure experience?\")\n" +
                                   "- **Questions about employees** (e.g., \"What is María's availability?\")\n\n" +
                                   "What can I help you with?",
                        Matches = new List<MatchResult>(),
                        Summary = string.Empty,
                        TotalCandidates = 0,
                        Timestamp = DateTime.UtcNow
                    });

                case QueryType.EmployeeQuestion:
                case QueryType.FollowUp:
                    // Answer the question without running full matching, with conversation history
                    var answer = await _openAIService.AnswerEmployeeQuestionAsync(chatRequest.Message, employees, conversationHistory);
                    return new OkObjectResult(new ChatResponse
                    {
                        Message = chatRequest.Message,
                        Response = answer,
                        Matches = new List<MatchResult>(),
                        Summary = string.Empty,
                        TotalCandidates = employees.Count(),
                        Timestamp = DateTime.UtcNow
                    });

                case QueryType.MatchRequest:
                default:
                    // Extract team size from the message if not explicitly provided
                    var teamSize = chatRequest.TeamSize ?? ChatRequest.ExtractTeamSizeFromMessage(chatRequest.Message) ?? 5;

                    // Create a match request from the chat message
                    var matchRequest = new MatchRequest
                    {
                        Query = chatRequest.Message,
                        TeamSize = teamSize,
                        AvailabilityRequired = chatRequest.AvailabilityRequired ?? true
                    };

                    // Single API call that returns both matches and analysis
                    var matchResponse = await _matcherService.FindMatchesAsync(matchRequest);

                    // Build response text: use recommendation if no good matches, otherwise use analysis
                    var responseText = matchResponse.HasSufficientMatches 
                        ? matchResponse.Analysis 
                        : (matchResponse.Recommendation + "\n\n---\n\n" + matchResponse.Analysis);

                    return new OkObjectResult(new ChatResponse
                    {
                        Message = chatRequest.Message,
                        Response = responseText,
                        Matches = matchResponse.Matches,
                        Summary = matchResponse.Summary,
                        TotalCandidates = matchResponse.TotalCandidates,
                        Timestamp = DateTime.UtcNow
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return new StatusCodeResult(500);
        }
    }
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public int? TeamSize { get; set; }
    public List<ChatMessage>? History { get; set; }

    public static int? ExtractTeamSizeFromMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        var lowerMessage = message.ToLowerInvariant();

        // Patterns like "top 3", "best 10", "need 7", "find 5", "give me 8", "show 4"
        var patterns = new[]
        {
            @"(?:top|best|need|find|give\s+me|show|get)\s+(\d+)",
            @"(\d+)\s+(?:employees?|developers?|people|candidates?|members?|engineers?)",
            @"team\s+(?:of\s+)?(\d+)",
            @"(\d+)\s+(?:best|top)"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(lowerMessage, pattern);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var size))
            {
                // Reasonable bounds: 1-50
                return Math.Clamp(size, 1, 50);
            }
        }

        return null;
    }

    public bool? AvailabilityRequired { get; set; }
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public List<MatchResult> Matches { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public int TotalCandidates { get; set; }
    public DateTime Timestamp { get; set; }
}
