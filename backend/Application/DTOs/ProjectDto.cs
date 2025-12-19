using System.Text.Json.Serialization;

namespace AlejanBros.Application.DTOs;

public class ProjectDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("requiredSkills")]
    public List<RequiredSkillDto> RequiredSkills { get; set; } = new();

    [JsonPropertyName("techStack")]
    public List<string> TechStack { get; set; } = new();

    [JsonPropertyName("teamSize")]
    public int TeamSize { get; set; }

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public DateTime? StartDate { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("projectType")]
    public string ProjectType { get; set; } = string.Empty;

    [JsonPropertyName("client")]
    public string Client { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class RequiredSkillDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("minimumLevel")]
    public int MinimumLevel { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}
