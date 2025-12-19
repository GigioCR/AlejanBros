using AlejanBros.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        try
        {
            // Step 1: Extract availability constraint from user query BEFORE filtering
            AvailabilityConstraint extractedConstraint;
            try
            {
                extractedConstraint = await _openAIService.ExtractAvailabilityConstraintAsync(request.Query);
                _logger.LogInformation("Extracted availability constraint: {Constraint}", extractedConstraint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract availability constraint, using default");
                extractedConstraint = AvailabilityConstraint.ExcludeUnavailable;
            }

            // Step 2: Generate embedding for the query
            float[] queryEmbedding;
            try
            {
                queryEmbedding = await _openAIService.GenerateEmbeddingAsync(request.Query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding for query");
                throw new InvalidOperationException("Unable to process query. Please try again.", ex);
            }

            // Step 3: Perform hybrid search (text + vector)
            IEnumerable<EmployeeSearchDocument> searchResults;
            try
            {
                searchResults = await _searchService.HybridSearchAsync(
                    request.Query,
                    queryEmbedding,
                    top: request.TeamSize * 3); // Get more candidates than needed for better ranking
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Search service failed");
                throw new InvalidOperationException("Search service is temporarily unavailable. Please try again.", ex);
            }

            // Step 4: Get full employee details from Cosmos DB and apply filters
            var candidateIds = searchResults.Select(r => r.Id).ToList();
        var candidates = new List<Employee>();

        foreach (var id in candidateIds)
        {
            try
            {
                var employee = await _cosmosDbService.GetEmployeeAsync(id);
                if (employee != null)
                {
                // Filter by minimum experience first
                if (request.MinimumExperience.HasValue && employee.YearsOfExperience < request.MinimumExperience.Value)
                {
                    continue;
                }

                // Apply extracted availability constraint
                var passesAvailabilityFilter = extractedConstraint switch
                {
                    AvailabilityConstraint.Any => true,
                    AvailabilityConstraint.ExcludeUnavailable => employee.Availability != AvailabilityStatus.Unavailable,
                    AvailabilityConstraint.OnlyAvailable => employee.Availability == AvailabilityStatus.Available,
                    _ => employee.Availability != AvailabilityStatus.Unavailable
                };

                    if (passesAvailabilityFilter)
                    {
                        candidates.Add(employee);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch employee {EmployeeId}, skipping", id);
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
            MatchResponse response;
            try
            {
                response = await _openAIService.AnalyzeAndRankCandidatesAsync(request, candidates);
                _logger.LogInformation("AI returned {MatchCount} matches", response.Matches.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI analysis failed");
                throw new InvalidOperationException("AI analysis service is temporarily unavailable. Please try again.", ex);
            }

        // Step 5: Limit to requested team size
        response.Matches = response.Matches.Take(request.TeamSize).ToList();

        // Step 7: Check if fewer matches than requested due to availability constraint
        var matchCount = response.Matches.Count;
        var requestedCount = request.TeamSize;

        if (matchCount < requestedCount && extractedConstraint == AvailabilityConstraint.OnlyAvailable)
        {
            var insufficientNote = $"\n\n⚠️ **Availability Note**: You requested {requestedCount} fully Available employees, but only **{matchCount}** match your criteria. " +
                                   $"You can:\n" +
                                   $"- Ask for a smaller team size (e.g., \"I need {matchCount} employees...\")\n" +
                                   $"- Include partially available employees (e.g., \"prioritize availability but include partially available\")\n" +
                                   $"- Adjust the required skills";
            response.Analysis += insufficientNote;
            response.HasSufficientMatches = false;
        }

            _logger.LogInformation("Final result: {Count} matches for query", response.Matches.Count);
            return response;
        }
        catch (InvalidOperationException)
        {
            throw; // Re-throw our custom exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in FindMatchesAsync");
            throw new InvalidOperationException("An unexpected error occurred while finding matches. Please try again.", ex);
        }
    }

    public async Task<MatchResponse> FindMatchesForProjectAsync(string projectId, int teamSize = 5)
    {
        try
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
        catch (ArgumentException)
        {
            throw; // Re-throw argument exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding matches for project {ProjectId}", projectId);
            throw new InvalidOperationException($"Unable to find matches for project {projectId}. Please try again.", ex);
        }
    }
}
