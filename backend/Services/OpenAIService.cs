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

    public async Task<string> GenerateMatchAnalysisAsync(MatchRequest request, IEnumerable<Employee> candidates, List<ConversationMessage>? history = null)
    {
        var chatClient = _client.GetChatClient(_settings.ChatDeployment);

        var systemPrompt = """
            You are an expert HR assistant specializing in matching employees to projects.
            
            LANGUAGE RULE: Always respond in ENGLISH regardless of the user's language.
            
            Analyze the project requirements and candidate profiles to provide recommendations.
            Be concise but thorough in your analysis.
            Focus on skill matches, experience levels, and availability.
            You have access to the conversation history for context.
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
            new SystemChatMessage(systemPrompt)
        };

        // Add conversation history for context
        if (history != null && history.Count > 0)
        {
            foreach (var msg in history)
            {
                if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                    messages.Add(new UserChatMessage(msg.Content));
                else if (msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    messages.Add(new AssistantChatMessage(msg.Content));
            }
        }

        messages.Add(new UserChatMessage(userPrompt));

        var response = await chatClient.CompleteChatAsync(messages);
        return response.Value.Content[0].Text;
    }

    public async Task<MatchResponse> AnalyzeAndRankCandidatesAsync(MatchRequest request, IEnumerable<Employee> candidates)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var candidatesList = candidates.ToList();

        var chatClient = _client.GetChatClient(_settings.ChatDeployment);

        var systemPrompt = $$"""
            You are an expert HR assistant. Analyze candidates and return a JSON response.
            
            LANGUAGE RULE: Always respond in ENGLISH regardless of the user's language.
            
            You must respond ONLY with valid JSON in the following format:
            {
                "matches": [
                    {
                        "employeeId": "string (MUST be the exact Id field from the candidate data)",
                        "matchScore": 0.0-1.0,
                        "matchReasons": ["reason1", "reason2"],
                        "gaps": ["gap1", "gap2"]
                    }
                ],
                "summary": "Brief overall summary",
                "analysis": "Detailed markdown analysis explaining your ranking decisions, why each candidate was selected, team composition recommendations, and any concerns. Use headers and bullet points for readability. Use employee NAMES only in this field, never IDs."
            }
            
            CRITICAL: The "employeeId" field MUST contain the EXACT "Id" value from the candidate JSON data (e.g., a GUID like "abc123-def-456"). Do NOT use the employee's name in employeeId.
            NOTE: In the "analysis" and "summary" text fields, use employee NAMES (e.g., "Isabella Moreno"), NOT IDs. IDs are only for the employeeId field.
            
            Order matches by matchScore descending. 
            IMPORTANT: Return EXACTLY {{request.TeamSize}} matches (or all candidates if fewer are available). Do not limit to 5.
            
            MATCH SCORE GUIDELINES:
            - 0.7-1.0: Strong match - candidate has most/all required skills
            - 0.5-0.69: Moderate match - candidate has some required skills but notable gaps
            - 0.3-0.49: Weak match - candidate lacks many required skills
            - 0.0-0.29: Poor match - candidate is not suitable for this project
            
            Be honest with scores. If candidates don't have the required skills, give them low scores.
            
            CRITICAL RULES FOR GAPS:
            - "gaps" should ONLY contain skills that are EXPLICITLY MENTIONED in the user's query, Required Skills, or Tech Stack fields above.
            - Do NOT infer or assume additional technologies. Only reference what the user explicitly asked for.
            - If the user only mentions "Android/Kotlin", do NOT add Firebase, Jetpack Compose, or any other technology as a gap unless the user explicitly mentioned them.
            - If a candidate has extra skills beyond what the project requires, that is NOT a gap - it's a bonus.
            - Having additional skills the project doesn't need should be mentioned positively in matchReasons, not as gaps.
            - A gap is strictly: a skill EXPLICITLY listed by the user that the candidate does NOT have.
            - If the candidate has all explicitly required skills, gaps should be an empty array.
            
            CRITICAL: Only evaluate candidates against what the user EXPLICITLY requested. Do not expand the requirements with related technologies.
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
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            Temperature = 0f // More deterministic responses for consistent scoring
        };

        var response = await chatClient.CompleteChatAsync(messages, options);
        var jsonResponse = response.Value.Content[0].Text;

        _logger.LogDebug("OpenAI Response: {Response}", jsonResponse);

        AnalysisResponse? analysisResult;
        try
        {
            analysisResult = JsonSerializer.Deserialize<AnalysisResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse OpenAI response, attempting to extract matches manually. Response: {Response}", jsonResponse);
            
            // Fallback: return all candidates with default scores based on order
            var fallbackMatches = candidatesList.Select((c, index) => new MatchResult
            {
                Employee = c,
                MatchScore = 1.0 - (index * 0.05), // Decreasing score by position
                MatchReasons = new List<string> { "Matched based on search criteria" },
                Gaps = new List<string>(),
                SkillMatches = CalculateSkillMatches(c, request)
            }).ToList();

            return new MatchResponse
            {
                Query = request.Query,
                Matches = fallbackMatches.Take(request.TeamSize).ToList(),
                Summary = "Analysis completed with fallback scoring.",
                TotalCandidates = candidatesList.Count,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }

        stopwatch.Stop();

        var matchResults = new List<MatchResult>();
        foreach (var match in analysisResult?.Matches ?? new List<AnalysisMatch>())
        {
            // Try to find by ID first, then fallback to name matching (in case AI returns name instead of ID)
            var employee = candidatesList.FirstOrDefault(c => c.Id == match.EmployeeId);
            
            if (employee == null)
            {
                // Fallback: try matching by name (case-insensitive)
                employee = candidatesList.FirstOrDefault(c => 
                    c.Name.Equals(match.EmployeeId, StringComparison.OrdinalIgnoreCase));
                
                if (employee == null)
                {
                    _logger.LogWarning("Employee not found for ID/Name: {EmployeeId}. Available IDs: {AvailableIds}", 
                        match.EmployeeId, 
                        string.Join(", ", candidatesList.Select(c => $"{c.Id} ({c.Name})")));
                    continue;
                }
            }
            
            matchResults.Add(new MatchResult
            {
                Employee = employee,
                MatchScore = match.MatchScore,
                MatchReasons = match.MatchReasons,
                Gaps = match.Gaps,
                SkillMatches = CalculateSkillMatches(employee, request)
            });
        }

        var orderedMatches = matchResults.OrderByDescending(m => m.MatchScore).ToList();
        
        // Threshold check: if best match score is below 0.5, recommend training/external hiring
        const double MinimumAcceptableScore = 0.5;
        var bestScore = orderedMatches.FirstOrDefault()?.MatchScore ?? 0;
        var hasSufficientMatches = bestScore >= MinimumAcceptableScore;
        
        string? recommendation = null;
        string analysis = analysisResult?.Analysis ?? string.Empty;
        
        if (!hasSufficientMatches)
        {
            recommendation = "Based on the analysis, there are no employees that are a strong fit for this project's requirements. We recommend:\n\n" +
                           "1. **Training existing employees** - Consider upskilling team members in the required technologies\n" +
                           "2. **External contracting** - Hire contractors or consultants with the specific expertise needed\n" +
                           "3. **Revising requirements** - If possible, adjust the project's tech stack to better align with available skills";
        }
        
        return new MatchResponse
        {
            Query = request.Query,
            Matches = orderedMatches,
            Summary = analysisResult?.Summary ?? "Analysis complete.",
            Analysis = analysis,
            TotalCandidates = candidatesList.Count,
            ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
            HasSufficientMatches = hasSufficientMatches,
            Recommendation = recommendation
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

    public async Task<bool> IsMatchingRelatedQueryAsync(string message)
    {
        try
        {
            var chatClient = _client.GetChatClient(_settings.ChatDeployment);

            var systemPrompt = """
                You are a classifier that determines if a user message is related to employee-project matching in an HR context.
                
                VALID queries (return "yes"):
                - Finding employees with specific skills
                - Building teams for projects
                - Matching candidates to requirements
                - Questions about employee availability
                - Requests for developer/engineer recommendations
                - Follow-up questions about previously recommended employees
                - Questions asking WHY an employee was ranked/recommended (e.g., "Why is María above Carlos?")
                - Questions about employee comparisons or rankings
                - Questions about skill gaps or qualifications
                - Clarifying questions about recommendations
                - Any question that references employees by name
                
                INVALID queries (return "no"):
                - General knowledge questions unrelated to employees
                - Questions about the system/architecture/how the app works
                - Math problems
                - Weather, news, jokes
                - Personal questions about the AI
                - Completely off-topic requests
                
                When in doubt, return "yes" to allow the conversation to continue naturally.
                Respond with ONLY "yes" or "no".
                """;

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(message)
            };

            var response = await chatClient.CompleteChatAsync(messages);
            var result = response.Value.Content[0].Text.Trim().ToLowerInvariant();

            return result.Contains("yes");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to classify query intent, allowing by default");
            return true; // Allow on error to avoid blocking legitimate queries
        }
    }

    public async Task<QueryType> ClassifyQueryTypeAsync(string message)
    {
        try
        {
            var chatClient = _client.GetChatClient(_settings.ChatDeployment);

            var systemPrompt = """
                Classify the user message into one of these categories:
                
                1. MATCH - User wants to find, search, or build a team of employees for a project
                   Examples: "Find React developers", "I need a team for a .NET project", "Who is available for cloud work?"
                
                2. QUESTION - User is asking a specific question about an employee or employees
                   Examples: "What is María's availability?", "Tell me about Carlos", "What skills does Laura have?"
                
                3. FOLLOWUP - User is asking a follow-up about previous recommendations or rankings
                   Examples: "Why is she ranked higher?", "Tell me more about the first one", "Why was María recommended?"
                
                4. SOCIAL - Greetings, thanks, goodbyes, or general social pleasantries
                   Examples: "Hello", "Hi there", "Thank you", "Thanks!", "Goodbye", "Bye", "Good morning", "Hola", "Gracias", "Adiós"
                
                5. OFFTOPIC - Anything not related to employees, projects, or social interaction
                   Examples: "What's the weather?", "Tell me a joke", "How does this system work?"
                
                Respond with ONLY one word: MATCH, QUESTION, FOLLOWUP, SOCIAL, or OFFTOPIC
                """;

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(message)
            };

            var response = await chatClient.CompleteChatAsync(messages);
            var result = response.Value.Content[0].Text.Trim().ToUpperInvariant();

            return result switch
            {
                "MATCH" => QueryType.MatchRequest,
                "QUESTION" => QueryType.EmployeeQuestion,
                "FOLLOWUP" => QueryType.FollowUp,
                "SOCIAL" => QueryType.Social,
                _ => QueryType.OffTopic
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to classify query type, defaulting to MatchRequest");
            return QueryType.MatchRequest;
        }
    }

    public async Task<string> AnswerEmployeeQuestionAsync(string question, IEnumerable<Employee> employees, List<ConversationMessage>? history = null)
    {
        var chatClient = _client.GetChatClient(_settings.ChatDeployment);

        var systemPrompt = """
            You are an HR assistant. Answer the user's question about employees based on the provided employee data.
            
            LANGUAGE RULE: Always respond in ENGLISH regardless of the user's language.
            
            Be concise and helpful. Only answer questions about the employees in the data provided.
            If the employee mentioned is not in the data, say so politely.
            Do NOT recommend or rank employees unless explicitly asked.
            You have access to the conversation history for context about previous recommendations.
            """;

        var employeesJson = JsonSerializer.Serialize(employees.Select(e => new
        {
            e.Name,
            e.JobTitle,
            e.Department,
            e.YearsOfExperience,
            Skills = e.Skills.Select(s => new { s.Name, Level = s.Level.ToString() }),
            Availability = e.Availability.ToString(),
            e.Location,
            e.Bio,
            e.Certifications
        }), new JsonSerializerOptions { WriteIndented = true });

        var userPrompt = $"""
            Employee Data:
            {employeesJson}

            User Question: {question}
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt)
        };

        // Add conversation history for context
        if (history != null && history.Count > 0)
        {
            foreach (var msg in history)
            {
                if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                    messages.Add(new UserChatMessage(msg.Content));
                else if (msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    messages.Add(new AssistantChatMessage(msg.Content));
            }
        }

        messages.Add(new UserChatMessage(userPrompt));

        var response = await chatClient.CompleteChatAsync(messages);
        return response.Value.Content[0].Text;
    }

    public async Task<string> GenerateSocialResponseAsync(string message)
    {
        var chatClient = _client.GetChatClient(_settings.ChatDeployment);

        var systemPrompt = """
            You are a friendly HR assistant. Respond politely to greetings, thanks, and goodbyes.
            
            LANGUAGE RULE: Always respond in ENGLISH regardless of the user's language.
            
            Keep your response brief and warm.
            Always include a gentle reminder of what you can help with at the end.
            
            Your capabilities:
            - Finding developers with specific skills
            - Building teams for projects
            - Matching employees to requirements
            - Answering questions about employees
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(message)
        };

        var response = await chatClient.CompleteChatAsync(messages);
        return response.Value.Content[0].Text;
    }

    private class AnalysisResponse
    {
        public List<AnalysisMatch> Matches { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public string Analysis { get; set; } = string.Empty;
    }

    private class AnalysisMatch
    {
        public string EmployeeId { get; set; } = string.Empty;
        public double MatchScore { get; set; }
        public List<string> MatchReasons { get; set; } = new();
        public List<string> Gaps { get; set; } = new();
    }
}
