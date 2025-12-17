using System.Text.Json.Serialization;

namespace AlejanBros.Models;

public class MatchRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("projectId")]
    public string? ProjectId { get; set; }

    [JsonPropertyName("requiredSkills")]
    public List<string>? RequiredSkills { get; set; }

    [JsonPropertyName("techStack")]
    public List<string>? TechStack { get; set; }

    [JsonPropertyName("minimumExperience")]
    public int? MinimumExperience { get; set; }

    [JsonPropertyName("teamSize")]
    public int TeamSize { get; set; } = 5;

    [JsonPropertyName("availabilityRequired")]
    public bool AvailabilityRequired { get; set; } = true;
}

public class MatchResult
{
    [JsonPropertyName("employee")]
    public Employee Employee { get; set; } = null!;

    [JsonPropertyName("matchScore")]
    public double MatchScore { get; set; }

    [JsonPropertyName("matchReasons")]
    public List<string> MatchReasons { get; set; } = new();

    [JsonPropertyName("skillMatches")]
    public List<SkillMatch> SkillMatches { get; set; } = new();

    [JsonPropertyName("gaps")]
    public List<string> Gaps { get; set; } = new();
}

public class SkillMatch
{
    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = string.Empty;

    [JsonPropertyName("employeeLevel")]
    public SkillLevel EmployeeLevel { get; set; }

    [JsonPropertyName("requiredLevel")]
    public SkillLevel? RequiredLevel { get; set; }

    [JsonPropertyName("isMatch")]
    public bool IsMatch { get; set; }
}

public class MatchResponse
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("matches")]
    public List<MatchResult> Matches { get; set; } = new();

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("totalCandidates")]
    public int TotalCandidates { get; set; }

    [JsonPropertyName("processingTimeMs")]
    public long ProcessingTimeMs { get; set; }
}
