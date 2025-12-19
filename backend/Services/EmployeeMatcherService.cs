using AlejanBros.Models;
using Microsoft.Extensions.Logging;

namespace AlejanBros.Services;

public interface IEmployeeMatcherService
{
    Task<MatchResponse> FindMatchesAsync(MatchRequest request);
    Task<MatchResponse> FindMatchesForProjectAsync(string projectId, int teamSize = 5);
}

public class EmployeeMatcherService : IEmployeeMatcherService
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ISearchService _searchService;
    private readonly IOpenAIService _openAIService;
    private readonly ILogger<EmployeeMatcherService> _logger;

    public EmployeeMatcherService(
        ICosmosDbService cosmosDbService,
        ISearchService searchService,
        IOpenAIService openAIService,
        ILogger<EmployeeMatcherService> logger)
    {
        _cosmosDbService = cosmosDbService;
        _searchService = searchService;
        _openAIService = openAIService;
        _logger = logger;
    }

    public async Task<MatchResponse> FindMatchesAsync(MatchRequest request)
    {
        _logger.LogInformation("Finding matches for query: {Query}, TeamSize: {TeamSize}", request.Query, request.TeamSize);

        // Step 1: Generate embedding for the query
        var queryEmbedding = await _openAIService.GenerateEmbeddingAsync(request.Query);

        // Step 2: Perform hybrid search (text + vector)
        var searchResults = await _searchService.HybridSearchAsync(
            request.Query,
            queryEmbedding,
            top: request.TeamSize * 3); // Get more candidates than needed for better ranking

        // Step 3: Get full employee details from Cosmos DB
        var candidateIds = searchResults.Select(r => r.Id).ToList();
        var candidates = new List<Employee>();

        foreach (var id in candidateIds)
        {
            var employee = await _cosmosDbService.GetEmployeeAsync(id);
            if (employee != null)
            {
                // Filter by availability if required
                if (!request.AvailabilityRequired || employee.Availability != AvailabilityStatus.Unavailable)
                {
                    // Filter by minimum experience if specified
                    if (!request.MinimumExperience.HasValue || employee.YearsOfExperience >= request.MinimumExperience.Value)
                    {
                        candidates.Add(employee);
                    }
                }
            }
        }

        if (!candidates.Any())
        {
            _logger.LogWarning("No candidates found matching the criteria");
            return new MatchResponse
            {
                Query = request.Query,
                Matches = new List<MatchResult>(),
                Summary = "No candidates found matching the specified criteria.",
                TotalCandidates = 0,
                ProcessingTimeMs = 0
            };
        }

        // Step 4: Use AI to analyze and rank candidates
        _logger.LogInformation("Sending {CandidateCount} candidates to AI for analysis, requesting {TeamSize} matches", candidates.Count, request.TeamSize);
        var response = await _openAIService.AnalyzeAndRankCandidatesAsync(request, candidates);
        _logger.LogInformation("AI returned {MatchCount} matches", response.Matches.Count);

        // Step 5: Limit to requested team size
        response.Matches = response.Matches.Take(request.TeamSize).ToList();

        _logger.LogInformation("Final result: {Count} matches for query", response.Matches.Count);
        return response;
    }

    public async Task<MatchResponse> FindMatchesForProjectAsync(string projectId, int teamSize = 5)
    {
        var project = await _cosmosDbService.GetProjectAsync(projectId);
        if (project == null)
        {
            throw new ArgumentException($"Project with ID {projectId} not found");
        }

        var request = new MatchRequest
        {
            Query = $"{project.Name}: {project.Description}",
            ProjectId = projectId,
            RequiredSkills = project.RequiredSkills.Select(s => s.Name).ToList(),
            TechStack = project.TechStack,
            TeamSize = teamSize > 0 ? teamSize : project.TeamSize,
            AvailabilityRequired = true
        };

        return await FindMatchesAsync(request);
    }
}
