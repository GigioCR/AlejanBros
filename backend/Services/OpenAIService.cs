using System.ClientModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogWarning(ex, "Rate limit exceeded for embedding generation");
            throw new InvalidOperationException("Service is experiencing high demand. Please try again in a moment.", ex);
        }
        catch (ClientResultException ex) when (ex.Status >= 500)
        {
            _logger.LogError(ex, "OpenAI service error during embedding generation");
            throw new InvalidOperationException("AI service is temporarily unavailable. Please try again.", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout generating embedding");
            throw new InvalidOperationException("Request timed out. Please try again.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating embedding");
            throw new InvalidOperationException("Unable to process request. Please try again.", ex);
        }
    }

    public async Task<MatchResponse> AnalyzeAndRankCandidatesAsync(MatchRequest request, IEnumerable<Employee> candidates)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var candidatesList = candidates.ToList();

        var chatClient = _client.GetChatClient(_settings.ChatDeployment);

        ExtractedRequirements extracted;
        try
        {
            extracted = await ExtractRequirementsAndPreferencesAsync(chatClient, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract requirements from query");
            throw new InvalidOperationException("Unable to parse query requirements. Please try again.", ex);
        }
        var effectiveRequest = new MatchRequest
        {
            Query = request.Query,
            ProjectId = request.ProjectId,
            RequiredSkills = extracted.RequiredSkills,
            TechStack = extracted.TechStack,
            MinimumExperience = extracted.MinimumExperience ?? request.MinimumExperience,
            TeamSize = request.TeamSize,
            AvailabilityRequired = request.AvailabilityRequired,
            AvailabilityConstraint = extracted.AvailabilityConstraint
        };

        var weightedMatches = candidatesList.Select(c => BuildScoredMatch(c, effectiveRequest, extracted.Preference)).ToList();
        var topMatches = weightedMatches
            .OrderByDescending(m => m.MatchScore)
            .Take(request.TeamSize)
            .ToList();

        var systemPrompt = """
            You are an expert HR assistant. You will be given project requirements and a pre-ranked list of candidates.

            LANGUAGE RULE: Always respond in ENGLISH regardless of the user's language.

            Your job is to explain WHY each candidate was recommended.
            DO NOT calculate or change any numeric scores. Scores are already computed.
            DO NOT reorder the candidates.

            You must respond ONLY with valid JSON in the following format:
            {
                "matches": [
                    {
                        "employeeId": "string (MUST be the exact Id field from the candidate data)",
                        "matchReasons": ["reason1", "reason2"],
                        "bonusReasons": ["bonus1", "bonus2"],
                        "gaps": ["gap1", "gap2"]
                    }
                ],
                "summary": "Brief overall summary",
                "analysis": "Detailed markdown analysis explaining your ranking decisions, why each candidate was selected, team composition recommendations, and any concerns. Use headers and bullet points for readability. Use employee NAMES only in this field, never IDs."
            }

            CRITICAL: You MUST return a match entry for EVERY candidate provided in the list, in the same order. Do not skip any candidates.

            CRITICAL: The "employeeId" field MUST contain the EXACT "Id" value from the candidate JSON data (e.g., a GUID like "abc123-def-456"). Do NOT use the employee's name in employeeId.
            NOTE: In the "analysis" and "summary" text fields, use employee NAMES (e.g., "Isabella Moreno"), NOT IDs. IDs are only for the employeeId field.

            CRITICAL RULES FOR GAPS:
            - "gaps" must ONLY contain skill/technology NAMES that appear in Required Skills or Tech Stack.
            - NEVER include "Availability", "Experience", or any non-skill criteria in gaps.
            - Do NOT infer or assume additional technologies. Only reference what the user explicitly asked for.
            - If a candidate has extra skills beyond what the project requires, that is NOT a gap - it's a bonus.
            - A gap is strictly: a skill EXPLICITLY listed by the user that the candidate does NOT have.
            - If the candidate has all explicitly required skills, gaps should be an empty array.

            CRITICAL RULES FOR MATCH REASONS VS BONUS REASONS:
            - matchReasons must ONLY reference criteria explicitly requested by the user (skills mentioned in the user's query, Required Skills, Tech Stack, availability, and minimum experience).
            - bonusReasons may include relevant strengths NOT explicitly requested. Do NOT put bonus skills in matchReasons.

            FALLBACK / WEAK MATCH RULE:
            - If a candidate matches ZERO of the explicitly requested skills, they are a fallback option.
            - In that case include a matchReason like "Fallback: does not match required skills; selected due to availability/experience/limited pool".
            """;

        var candidatesJson = JsonSerializer.Serialize(topMatches.Select(m => new
        {
            Id = m.Employee.Id,
            m.Employee.Name,
            m.Employee.JobTitle,
            m.Employee.YearsOfExperience,
            Skills = m.Employee.Skills.Select(s => new { s.Name, Level = s.Level.ToString() }),
            Availability = m.Employee.Availability.ToString(),
            BaseMatchScore = m.BaseMatchScore,
            AdjustedMatchScore = m.MatchScore
        }));

        var userPrompt = $"""
            Project: {request.Query}
            Required Skills: {string.Join(", ", effectiveRequest.RequiredSkills ?? new List<string>())}
            Tech Stack: {string.Join(", ", effectiveRequest.TechStack ?? new List<string>())}
            Min Experience: {effectiveRequest.MinimumExperience ?? 0} years
            Team Size: {request.TeamSize}
            Availability Required: {request.AvailabilityRequired}
            Preference: {extracted.Preference}

            Candidates (pre-ranked with computed scores): {candidatesJson}
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            Temperature = 0f
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
                BaseMatchScore = 1.0 - (index * 0.05),
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

        var aiMatchesById = (analysisResult?.Matches ?? new List<AnalysisMatch>())
            .GroupBy(m => m.EmployeeId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var match in topMatches)
        {
            if (aiMatchesById.TryGetValue(match.Employee.Id, out var aiMatch))
            {
                match.MatchReasons = aiMatch.MatchReasons;
                match.BonusReasons = aiMatch.BonusReasons;
                var allowedGapSkills = (effectiveRequest.RequiredSkills ?? new List<string>())
                    .Concat(effectiveRequest.TechStack ?? new List<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Filter gaps: must be in required skills AND employee must NOT have that skill
                match.Gaps = (aiMatch.Gaps ?? new List<string>())
                    .Where(g => !string.IsNullOrWhiteSpace(g))
                    .Where(g => allowedGapSkills.Any(s => s.Equals(g, StringComparison.OrdinalIgnoreCase)))
                    .Where(g => !match.Employee.Skills.Any(es => es.Name.Equals(g, StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                // Fallback: AI didn't provide details for this candidate, generate detailed ones
                var requiredSkills = (effectiveRequest.RequiredSkills ?? new List<string>())
                    .Concat(effectiveRequest.TechStack ?? new List<string>())
                    .ToList();
                var matchedSkills = requiredSkills
                    .Where(rs => match.Employee.Skills.Any(es => es.Name.Equals(rs, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                var gaps = requiredSkills.Except(matchedSkills, StringComparer.OrdinalIgnoreCase).ToList();

                var reasons = new List<string>();
                
                // Add skill matches with levels
                foreach (var skillName in matchedSkills)
                {
                    var employeeSkill = match.Employee.Skills.First(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
                    reasons.Add($"{employeeSkill.Level} level in {skillName}");
                }
                
                // Add availability
                reasons.Add($"Availability: {match.Employee.Availability}");
                
                // Add experience if relevant
                if (match.Employee.YearsOfExperience >= 5)
                {
                    reasons.Add($"{match.Employee.YearsOfExperience} years of experience");
                }

                match.MatchReasons = reasons.Count > 0 ? reasons : new List<string> { $"Availability: {match.Employee.Availability}" };
                match.Gaps = gaps;
                
                // Add bonus reasons for extra skills
                var bonusSkills = match.Employee.Skills
                    .Where(s => !requiredSkills.Any(rs => rs.Equals(s.Name, StringComparison.OrdinalIgnoreCase)))
                    .Where(s => s.Level >= SkillLevel.Advanced)
                    .Take(3)
                    .ToList();
                    
                if (bonusSkills.Any())
                {
                    match.BonusReasons = bonusSkills.Select(s => $"{s.Level} level in {s.Name}").ToList();
                }
            }
        }

        var orderedMatches = topMatches;
        
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
            Recommendation = recommendation,
            AppliedAvailabilityConstraint = extracted.AvailabilityConstraint
        };
    }

    private async Task<ExtractedRequirements> ExtractRequirementsAndPreferencesAsync(ChatClient chatClient, MatchRequest request)
    {
        var existingRequiredSkills = request.RequiredSkills ?? new List<string>();
        var existingTechStack = request.TechStack ?? new List<string>();

        var systemPrompt = """
            Extract structured matching requirements and user priorities from the user's message.

            LANGUAGE RULE: Always respond in ENGLISH regardless of the user's language.

            Respond ONLY with valid JSON in this format:
            {
              "requiredSkills": ["skill1", "skill2"],
              "techStack": ["tech1", "tech2"],
              "minimumExperience": 0,
              "preference": "balanced" | "availability" | "skills" | "experience",
              "availabilityConstraint": "any" | "excludeUnavailable" | "onlyAvailable"
            }

            Rules:
            - Only include skills/technologies explicitly mentioned by the user.
            - Do NOT infer related technologies.
            - If the user does not specify a minimum experience, set minimumExperience to 0.
            
            Availability rules:
            - If user says "ONLY available", "must be available", "strictly available", "exclude partially available": set availabilityConstraint to "onlyAvailable" and preference to "availability"
            - If user says "prioritize availability", "availability is critical/important", "prefer available": set availabilityConstraint to "excludeUnavailable" and preference to "availability"
            - If no availability mentioned: set availabilityConstraint to "excludeUnavailable" and preference to "balanced"
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(request.Query)
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            Temperature = 0f
        };

        ChatCompletion response;
        try
        {
            response = await chatClient.CompleteChatAsync(messages, options);
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogWarning(ex, "Rate limit exceeded for chat completion");
            throw new InvalidOperationException("Service is experiencing high demand. Please try again in a moment.", ex);
        }
        catch (ClientResultException ex) when (ex.Status >= 500)
        {
            _logger.LogError(ex, "OpenAI service error during chat completion");
            throw new InvalidOperationException("AI service is temporarily unavailable. Please try again.", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout during chat completion");
            throw new InvalidOperationException("Request timed out. Please try again.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during chat completion");
            throw new InvalidOperationException("Unable to process request. Please try again.", ex);
        }

        var json = response.Content[0].Text;

        ExtractedRequirements? extracted;
        try
        {
            extracted = JsonSerializer.Deserialize<ExtractedRequirements>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            });
        }
        catch
        {
            extracted = null;
        }

        var requiredSkills = existingRequiredSkills.Count > 0
            ? existingRequiredSkills
            : (extracted?.RequiredSkills ?? new List<string>());

        var techStack = existingTechStack.Count > 0
            ? existingTechStack
            : (extracted?.TechStack ?? new List<string>());

        return new ExtractedRequirements
        {
            RequiredSkills = requiredSkills.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList(),
            TechStack = techStack.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList(),
            MinimumExperience = extracted?.MinimumExperience,
            Preference = extracted?.Preference ?? PreferenceType.Balanced,
            AvailabilityConstraint = extracted?.AvailabilityConstraint ?? AvailabilityConstraint.ExcludeUnavailable
        };
    }

    private MatchResult BuildScoredMatch(Employee employee, MatchRequest request, PreferenceType preference)
    {
        var requiredSkills = (request.RequiredSkills ?? new List<string>())
            .Concat(request.TechStack ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var matchedSkills = requiredSkills
            .Where(rs => employee.Skills.Any(es => es.Name.Equals(rs, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var skillMatchRatio = requiredSkills.Count == 0 ? 0.0 : (double)matchedSkills.Count / requiredSkills.Count;
        var skillLevelScore = matchedSkills.Count == 0
            ? 0.0
            : matchedSkills
                .Select(rs => employee.Skills.First(es => es.Name.Equals(rs, StringComparison.OrdinalIgnoreCase)).Level)
                .Average(l => GetSkillLevelScore(l));

        var availabilityScore = GetAvailabilityScore(employee.Availability);
        var experienceScore = GetExperienceScore(employee.YearsOfExperience);

        var baseScore = (skillMatchRatio * 0.50) + (skillLevelScore * 0.25) + (availabilityScore * 0.15) + (experienceScore * 0.10);

        var (wSkillMatch, wSkillLevel, wAvailability, wExperience) = preference switch
        {
            PreferenceType.Availability => (0.35, 0.20, 0.35, 0.10),
            PreferenceType.Skills => (0.60, 0.25, 0.10, 0.05),
            PreferenceType.Experience => (0.45, 0.20, 0.10, 0.25),
            _ => (0.50, 0.25, 0.15, 0.10)
        };

        var adjustedScore = (skillMatchRatio * wSkillMatch) + (skillLevelScore * wSkillLevel) + (availabilityScore * wAvailability) + (experienceScore * wExperience);

        if (requiredSkills.Count > 0 && matchedSkills.Count == 0)
        {
            baseScore = Math.Min(baseScore, 0.29);
            adjustedScore = Math.Min(adjustedScore, 0.29);
        }

        return new MatchResult
        {
            Employee = employee,
            MatchScore = adjustedScore,
            BaseMatchScore = baseScore,
            SkillMatches = CalculateSkillMatches(employee, request)
        };
    }

    private static double GetSkillLevelScore(SkillLevel level)
    {
        return level switch
        {
            SkillLevel.Expert => 1.0,
            SkillLevel.Advanced => 0.8,
            SkillLevel.Intermediate => 0.5,
            SkillLevel.Beginner => 0.2,
            _ => 0.0
        };
    }

    private static double GetAvailabilityScore(AvailabilityStatus availability)
    {
        return availability switch
        {
            AvailabilityStatus.Available => 1.0,
            AvailabilityStatus.PartiallyAvailable => 0.5,
            AvailabilityStatus.Unavailable => 0.0,
            _ => 0.0
        };
    }

    private static double GetExperienceScore(int years)
    {
        return years switch
        {
            >= 10 => 1.0,
            >= 5 => 0.8,
            >= 3 => 0.6,
            >= 1 => 0.4,
            _ => 0.2
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

            ChatCompletion response;
            try
            {
                response = await chatClient.CompleteChatAsync(messages);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to classify query, allowing by default");
                return true; // Allow on error
            }

            var result = response.Content[0].Text.Trim().ToLowerInvariant();
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

            ChatCompletion response;
            try
            {
                response = await chatClient.CompleteChatAsync(messages);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to classify query type, defaulting to MatchRequest");
                return QueryType.MatchRequest;
            }

            var result = response.Content[0].Text.Trim().ToUpperInvariant();
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
            _logger.LogError(ex, "Unexpected error classifying query type");
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

    public async Task<AvailabilityConstraint> ExtractAvailabilityConstraintAsync(string query)
    {
        try
        {
            var chatClient = _client.GetChatClient(_settings.ChatDeployment);

            var systemPrompt = "Analyze the user's query and determine the availability constraint. Respond with ONLY one word: onlyAvailable, excludeUnavailable, or any. Rules: Use 'onlyAvailable' ONLY if user uses STRICT language like 'ONLY available', 'must be available', 'no partially available', 'exclude partially available'. Use 'excludeUnavailable' for SOFT preferences like 'preferably available', 'prefer available', 'prioritize availability', 'ideally available', or no mention of availability. Use 'any' if user explicitly wants unavailable included. IMPORTANT: 'preferably' or 'prefer' means SOFT preference = excludeUnavailable, NOT onlyAvailable.";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(query)
            };

            var options = new ChatCompletionOptions { Temperature = 0f };
            ChatCompletion response;
            try
            {
                response = await chatClient.CompleteChatAsync(messages, options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract availability constraint, using default");
                return AvailabilityConstraint.ExcludeUnavailable;
            }

            var result = response.Content[0].Text.Trim().ToLowerInvariant();

            return result switch
            {
                "onlyavailable" => AvailabilityConstraint.OnlyAvailable,
                "any" => AvailabilityConstraint.Any,
                _ => AvailabilityConstraint.ExcludeUnavailable
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error extracting availability constraint");
            return AvailabilityConstraint.ExcludeUnavailable;
        }
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
        public List<string> MatchReasons { get; set; } = new();
        public List<string> BonusReasons { get; set; } = new();
        public List<string> Gaps { get; set; } = new();
    }

    private class ExtractedRequirements
    {
        public List<string> RequiredSkills { get; set; } = new();
        public List<string> TechStack { get; set; } = new();
        public int? MinimumExperience { get; set; }
        public PreferenceType Preference { get; set; } = PreferenceType.Balanced;
        public AvailabilityConstraint AvailabilityConstraint { get; set; } = AvailabilityConstraint.ExcludeUnavailable;
    }

    private enum PreferenceType
    {
        Balanced,
        Availability,
        Skills,
        Experience
    }
}
