using System.Text.Json.Serialization;

namespace AlejanBros.Application.DTOs;

public class MatchRequestDto
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

    [JsonPropertyName("availabilityConstraint")]
    public int AvailabilityConstraint { get; set; } = 1; // ExcludeUnavailable
}

public class MatchResponseDto
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("matches")]
    public List<MatchResultDto> Matches { get; set; } = new();

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("analysis")]
    public string Analysis { get; set; } = string.Empty;

    [JsonPropertyName("totalCandidates")]
    public int TotalCandidates { get; set; }

    [JsonPropertyName("processingTimeMs")]
    public long ProcessingTimeMs { get; set; }

    [JsonPropertyName("hasSufficientMatches")]
    public bool HasSufficientMatches { get; set; } = true;

    [JsonPropertyName("recommendation")]
    public string? Recommendation { get; set; }

    [JsonPropertyName("appliedAvailabilityConstraint")]
    public int AppliedAvailabilityConstraint { get; set; }
}

public class MatchResultDto
{
    [JsonPropertyName("employee")]
    public EmployeeDto Employee { get; set; } = new();

    [JsonPropertyName("matchScore")]
    public double MatchScore { get; set; }

    [JsonPropertyName("baseMatchScore")]
    public double? BaseMatchScore { get; set; }

    [JsonPropertyName("matchReasons")]
    public List<string> MatchReasons { get; set; } = new();

    [JsonPropertyName("bonusReasons")]
    public List<string>? BonusReasons { get; set; }

    [JsonPropertyName("gaps")]
    public List<string> Gaps { get; set; } = new();

    [JsonPropertyName("skillMatches")]
    public List<object> SkillMatches { get; set; } = new();

    [JsonPropertyName("isFallbackCandidate")]
    public bool? IsFallbackCandidate { get; set; }
}
