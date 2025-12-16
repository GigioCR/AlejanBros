using System.ClientModel;
using System.Text;
using System.Text.Json;
using AlejanBros.Configuration;
using AlejanBros.Models;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace AlejanBros.Services;

public class OpenAIService : IOpenAIService
{
    private readonly AzureOpenAIClient _client;
    private readonly OpenAISettings _settings;
    private readonly ILogger<OpenAIService> _logger;

    public OpenAIService(
        AzureOpenAIClient client,
        IOptions<AzureSettings> settings,
        ILogger<OpenAIService> logger)
    {
        _client = client;
        _settings = settings.Value.OpenAI;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        try
        {
            var embeddingClient = _client.GetEmbeddingClient(_settings.EmbeddingDeployment);
            var response = await embeddingClient.GenerateEmbeddingAsync(text);
            return response.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text");
            throw;
        }
    }

    public async Task<string> GenerateMatchAnalysisAsync(MatchRequest request, IEnumerable<Employee> candidates)
    {
        var chatClient = _client.GetChatClient(_settings.ChatDeployment);

        var systemPrompt = """
            You are an expert HR assistant specializing in matching employees to projects.
            Analyze the project requirements and candidate profiles to provide recommendations.
            Be concise but thorough in your analysis.
            Focus on skill matches, experience levels, and availability.
            """;

        var candidatesJson = JsonSerializer.Serialize(candidates.Select(c => new
        {
            c.Name,
            c.JobTitle,
            c.Department,
            c.YearsOfExperience,
            Skills = c.Skills.Select(s => new { s.Name, Level = s.Level.ToString(), s.YearsUsed }),
            c.Certifications,
            Availability = c.Availability.ToString(),
            c.Bio
        }), new JsonSerializerOptions { WriteIndented = true });

        var userPrompt = $"""
            Project Requirements:
            Query: {request.Query}
            Required Skills: {string.Join(", ", request.RequiredSkills ?? new List<string>())}
            Tech Stack: {string.Join(", ", request.TechStack ?? new List<string>())}
            Minimum Experience: {request.MinimumExperience ?? 0} years
            Team Size Needed: {request.TeamSize}

            Available Candidates:
            {candidatesJson}

            Please analyze these candidates and provide:
            1. A ranked list of the best matches
            2. Key reasons for each recommendation
            3. Any skill gaps that should be addressed
            4. Overall team composition recommendation
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var response = await chatClient.CompleteChatAsync(messages);
        return response.Value.Content[0].Text;
    }

    public async Task<MatchResponse> AnalyzeAndRankCandidatesAsync(MatchRequest request, IEnumerable<Employee> candidates)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var candidatesList = candidates.ToList();

        var chatClient = _client.GetChatClient(_settings.ChatDeployment);

        var systemPrompt = """
            You are an expert HR assistant. Analyze candidates and return a JSON response.
            You must respond ONLY with valid JSON in the following format:
            {
                "matches": [
                    {
                        "employeeId": "string",
                        "matchScore": 0.0-1.0,
                        "matchReasons": ["reason1", "reason2"],
                        "gaps": ["gap1", "gap2"]
                    }
                ],
                "summary": "Brief overall summary"
            }
            Order matches by matchScore descending. Include all candidates.
            """;

        var candidatesJson = JsonSerializer.Serialize(candidatesList.Select(c => new
        {
            c.Id,
            c.Name,
            c.JobTitle,
            c.YearsOfExperience,
            Skills = c.Skills.Select(s => new { s.Name, Level = s.Level.ToString() }),
            Availability = c.Availability.ToString()
        }));

        var userPrompt = $"""
            Project: {request.Query}
            Required Skills: {string.Join(", ", request.RequiredSkills ?? new List<string>())}
            Tech Stack: {string.Join(", ", request.TechStack ?? new List<string>())}
            Min Experience: {request.MinimumExperience ?? 0} years
            Team Size: {request.TeamSize}
            Availability Required: {request.AvailabilityRequired}

            Candidates: {candidatesJson}
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        var response = await chatClient.CompleteChatAsync(messages, options);
        var jsonResponse = response.Value.Content[0].Text;

        var analysisResult = JsonSerializer.Deserialize<AnalysisResponse>(jsonResponse, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        stopwatch.Stop();

        var matchResults = new List<MatchResult>();
        foreach (var match in analysisResult?.Matches ?? new List<AnalysisMatch>())
        {
            var employee = candidatesList.FirstOrDefault(c => c.Id == match.EmployeeId);
            if (employee != null)
            {
                matchResults.Add(new MatchResult
                {
                    Employee = employee,
                    MatchScore = match.MatchScore,
                    MatchReasons = match.MatchReasons,
                    Gaps = match.Gaps,
                    SkillMatches = CalculateSkillMatches(employee, request)
                });
            }
        }

        return new MatchResponse
        {
            Query = request.Query,
            Matches = matchResults.OrderByDescending(m => m.MatchScore).ToList(),
            Summary = analysisResult?.Summary ?? "Analysis complete.",
            TotalCandidates = candidatesList.Count,
            ProcessingTimeMs = stopwatch.ElapsedMilliseconds
        };
    }

    private List<SkillMatch> CalculateSkillMatches(Employee employee, MatchRequest request)
    {
        var skillMatches = new List<SkillMatch>();
        var requiredSkills = (request.RequiredSkills ?? new List<string>())
            .Concat(request.TechStack ?? new List<string>())
            .Distinct()
            .ToList();

        foreach (var requiredSkill in requiredSkills)
        {
            var employeeSkill = employee.Skills.FirstOrDefault(s =>
                s.Name.Equals(requiredSkill, StringComparison.OrdinalIgnoreCase));

            skillMatches.Add(new SkillMatch
            {
                SkillName = requiredSkill,
                EmployeeLevel = employeeSkill?.Level ?? SkillLevel.Beginner,
                RequiredLevel = SkillLevel.Intermediate,
                IsMatch = employeeSkill != null
            });
        }

        return skillMatches;
    }

    private class AnalysisResponse
    {
        public List<AnalysisMatch> Matches { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
    }

    private class AnalysisMatch
    {
        public string EmployeeId { get; set; } = string.Empty;
        public double MatchScore { get; set; }
        public List<string> MatchReasons { get; set; } = new();
        public List<string> Gaps { get; set; } = new();
    }
}
